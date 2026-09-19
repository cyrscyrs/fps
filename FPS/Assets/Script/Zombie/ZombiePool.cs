using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;     // Unity 内置对象池 ObjectPool<T>（Unity 2021 以后自带，不用装包）

/// <summary>
/// 僵尸对象池：统一管理僵尸的「生成」和「消失」，避免反复 Instantiate / Destroy。
///
/// 流程：
///   刷怪点 ZombieSpawnPoint -> ZombiePool.Get()    从池里取一只（池子空了才新建）
///   僵尸死亡               -> Zombie.Died 事件
///   ZombiePool             -> 等 corpseLingerTime 秒 -> Release() 把尸体收回来（SetActive(false)）
///
/// 场景里放一个就行（SampleScene 里已经放好一个，预制体是 Assets/Prefabs/ZombiePool.prefab）；
/// 如果场景里忘了放，刷怪点也会自动建一个默认设置的对象池。
/// </summary>
[DisallowMultipleComponent]
public class ZombiePool : MonoBehaviour
{
    [Header("对象池设置")]
    [SerializeField] private int defaultCapacity = 5;                 // 每种僵尸预建几只（同时也是池子内部容量）
    [SerializeField] private int maxSize = 30;                        // 每种僵尸池子最多留几只，多的直接销毁
    [SerializeField] private bool collectionCheck = true;             // 同一只僵尸被重复回收时直接报错，方便抓 bug
    [SerializeField] private List<GameObject> prewarmPrefabs = new List<GameObject>();   // 开局先建好哪些僵尸

    [Header("死亡回收")]
    [SerializeField] private float corpseLingerTime = 5f;             // 僵尸死亡后尸体停留多久再回收到池子（0 = 立刻）

    [Header("调试")]
    [SerializeField] private bool logPool = false;                    // 取用 / 回收时在 Console 打印

    // 一种僵尸预制体对应一个池子
    private readonly Dictionary<GameObject, ObjectPool<Zombie>> pools = new Dictionary<GameObject, ObjectPool<Zombie>>();

    // 记下每只僵尸是从哪个预制体的池子里出来的，回收时才知道该还回哪个池子
    private readonly Dictionary<Zombie, GameObject> sourcePrefabs = new Dictionary<Zombie, GameObject>();

    // 已经死亡、正在等回收的僵尸
    private readonly Dictionary<Zombie, Coroutine> pendingRecycles = new Dictionary<Zombie, Coroutine>();

    private static ZombiePool instance;

    /// <summary>
    /// 场景里的对象池。场景里一个都没有时会自动创建一个（默认设置），
    /// 这样刷怪点预制体拖到任何场景里都能直接用。
    /// </summary>
    public static ZombiePool Instance
    {
        get
        {
            if (instance != null) return instance;

            instance = FindObjectOfType<ZombiePool>();
            if (instance == null)
            {
                instance = new GameObject("[ZombiePool]").AddComponent<ZombiePool>();
                Debug.Log("[ZombiePool] 场景里没有对象池，已自动创建一个（默认设置）。");
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[ZombiePool] 场景里存在多个对象池，{name} 已被忽略（只保留 {instance.name}）。", this);
            enabled = false;
            return;
        }

        instance = this;
    }

    private void Start()
    {
        Prewarm();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        pools.Clear();
        sourcePrefabs.Clear();
        pendingRecycles.Clear();
    }

    // ---------- 对外接口 ----------

    /// <summary>从池里取一只僵尸放到指定位置。池子里没有待用的就新建一只。</summary>
    public Zombie Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[ZombiePool] 没有指定僵尸预制体，取不到僵尸。", this);
            return null;
        }

        Zombie zombie = GetPool(prefab).Get();
        if (zombie == null) return null;                 // 预制体上没有 Zombie 组件之类的问题，上面已经报过错

        sourcePrefabs[zombie] = prefab;
        zombie.PlaceAt(position, rotation);

        if (logPool)
            Debug.Log($"[ZombiePool] 取出僵尸 {zombie.name}（待用 {GetPool(prefab).CountInactive}，" +
                      $"在用 {GetPool(prefab).CountActive}）", zombie);

