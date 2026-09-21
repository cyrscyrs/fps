using System;
using UnityEngine;

/// <summary>
/// 血量组件：玩家和僵尸都用它。
/// 受到伤害 / 死亡时会抛出事件，死亡后做什么交给各自的脚本处理
/// （僵尸 -> ZombieDeathState 倒地；玩家 -> PlayerHealth 停止操作并重生）。
/// </summary>
[DisallowMultipleComponent]
public class Health : MonoBehaviour, IDamageable
{
    [Header("血量")]
    [SerializeField] private float maxHealth = 300f;

    [Header("死亡处理")]
    [SerializeField] private bool destroyOnDeath = false;   // 死后销毁自己（僵尸有自己的死亡状态，所以关闭）
    [SerializeField] private float destroyDelay = 0f;       // destroyOnDeath 打开时，延迟多久销毁

    [Header("调试")]
    [SerializeField] private bool logDamage = false;         // 受击/死亡时在 Console 打印

    /// <summary>受伤时触发，参数：伤害值、攻击者。</summary>
    public event Action<float, GameObject> Damaged;

    /// <summary>生命值降到 0 时触发一次。</summary>
    public event Action Died;

    /// <summary>
    /// 生命值只要发生变化就会触发：受伤、回血、读档设值、重生回满都算。
    /// UI（血条）订阅这个就能一直显示对的血量，不会出现「数值改了但界面还停在旧值」。
    /// </summary>
    public event Action Changed;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }

    /// <summary>剩余血量比例 0~1，UI 血条可以直接用。</summary>
    public float HealthPercent => maxHealth <= 0f ? 0f : Mathf.Clamp01(CurrentHealth / maxHealth);

    public bool IsDead => CurrentHealth <= 0f;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    /// <summary>扣血（子弹、近战攻击都调这里）。血量为 0 时触发死亡。</summary>
    public void TakeDamage(float damage, GameObject attacker = null)
    {
        if (IsDead || damage <= 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);

        if (logDamage)
            Debug.Log($"[Health] {name} 受到 {damage:0.#} 点伤害，剩余 {CurrentHealth:0.#}/{maxHealth:0.#}", this);

        Damaged?.Invoke(damage, attacker);
        Changed?.Invoke();

        if (IsDead) Die();
    }

    /// <summary>回血，不会超过最大血量。</summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        Changed?.Invoke();
    }

    /// <summary>回满血（重生用）。注意：这个方法不会重新触发死亡/复活事件。</summary>
    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        Changed?.Invoke();   // 重生回满血，血条也要跟着刷新
    }

    /// <summary>直接把血量设成某个值（读档用）。不会触发受击/死亡事件，值会被限制在 0~最大血量。</summary>
    public void SetHealth(float value)
    {
        CurrentHealth = Mathf.Clamp(value, 0f, maxHealth);
        Changed?.Invoke();   // 读档设值也要通知 UI，否则血条会停在旧数字上
    }

    /// <summary>改最大血量。refill 为 true 时顺手回满，常用于初始化不同血量的敌人。</summary>
    public void SetMaxHealth(float value, bool refill = false)
    {
        maxHealth = Mathf.Max(1f, value);
        CurrentHealth = refill ? maxHealth : Mathf.Min(CurrentHealth, maxHealth);
        Changed?.Invoke();
    }

    private void Die()
    {
        CurrentHealth = 0f;
        Changed?.Invoke();

        if (logDamage) Debug.Log($"[Health] {name} 死亡", this);

        Died?.Invoke();

        if (destroyOnDeath)
        {
            if (destroyDelay > 0f) Destroy(gameObject, destroyDelay);
            else Destroy(gameObject);
        }
    }
}
