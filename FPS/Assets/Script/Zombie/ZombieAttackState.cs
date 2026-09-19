using UnityEngine;

/// <summary>
/// Attack：站住不动、正面朝向玩家挥爪。
/// 玩家离开 attackExitRange 就回到 Walk；跑得更远（超过 loseRange）则回到 Idle。
/// </summary>
public class ZombieAttackState : ZombieState
{
    private bool hitThisSwing;   // 一次挥击只触发一次命中，避免同一次动画里连续判定

    public ZombieAttackState(StateMachine _stateMachine, Zombie _zombie, string _animBoolName)
        : base(_stateMachine, _zombie, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        hitThisSwing = false;
        zombie.StopMoving();
    }

    public override void Update()
    {
        base.Update();

        if (zombie.isDead) return;

        float distance = zombie.DistanceToPlayer;

        if (distance > zombie.AttackExitRange)
        {
            // 没有玩家时 distance 是正无穷，同样会走到这里
            zombie.ChangeState(distance <= zombie.LoseRange ? zombie.walkState : zombie.idleState);
            return;
        }

        if (zombie.HasPlayer) zombie.FacePosition(zombie.Player.position);

        CheckAttackHit();
    }

    public override void FixedUpdate()
    {
        // 攻击的时候站住不动
        zombie.StopMoving();
    }

    /// <summary>
    /// 攻击动画播到一半时判定一次命中。攻击动画设置成循环播放，
    /// 所以只要玩家不离开攻击范围，僵尸就会一直挥爪。
    /// </summary>
    private void CheckAttackHit()
    {
        AnimatorStateInfo info = zombie.anim.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(Zombie.AnimAttack)) return;

        float progress = Mathf.Repeat(info.normalizedTime, 1f);   // 0~1 表示这一次挥击播到哪了

        if (progress >= 0.5f && !hitThisSwing)
        {
            hitThisSwing = true;
            zombie.AttackHit();
        }
        else if (progress < 0.1f)
        {
            hitThisSwing = false;   // 新的一次挥击开始了
        }
    }
}
