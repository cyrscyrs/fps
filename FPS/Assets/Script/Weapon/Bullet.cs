using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 子弹：带伤害值，命中「可受伤目标」就扣血 + 生成命中特效，然后立刻关掉自身特效并回收进对象池。
/// 谁发射的不重要（玩家、僵尸、炮台都能用），子弹只做两件事：
/// 1) 不打到自己人（owner）
/// 2) 打到 IDamageable 就 TakeDamage
///
/// 命中判定用「上一帧位置 -> 当前位置」的射线扫描：子弹速度比物理步长快的时候，
/// 光靠碰撞体/Trigger 会在两帧之间“穿”过目标（速度 20m/s、物理步 0.02s ≈ 每步 0.4m）。
/// 扫描命中后就不会再有物理反弹，视觉效果是子弹在命中点直接消失。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    public ObjectPool<GameObject> pool;
    [SerializeField] private Rigidbody rb;
    public ParticleSystem ps;
    public Light myLight;

    [Header("伤害")]
    [SerializeField] private float damage = 10f;        // 开火时由武器覆盖
    [SerializeField] private GameObject owner;          // 发射者，子弹不会打到它自己

    [Header("飞行")]
    public float speed = 20f;
    public float lifeTime = 3f;

    [Header("命中特效")]
    [SerializeField] private GameObject hitEffectPrefab;     // 命中受伤目标时在命中点生成（血雾）
    [SerializeField] private float hitEffectLifetime = 3f;   // 特效兜底销毁时间（特效自己也会销毁）

    [SerializeField] private bool logHits = false;           // 调试：打印命中/回收过程

    private float timer = 0;
    private bool released = false;
    private Vector3 lastPosition;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (ps == null) ps = GetComponentInChildren<ParticleSystem>();
        if (myLight == null) myLight = GetComponentInChildren<Light>();
    }

    // 从对象池取出时重置状态（同一个子弹会被反复使用）
    private void OnEnable()
    {
        timer = 0f;
        released = false;
        lastPosition = transform.position;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer > lifeTime)
        {
            Release();
            return;
        }

        // 扫一下这一帧走过的路径
        Sweep();
        lastPosition = transform.position;
    }

    public void SetOwner(GameObject shooter)
    {
        owner = shooter;
    }

    public void SetDamage(float value)
    {
        damage = value;
    }

    /// <summary>朝某个方向发射（伤害用预制体上的默认值）。</summary>
    public void Shoot(Vector3 _dir)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        // 关键：武器是用 transform 把子弹“瞬移”到枪口的，刚体的物理位置还停在上一次的位置，
        // 不把它一起搬到枪口，子弹会从错的地方飞出去（第一枪打不中、看起来像被弹开都是这个原因）。
        rb.position = transform.position;
        rb.rotation = transform.rotation;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        lastPosition = transform.position;   // 从枪口开始扫，别把枪口到上一发老位置的线也算进去
        PlayEffects();
        rb.velocity = _dir * speed;
    }

    /// <summary>武器开火用：一次把方向、发射者、伤害都设置好。</summary>
    public void Shoot(Vector3 _dir, GameObject shooter, float bulletDamage)
    {
        SetOwner(shooter);
        SetDamage(bulletDamage);
        Shoot(_dir);
    }

    // 碰撞体/Trigger 作为后备（比如以后把子弹换成慢速的投掷物）
    private void OnTriggerEnter(Collider other)
    {
        if (released || IsOwner(other)) return;

        Vector3 point = other.ClosestPoint(transform.position);
        if (TryDamage(other, SafeClosestPoint(other, transform.position), -transform.forward)) return;
        if (!other.isTrigger) Release();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (released || collision.collider == null || IsOwner(collision.collider)) return;

        ContactPoint contact = collision.GetContact(0);
        if (TryDamage(collision.collider, contact.point, contact.normal)) return;
        Release();
    }

    /// <summary>用射线扫过这一帧移动的这段距离，找到第一个挡路的东西。</summary>
    private void Sweep()
    {
        if (released) return;

        Vector3 delta = transform.position - lastPosition;
        float distance = delta.magnitude;
        if (distance < 0.0001f) return;

        // QueryTriggerInteraction.Ignore：忽略 Trigger（包括子弹自己的碰撞体）
        if (!Physics.Raycast(lastPosition, delta / distance, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
            return;

        if (IsOwner(hit.collider)) return;   // 打到自己人（比如玩家自己的胶囊体）：继续飞

        if (logHits) Debug.Log($"[Bullet] sweep hit {hit.collider.GetType().Name}({hit.collider.gameObject.name}) at {hit.point}", this);
        if (TryDamage(hit.collider, hit.point, hit.normal)) return;
        Release();                            // 打到墙 / 地面 / 掩体
    }

    /// <summary>命中可受伤目标就扣血 + 生成血雾 + 回收，返回是否命中伤害目标。</summary>
    private bool TryDamage(Collider other, Vector3 point, Vector3 normal)
    {
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || target.IsDead) return false;

        target.TakeDamage(damage, owner);
        if (logHits) Debug.Log($"[Bullet] damaged {other.gameObject.name} for {damage} at {point}", this);
        SpawnHitEffect(point, normal);
        Release();
        return true;
    }

    /// <summary>在命中点生成特效（血雾），朝向贴着命中面。</summary>
    private void SpawnHitEffect(Vector3 point, Vector3 normal)
    {
        if (hitEffectPrefab == null) return;

        Quaternion rotation = normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(normal)
            : Quaternion.identity;

        GameObject effect = Instantiate(hitEffectPrefab, point, rotation);
        if (hitEffectLifetime > 0f) Destroy(effect, hitEffectLifetime);
    }

    /// <summary>开火/复用时把特效打开。</summary>
    private void PlayEffects()
    {
        if (myLight != null) myLight.enabled = true;

        if (ps != null)
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    /// <summary>回收前关掉所有特效：粒子停止并清空、点光源关掉、速度清零。</summary>
    private void StopEffects()
    {
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (myLight != null) myLight.enabled = false;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private bool IsOwner(Collider other)
    {
        if (owner == null || other == null) return false;

        return other.gameObject == owner || other.transform.IsChildOf(owner.transform);
    }

    /// <summary>非凸 MeshCollider 不支持 ClosestPoint，直接用子弹当前位置兜底。</summary>
    private static Vector3 SafeClosestPoint(Collider other, Vector3 fallback)
    {
        if (other is BoxCollider || other is SphereCollider || other is CapsuleCollider) return other.ClosestPoint(fallback);

        MeshCollider mesh = other as MeshCollider;
        if (mesh != null && mesh.convex) return other.ClosestPoint(fallback);

        return fallback;
    }

    /// <summary>命中后走这里：关特效 -> 交给对象池。</summary>
    private void Release()
    {
        if (released) return;
        released = true;

        StopEffects();
        if (logHits) Debug.Log($"[Bullet] released at {transform.position}", this);

        if (pool != null) pool.Release(gameObject);          // 玩家武器：回收进对象池
        else Destroy(gameObject);                            // 没走对象池的子弹（比如僵尸发射的）直接销毁
    }
}
