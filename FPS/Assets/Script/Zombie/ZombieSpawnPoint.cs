using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 刷怪点：每隔一段时间检查一次「当前场景里的僵尸数量」，
/// 只要没到数量上限，就在刷怪点周围生成新的僵尸；到上限就暂时不刷，
/// 等玩家打死几只、数量掉下来之后，下一轮自动继续补怪。
///
/// 用法：把 ZombieSpawnPoint.prefab 拖进场景摆好位置，
/// 把僵尸预制体拖到 Zombie Prefab 上即可（默认已经填好 ShirtlessZombie_FREE）。
/// 僵尸统一从对象池 ZombiePool 里取（池子空了才会新建），死亡后由池子负责回收。
/// </summary>
[DisallowMultipleComponent]
public class ZombieSpawnPoint : MonoBehaviour
{
    [Header("刷怪设置")]
    [SerializeField] private GameObject zombiePrefab;         // 要生成的僵尸预制体（根物体上要有 Zombie 组件）
    [SerializeField] private float spawnInterval = 5f;         // 每隔多少秒检查一次并尝试刷怪
    [SerializeField] private int spawnCountPerInterval = 1;    // 每次刷新生成几只
    [SerializeField] private float firstSpawnDelay = 0f;       // 游戏开始后先等多久才开始刷怪
    [SerializeField] private int maxSpawnTotal = 0;            // 这个刷怪点总共最多刷几只，0 = 不限

    [Header("僵尸数量限制（当前场景）")]
    [SerializeField] private int maxZombieCount = 10;          // 场景里同时存在的僵尸上限，够了就停刷
    [SerializeField] private bool countDeadZombies = false;    // 勾上 = 尸体也占名额（默认只数活着的）

    [Header("生成位置")]
    [SerializeField] private float spawnRadius = 1.5f;         // 在刷怪点周围多大范围内随机取点
    [SerializeField] private bool snapToGround = false;        // 生成点向下打射线贴到地面（有高低差的地形用）
    [SerializeField] private LayerMask groundLayers = ~0;      // 贴地射线检测哪一层
    [SerializeField] private float groundCheckHeight = 2f;     // 射线起点在生成点上方的距离
    [SerializeField] private float groundCheckDistance = 6f;   // 射线长度
    [SerializeField] private bool snapToNavMesh = true;        // 生成点吸到 NavMesh 上（僵尸得站在烘焙好的可行走面上才会寻路）
    [SerializeField] private float navMeshSampleDistance = 2f; // 吸 NavMesh 的搜索半径
    [SerializeField] private bool randomFacing = true;         // 随机朝向；关掉则用刷怪点自己的朝向
    [SerializeField] private float minPlayerDistance = 0f;     // 离玩家至少多远才刷，0 = 不检查（避免刷在玩家脸上）
    [SerializeField] private string playerTag = "Player";      // 找玩家用的 Tag

    [Header("调试")]
    [SerializeField] private bool logSpawn = false;             // 刷怪时在 Console 打印
    [SerializeField] private bool drawGizmos = true;            // 在 Scene 视图画出刷怪范围

    private Coroutine spawnRoutine;
    private int spawnedTotal;                                   // 这个刷怪点已经刷出来多少只
    private Transform player;
    private bool warnedMissingZombieComponent;

    /// <summary>当前场景里的僵尸数量，按 countDeadZombies 决定算不算尸体。</summary>
    public int CurrentZombieCount => countDeadZombies ? Zombie.ActiveCount : Zombie.AliveCount;

    /// <summary>场景里的对象池（场景里没放的话会自动建一个）。</summary>
    public ZombiePool Pool => ZombiePool.Instance;

    public int MaxZombieCount => maxZombieCount;
    public int SpawnedTotal => spawnedTotal;

    /// <summary>场景里的僵尸已经到上限了。</summary>
    public bool IsSceneFull => CurrentZombieCount >= maxZombieCount;

    /// <summary>这个刷怪点自己刷完了配额。</summary>
    public bool ReachedTotalLimit => maxSpawnTotal > 0 && spawnedTotal >= maxSpawnTotal;

