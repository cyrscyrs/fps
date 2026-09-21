using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 僵尸：缓存组件、查找玩家、创建状态机，并把每帧的更新转发给当前状态。
/// 挂在僵尸预制体的根物体上（Animator、Rigidbody 要在同一个物体上）。
/// 生成和回收都交给对象池 ZombiePool，脚本里不要自己去 Instantiate / Destroy。
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class Zombie : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float moveSpeed = 2.3f;        // 追击速度（米/秒）
    [SerializeField] private float turnSpeed = 8f;          // 转身速度，越大转得越快
    [SerializeField] private float modelYawOffset = 0f;     // 模型正面不是 +Z 时改成 180 修正

    [Header("距离判定（米）")]
    [SerializeField] private float detectRange = 12f;       // 玩家进入这个范围：idle -> walk
    [SerializeField] private float loseRange = 16f;         // 玩家超出这个范围：walk / attack -> idle
    [SerializeField] private float attackRange = 2f;        // 玩家进入这个范围：walk -> attack
    [SerializeField] private float attackExitRange = 2.5f;  // 玩家超出这个范围：attack -> walk（比 attackRange 大一点，避免在边界来回抖）

    [Header("攻击")]
    [SerializeField] private float attackDamage = 10f;     // 一次挥爪对玩家造成的伤害

    [Header("远程攻击（可选）")]
    [SerializeField] private bool rangedAttack = false;      // 勾上后攻击时发射子弹（默认关：僵尸是近战挥爪）
    [SerializeField] private GameObject bulletPrefab;        // 子弹预制体
    [SerializeField] private Transform firePoint;            // 枪口位置（留空则从胸口发出）
    [SerializeField] private float bulletDamage = 10f;       // 子弹伤害

    // 血量组件：僵尸预制体上挂着 Health，子弹打中时扣的就是它
    private Health health;

    // 叫声组件（可选）：挂了 ZombieAudio 才会播 idle / battle / dying 音效
    private ZombieAudio zombieAudio;

    [Header("玩家")]
    [SerializeField] private Transform player;              // 留空则在开始游戏时自动按 Tag 查找
    [SerializeField] private string playerTag = "Player";

    [Header("寻路")]
    [SerializeField] private bool useNavigation = true;      // 勾上 = 用 NavMeshAgent 绕开障碍物找路（关掉就是以前的直线走）
    [SerializeField] private float repathInterval = 0.25f;   // 每隔多久重新算一次到玩家的路
    [SerializeField] private float repathDistance = 1f;      // 目标点移动超过这个距离就立刻重算
    [Header("其他")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool logStateChanges = true;    // 状态切换时在 Console 打印日志

    // 动画参数名，与 Assets/Zombie/Anim/Animator/Zombie_AC.controller 里的 Bool 参数一致
    public const string AnimWalk = "Walk";
    public const string AnimAttack = "Attack";
    public const string AnimDeath = "Death";

    public Animator anim { get; private set; }
    public Rigidbody rb { get; private set; }
    public NavMeshAgent agent { get; private set; }

    private Vector3 lastPathTarget;
    private float nextRepathTime;

    public StateMachine stateMachine { get; private set; }
    public ZombieIdleState idleState { get; private set; }
    public ZombieWalkState walkState { get; private set; }
    public ZombieAttackState attackState { get; private set; }
    public ZombieDeathState deathState { get; private set; }

    /// <summary>是否已经死亡：死亡后不再接受任何状态切换。</summary>
    public bool isDead { get; private set; }

    /// <summary>死亡瞬间触发一次（对象池 ZombiePool 订阅它，等尸体停留时间到了把僵尸收回去）。</summary>
    public event Action<Zombie> Died;

    // ---------- 场景内的僵尸计数（刷怪点 ZombieSpawnPoint 读的就是这两个数） ----------

    /// <summary>当前场景里还活着的僵尸数量。僵尸一死就减一，刷怪点靠它决定要不要补怪。</summary>
    public static int AliveCount { get; private set; }

    /// <summary>当前场景里处于激活状态的僵尸总数（含还没被池子回收的尸体，不含池里待用的）。</summary>
    public static int ActiveCount { get; private set; }

    private bool countedAsActive;
    private bool countedAsAlive;

    /// <summary>进入播放模式时把计数清零，避免关掉 Domain Reload 之后数字残留。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSceneCounters()
    {
        AliveCount = 0;
        ActiveCount = 0;
    }

    /// <summary>僵尸被激活（从对象池取出，或场景里的僵尸开局加载）时计入数量。</summary>
    private void OnEnable()
    {
        ActiveCount++;
        AliveCount++;
        countedAsActive = true;
        countedAsAlive = true;
    }

    /// <summary>僵尸被关掉（被对象池回收，或直接销毁）时不再占名额。</summary>
    private void OnDisable()
    {
        UnregisterFromScene();
    }

    /// <summary>死亡后就不再算「活着的僵尸」（尸体还留在场景里时 ActiveCount 不变）。</summary>
    private void UnregisterAlive()
    {
        if (!countedAsAlive) return;

        countedAsAlive = false;
        AliveCount = Mathf.Max(0, AliveCount - 1);
    }

    private void UnregisterFromScene()
    {
        UnregisterAlive();

        if (!countedAsActive) return;

        countedAsActive = false;
        ActiveCount = Mathf.Max(0, ActiveCount - 1);
    }

    public float MoveSpeed => moveSpeed;
    public float TurnSpeed => turnSpeed;
    public float DetectRange => detectRange;
    public float LoseRange => loseRange;
    public float AttackRange => attackRange;
    public float AttackExitRange => attackExitRange;
    public Transform Player => player;
    public bool HasPlayer => player != null;

    /// <summary>
    /// 僵尸到玩家的水平距离。没有玩家（或玩家被销毁）时返回正无穷，等于“玩家太远”。
    /// </summary>
    public float DistanceToPlayer
    {
        get
        {
            if (player == null) return float.PositiveInfinity;

            Vector3 self = transform.position;
            Vector3 target = player.position;
            self.y = 0f;
            target.y = 0f;
            return Vector3.Distance(self, target);
        }
    }

    private void Awake()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();

        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            // Agent 只负责「算路」，真正的位移还是刚体（见 MoveTowards）：
            // 这样重力、碰撞、子弹判定全都和以前一样，也不会出现 Agent 和物理互相拉扯的抖动
            agent.updatePosition = false;
            agent.updateRotation = false;   // 转身由 Zombie.FacePosition / FaceDirection 控制，模型朝向参数才有效
            agent.autoBraking = false;      // 追人不减速，什么时候停由攻击范围决定
            agent.stoppingDistance = 0f;
            agent.speed = moveSpeed;
        }

        health = GetComponent<Health>();
        zombieAudio = GetComponent<ZombieAudio>();   // 状态机初始化前先拿到，第一声 Idle 叫声才不会漏
        if (health != null) health.Died += Die;   // 血量归零 -> 进入死亡状态

        anim.applyRootMotion = false;   // 位移交给脚本控制，否则根运动会把僵尸自己带跑

        // 只允许绕 Y 轴旋转，僵尸被撞到时不会翻倒
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // 状态机只依赖僵尸自己，所以放在 Awake 里建好：对象池可能在任意时刻把僵尸取出来用
        stateMachine = new StateMachine();
        // Idle 是 Animator 的默认状态，没有对应的 Bool 参数，所以这里传空字符串
        idleState = new ZombieIdleState(stateMachine, this, string.Empty);
        walkState = new ZombieWalkState(stateMachine, this, AnimWalk);
        attackState = new ZombieAttackState(stateMachine, this, AnimAttack);
        deathState = new ZombieDeathState(stateMachine, this, AnimDeath);

        // 僵尸出生时在原地待机
        stateMachine.Initialize(idleState);
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= Die;

        UnregisterFromScene();   // 保险：没走 OnDisable 就被销毁时，计数也要减掉
    }

    private void Start()
    {
        if (player == null) player = FindPlayer();
    }

    private void Update()
    {
        if (stateMachine == null || stateMachine.currentState == null) return;
        stateMachine.currentState.Update();
    }

    private void FixedUpdate()
    {
        if (stateMachine == null || stateMachine.currentState == null) return;
        stateMachine.currentState.FixedUpdate();
    }

    // ---------- 给各个状态调用的工具方法 ----------

    /// <summary>能不能真寻路：勾了开关、挂了 Agent、而且 Agent 已经在 NavMesh 上。</summary>
    public bool CanNavigate => useNavigation && agent != null && agent.enabled && agent.isOnNavMesh;

    /// <summary>
    /// 朝目标点移动：能寻路就让 Agent 算一条绕开障碍物的路、沿着路走；
    /// 没 NavMesh（或关掉寻路）就退回原来的直线移动。
    /// 位移仍然写进刚体速度，重力、碰撞、子弹判定都和以前一样。
    /// </summary>
    public void MoveTowards(Vector3 targetPosition, float speed)
    {
        if (CanNavigate)
        {
            // 玩家一直在动，但没必要每帧都重算整条路：隔一段时间/目标跑远了才重算
            if (Time.time >= nextRepathTime ||
                (targetPosition - lastPathTarget).sqrMagnitude > repathDistance * repathDistance)
            {
                agent.SetDestination(targetPosition);
                lastPathTarget = targetPosition;
                nextRepathTime = Time.time + Mathf.Max(0.05f, repathInterval);
            }

            agent.nextPosition = transform.position;               // updatePosition = false，Agent 的位置得手动跟住刚体
            ApplyHorizontalVelocity(agent.desiredVelocity, speed);  // 沿着算出来的路走
            return;
        }

        // 没有 NavMesh 时的退路：直接朝目标走直线
        ApplyHorizontalVelocity(targetPosition - transform.position, speed);
    }

    /// <summary>停止水平移动，只保留竖直方向速度，顺便把当前的路清掉（下次追人重新算）。</summary>
    public void StopMoving()
    {
        // 只在真的有路要清的时候才调用：攻击状态每物理帧都会走到这里
        if (CanNavigate && agent.hasPath) agent.ResetPath();

        nextRepathTime = 0f;

        Vector3 velocity = rb.velocity;
        velocity.x = 0f;
        velocity.z = 0f;
        rb.velocity = velocity;
    }

    /// <summary>把方向转成水平速度写进刚体（长度统一按 speed，竖直方向速度保留，僵尸仍受重力）。</summary>
    private void ApplyHorizontalVelocity(Vector3 direction, float speed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            Vector3 stopped = rb.velocity;
            stopped.x = 0f;
            stopped.z = 0f;
            rb.velocity = stopped;
            return;
        }

        Vector3 velocity = direction.normalized * speed;
        velocity.y = rb.velocity.y;
        rb.velocity = velocity;
    }

    /// <summary>在水平面上转向某个点（不会低头抬头）。</summary>
    public void FacePosition(Vector3 position)
    {
        FaceDirection(position - transform.position);
    }

    /// <summary>在水平面上转向某个方向（不会低头抬头）。</summary>
    public void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, modelYawOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 走路时的转身：优先朝「实际走的方向」（绕障碍物时会拐弯，不会横着平移）；
    /// 没速度（比如已经走到路径终点）再朝向玩家。
    /// </summary>
    public void FaceMoveDirectionOrPlayer()
    {
        if (CanNavigate)
        {
            Vector3 velocity = agent.desiredVelocity;
            velocity.y = 0f;

            if (velocity.sqrMagnitude > 0.01f)
            {
                FaceDirection(velocity);
                return;
            }
        }

        if (HasPlayer) FacePosition(player.position);
    }

    // ---------- 对外接口 ----------

    /// <summary>切换状态。死亡后不再接受切换。</summary>
    public void ChangeState(ZombieState newState)
    {
        if (isDead) return;
        stateMachine.ChangeState(newState);
    }

    /// <summary>进入死亡状态。子弹打中僵尸、扣完血时就会走到这里。</summary>
    public void Die()
    {
        if (isDead) return;

        isDead = true;
        UnregisterAlive();                      // 死了就不再占刷怪点的名额
        stateMachine.ChangeState(deathState);   // 这里直接走状态机，绕开 ChangeState 里的死亡拦截

        Died?.Invoke(this);                     // 通知对象池：尸体停留时间到了之后把我收回去
    }

    // ---------- 对象池复用 ----------

    /// <summary>
    /// 从对象池取出来时调用：把上一轮死亡留下的状态全部复位，让僵尸能重新用。
    /// （死亡状态把刚体改成了 Kinematic、关掉了碰撞体，Animator 上也留着死亡参数，这里都要还原）
    /// </summary>
    public void ResetForReuse()
    {
        isDead = false;

        if (health != null) health.ResetHealth();

        // 清掉 Animator 上的参数和残留动画，否则复用出来的一瞬间还在播死亡动画
        anim.Rebind();
        anim.SetBool(AnimWalk, false);
        anim.SetBool(AnimAttack, false);
        anim.SetBool(AnimDeath, false);
        anim.Update(0f);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        Collider body = GetComponent<Collider>();
        if (body != null) body.enabled = true;

        // 把上一轮留下的路径和重算计时清掉，复用之后再重新追击
        if (CanNavigate) agent.ResetPath();
        nextRepathTime = 0f;
        lastPathTarget = transform.position;

        stateMachine.ChangeState(idleState);   // 复用出来的僵尸先站在原地待机
    }

    /// <summary>把僵尸摆到指定位置（对象池取出来之后由池子调用）。</summary>
    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        // 有 NavMesh 的话顺手吸到最近的可行走面上，避免刷在墙里 / 台阶里出不来
        if (agent != null && agent.enabled &&
            NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            position = hit.position;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (rb != null)
        {
            rb.position = position;
            rb.rotation = rotation;
        }

        if (agent == null || !agent.enabled) return;

        // Agent 的位置也要跟上，不然第一帧它会按旧位置算路
        agent.nextPosition = position;
        agent.Warp(position);
        nextRepathTime = 0f;
        lastPathTarget = position;
    }

    /// <summary>攻击命中回调：攻击动画打到人的那一刻触发，玩家血量系统做好后在这里扣血。</summary>
    public void AttackHit()
    {
        // 玩家已经跑开的话，这一爪打空
        if (isDead || player == null) return;
        if (DistanceToPlayer > attackExitRange) return;

        if (rangedAttack)
        {
            FireBullet();
            return;
        }

        Health target = player.GetComponentInParent<Health>();
        if (target == null || target.IsDead) return;

        target.TakeDamage(attackDamage, gameObject);
    }

    /// <summary>远程攻击：朝玩家胸口射一发子弹（子弹自己会去判定命中）。</summary>
    private void FireBullet()
    {
        if (bulletPrefab == null) return;

        Transform muzzle = firePoint != null ? firePoint : transform;
        Vector3 aimPoint = player.position + Vector3.up * 1f;   // 瞄玩家胸口
        Vector3 direction = (aimPoint - muzzle.position).normalized;

        GameObject bulletObject = Instantiate(bulletPrefab, muzzle.position, Quaternion.LookRotation(direction));
        Bullet bullet = bulletObject.GetComponent<Bullet>();
        if (bullet != null) bullet.Shoot(direction, gameObject, bulletDamage);
    }

    /// <summary>Animation Event 回调：动画播到关键帧时通知当前状态。</summary>
    public void AnimationFinishTrigger()
    {
        if (stateMachine == null || stateMachine.currentState == null) return;
        stateMachine.currentState.AnimationFinishTrigger();
    }

    /// <summary>状态切换日志，方便在 Console 里确认状态机有没有按预期工作。</summary>
    public void LogState(string stateName)
    {
        if (logStateChanges) Debug.Log($"[Zombie] {name} -> {stateName}", this);
    }

    /// <summary>切换状态时通知叫声组件：Idle 放 idle 叫声、追人放 battle 叫声、死亡放 dying 叫声。</summary>
    public void NotifyStateEntered(ZombieState state)
    {
        if (zombieAudio != null) zombieAudio.OnStateEntered(state);
    }

    private Transform FindPlayer()
    {
        GameObject playerObject = null;

        try
        {
            playerObject = GameObject.FindGameObjectWithTag(playerTag);
        }
        catch (UnityException)
        {
            // 标签不存在时 Unity 会抛异常，这里退回按组件查找
        }

        if (playerObject == null)
        {
            PlayerControll playerControll = FindObjectOfType<PlayerControll>();
            if (playerControll != null) playerObject = playerControll.gameObject;
        }

        if (playerObject == null)
            Debug.LogWarning($"[Zombie] 没有找到玩家（Tag = {playerTag}），僵尸会一直待在 Idle 状态。", this);

        return playerObject != null ? playerObject.transform : null;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);     // 黄：发现玩家的范围
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);    // 蓝：丢失玩家的范围
        Gizmos.DrawWireSphere(transform.position, loseRange);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);   // 红：攻击范围
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, player.position);
        }
    }
}