        return zombie;
    }

    /// <summary>把僵尸还回池子：失活收起来，下次刷怪直接复用。死亡回收走的是这个方法。</summary>
    public void Release(Zombie zombie)
    {
        if (zombie == null) return;

        CancelPendingRecycle(zombie);

        if (!sourcePrefabs.TryGetValue(zombie, out GameObject prefab) || !pools.TryGetValue(prefab, out ObjectPool<Zombie> pool))
        {
            // 不是池子发出去的（例如直接摆在场景里的僵尸），还回去会把计数搞乱，所以只提示不动它
            Debug.LogWarning($"[ZombiePool] {zombie.name} 不是从对象池里取出来的，无法回收。", zombie);
            return;
        }

        if (logPool) Debug.Log($"[ZombiePool] 回收僵尸 {zombie.name}", zombie);

        pool.Release(zombie);
    }

    /// <summary>查某种僵尸在池子里的统计：待用几只、在用几只、一共建过几只（调试用）。</summary>
    public void GetStats(GameObject prefab, out int inactive, out int active, out int total)
    {
        if (prefab != null && pools.TryGetValue(prefab, out ObjectPool<Zombie> pool))
        {
            inactive = pool.CountInactive;
            active = pool.CountActive;
            total = pool.CountAll;
            return;
        }

        inactive = 0;
        active = 0;
        total = 0;
    }

    // ---------- 池子内部 ----------

    private ObjectPool<Zombie> GetPool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<Zombie> pool)) return pool;

        pool = new ObjectPool<Zombie>(
            createFunc: () => CreateZombie(prefab),
            actionOnGet: OnGetFromPool,
            actionOnRelease: OnReleaseToPool,
            actionOnDestroy: OnDestroyPooled,
            collectionCheck: collectionCheck,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);

        pools.Add(prefab, pool);
        return pool;
    }

    /// <summary>池子空了才走这里：真正 Instantiate 一只新的，建好先收起来。</summary>
    private Zombie CreateZombie(GameObject prefab)
    {
        Zombie zombie = Instantiate(prefab, transform).GetComponent<Zombie>();

        if (zombie == null)
        {
            Debug.LogError($"[ZombiePool] {prefab.name} 上没有 Zombie 组件，对象池用不了。", this);
            return null;
        }

        zombie.name = prefab.name + "_Pooled";
        zombie.gameObject.SetActive(false);      // 先收起来，等着被取用
        return zombie;
    }

    private void OnGetFromPool(Zombie zombie)
    {
        if (zombie == null) return;

        zombie.gameObject.SetActive(true);       // 先激活，Animator / 刚体 复位才有意义
        zombie.ResetForReuse();                  // 抹掉上一轮死亡留下的状态
        zombie.Died += OnZombieDied;             // 这只僵尸死了之后由池子安排回收
    }

    private void OnReleaseToPool(Zombie zombie)
    {
        if (zombie == null) return;

        zombie.Died -= OnZombieDied;
        zombie.transform.SetParent(transform, false);
        zombie.gameObject.SetActive(false);
    }

    /// <summary>池子满了还往回还的时候，Unity 会走这里把多出来的僵尸真正销毁。</summary>
    private void OnDestroyPooled(Zombie zombie)
    {
        if (zombie == null) return;

        zombie.Died -= OnZombieDied;
        sourcePrefabs.Remove(zombie);
        Destroy(zombie.gameObject);
    }

    /// <summary>开局按 defaultCapacity 把每种僵尸先建好，避免打架时临时 Instantiate 掉帧。</summary>
    private void Prewarm()
    {
        for (int i = 0; i < prewarmPrefabs.Count; i++)
        {
            GameObject prefab = prewarmPrefabs[i];
            if (prefab == null) continue;

            ObjectPool<Zombie> pool = GetPool(prefab);
            int count = Mathf.Max(0, defaultCapacity - pool.CountInactive);

            // 先全部取出来，再一起还回去。
            // （不能取一只还一只：还回去的那只会被下一次 Get 又拿出来，等于没预热）
            List<Zombie> created = new List<Zombie>(count);
            for (int j = 0; j < count; j++)
            {
                Zombie zombie = pool.Get();
                if (zombie == null) break;
                created.Add(zombie);
            }

            for (int j = 0; j < created.Count; j++)
            {
                pool.Release(created[j]);
            }
        }
    }

    // ---------- 死亡回收 ----------

    private void OnZombieDied(Zombie zombie)
    {
        if (zombie == null) return;

        if (corpseLingerTime <= 0f)
        {
            Release(zombie);                     // 配置成立刻回收，就不等尸体了
            return;
        }

        CancelPendingRecycle(zombie);
        pendingRecycles[zombie] = StartCoroutine(RecycleAfterDelay(zombie, corpseLingerTime));
    }

    private IEnumerator RecycleAfterDelay(Zombie zombie, float delay)
    {
        // 用缩放时间：游戏被 GameOverUI 暂停（timeScale = 0）时，尸体也一起暂停
        yield return new WaitForSeconds(delay);

        pendingRecycles.Remove(zombie);
        Release(zombie);
    }

    private void CancelPendingRecycle(Zombie zombie)
    {
        if (!pendingRecycles.TryGetValue(zombie, out Coroutine routine)) return;

        pendingRecycles.Remove(zombie);
        if (routine != null) StopCoroutine(routine);
    }
}