    private void OnEnable()
    {
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (spawnRoutine == null) return;

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    private IEnumerator SpawnLoop()
    {
        if (firstSpawnDelay > 0f) yield return new WaitForSeconds(firstSpawnDelay);

        // 每隔 spawnInterval 秒尝试刷一批，僵尸满了就空转一轮，等下一轮再看
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, spawnInterval));
            SpawnBatch();
        }
    }

    /// <summary>
    /// 立刻尝试刷一批：数量低于上限就按缺多少补多少，已经到上限就什么都不做。
    /// 想用代码手动刷怪（比如开局先来一波）直接调这个。
    /// </summary>
    public void SpawnBatch()
    {
        if (!CanSpawn()) return;

        int room = maxZombieCount - CurrentZombieCount;         // 场景里还能再放几只
        if (room <= 0) return;

        int count = Mathf.Min(spawnCountPerInterval, room);
        if (maxSpawnTotal > 0) count = Mathf.Min(count, maxSpawnTotal - spawnedTotal);
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            if (SpawnOne() != null) spawnedTotal++;
        }
    }

    /// <summary>在刷怪点周围生成一只僵尸，返回生成出来的对象；没找到合适的位置就返回 null。</summary>
    public GameObject SpawnOne()
    {
        if (!CanSpawn()) return null;

        if (!TryGetSpawnPosition(out Vector3 position))
        {
            if (logSpawn) Debug.Log($"[ZombieSpawnPoint] {name} 这次没找到合适的生成点，跳过。", this);
            return null;
        }

        Quaternion rotation = randomFacing
            ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
            : transform.rotation;

        // 从对象池取：池子里有待用的就直接复用，没有才新建（动画 / 刚体 / 血量由池子负责复位）
        Zombie zombie = ZombiePool.Instance.Get(zombiePrefab, position, rotation);
        if (zombie == null) return null;

        zombie.gameObject.name = zombiePrefab.name + "_Spawned";

        if (logSpawn)
            Debug.Log($"[ZombieSpawnPoint] {name} 刷出僵尸 {zombie.name}，" +
                      $"场景僵尸 {Zombie.AliveCount}/{maxZombieCount}", zombie);

        return zombie.gameObject;
    }

    /// <summary>刷怪前的通用检查：预制体有没有、配额和场景上限还有没有余量。</summary>
    private bool CanSpawn()
    {
        if (zombiePrefab == null)
        {
            Debug.LogWarning($"[ZombieSpawnPoint] {name} 没有设置僵尸预制体，不会刷怪。", this);
            return false;
        }

        // 预制体上没有 Zombie 组件的话数量统计不到，会一直刷下去，所以直接拦住
        if (zombiePrefab.GetComponent<Zombie>() == null)
        {
            if (!warnedMissingZombieComponent)
            {
                warnedMissingZombieComponent = true;
                Debug.LogWarning($"[ZombieSpawnPoint] {name} 的僵尸预制体 {zombiePrefab.name} 上没有 Zombie 组件，" +
                                 "数量限制没法统计，已停止刷怪。", this);
            }
            return false;
        }

        return !ReachedTotalLimit;
    }

    /// <summary>在刷怪点周围找一个可以站人的点：随机偏移 -> 可选贴地 -> 离玩家够远。</summary>
    private bool TryGetSpawnPosition(out Vector3 position)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (snapToGround && !TrySnapToGround(ref candidate)) continue;
            if (snapToNavMesh && !TrySnapToNavMesh(ref candidate)) continue;
            if (TooCloseToPlayer(candidate)) continue;

            position = candidate;
            return true;
        }

        // 兜底：随机点都不合适就用刷怪点自己的位置
        position = transform.position;
        if (snapToGround) TrySnapToGround(ref position);
        if (snapToNavMesh) TrySnapToNavMesh(ref position);
        return !TooCloseToPlayer(position);
    }

    /// <summary>从候选点上方打一条向下的射线，落到地面上。</summary>
    private bool TrySnapToGround(ref Vector3 position)
    {
        Vector3 origin = position + Vector3.up * groundCheckHeight;
        float distance = groundCheckHeight + groundCheckDistance;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, groundLayers, QueryTriggerInteraction.Ignore))
        {
            position = hit.point;
            return true;
        }

        return false;
    }

    /// <summary>把候选点吸到最近的 NavMesh 可行走面上；附近没有 NavMesh 就返回 false（换个点再试）。</summary>
    private bool TrySnapToNavMesh(ref Vector3 position)
    {
        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas)) return false;

        position = hit.position;
        return true;
    }

    private bool TooCloseToPlayer(Vector3 position)
    {
        if (minPlayerDistance <= 0f) return false;

        if (player == null) player = FindPlayer();
        if (player == null) return false;                       // 场景里还没有玩家就先不管这条

        Vector3 self = position;
        Vector3 target = player.position;
        self.y = 0f;
        target.y = 0f;
        return Vector3.Distance(self, target) < minPlayerDistance;
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

        return playerObject != null ? playerObject.transform : null;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = new Color(0.6f, 0.1f, 0.9f, 1f);       // 紫：刷怪范围
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
        Gizmos.DrawSphere(transform.position, 0.12f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.5f);

        if (snapToGround)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);  // 绿：贴地射线
            Gizmos.DrawLine(transform.position + Vector3.up * groundCheckHeight,
                            transform.position + Vector3.up * (groundCheckHeight - groundCheckDistance));
        }

        if (minPlayerDistance > 0f)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.4f);      // 橙：离玩家太近不刷
            Gizmos.DrawWireSphere(transform.position, minPlayerDistance);
        }
    }
}
