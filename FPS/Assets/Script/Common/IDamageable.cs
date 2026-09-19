using UnityEngine;

/// <summary>
/// 「可以被打」的统一接口：子弹、僵尸的爪子都只认这个接口，
/// 以后加新的敌人 / 可破坏物，只要挂上 Health 就能被打。
/// </summary>
public interface IDamageable
{
    /// <summary>血量是否已经空了。</summary>
    bool IsDead { get; }

    /// <summary>扣血。attacker 是攻击者（子弹就是开枪的人），可以为空。</summary>
    void TakeDamage(float damage, GameObject attacker = null);
}
