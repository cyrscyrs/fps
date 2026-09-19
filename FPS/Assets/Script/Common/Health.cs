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

        if (IsDead) Die();
    }

    /// <summary>回血，不会超过最大血量。</summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    /// <summary>回满血（重生用）。注意：这个方法不会重新触发死亡/复活事件。</summary>
    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
    }

    /// <summary>改最大血量。refill 为 true 时顺手回满，常用于初始化不同血量的敌人。</summary>
    public void SetMaxHealth(float value, bool refill = false)
    {
        maxHealth = Mathf.Max(1f, value);
        CurrentHealth = refill ? maxHealth : Mathf.Min(CurrentHealth, maxHealth);
    }

    private void Die()
    {
        CurrentHealth = 0f;

        if (logDamage) Debug.Log($"[Health] {name} 死亡", this);

        Died?.Invoke();

        if (destroyOnDeath)
        {
            if (destroyDelay > 0f) Destroy(gameObject, destroyDelay);
            else Destroy(gameObject);
        }
    }
}
