using UnityEngine;

/// <summary>
/// 玩家死亡处理：血量为 0 时停止操作、解锁鼠标，可选延迟后在出生点满血复活。
/// 挂在玩家根物体上，和 Health 一起使用。
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerHealth : MonoBehaviour
{
    [Header("重生")]
    [SerializeField] private float respawnDelay = 3f;    // 0 = 不自动重生，只躺在原地
    [SerializeField] private Transform respawnPoint;     // 留空 = 用游戏开始时的位置和朝向

    [Header("死亡时")]
    [SerializeField] private bool unlockCursor = true;

    private Health health;
    private PlayerControll controll;
    private Weapon weapon;
    private Rigidbody rb;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool isDead;

    private void Awake()
    {
        health = GetComponent<Health>();
        controll = GetComponent<PlayerControll>();
        weapon = GetComponent<Weapon>();
        rb = GetComponent<Rigidbody>();

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        health.Died += OnDied;
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
    }

    private void OnDied()
    {
        if (isDead) return;
        isDead = true;

        // 死亡后先不能动、不能开枪
        if (controll != null) controll.enabled = false;
        if (weapon != null) weapon.enabled = false;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (unlockCursor) Cursor.lockState = CursorLockMode.None;

        Debug.Log("[PlayerHealth] 玩家死亡", this);

        if (respawnDelay > 0f) Invoke(nameof(Respawn), respawnDelay);
    }

    /// <summary>在出生点满血复活（也可以外部直接调用，做成复活点/存档用）。</summary>
    public void Respawn()
    {
        Vector3 position = respawnPoint != null ? respawnPoint.position : spawnPosition;
        Quaternion rotation = respawnPoint != null ? respawnPoint.rotation : spawnRotation;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
        }

        transform.SetPositionAndRotation(position, rotation);

        health.ResetHealth();
        isDead = false;

        if (controll != null) controll.enabled = true;
        if (weapon != null) weapon.enabled = true;
        if (unlockCursor) Cursor.lockState = CursorLockMode.Locked;

        Debug.Log("[PlayerHealth] 玩家重生", this);
    }
}
