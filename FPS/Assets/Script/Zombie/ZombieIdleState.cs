using UnityEngine;

/// <summary>
/// Idle：站在原地待机（Animator 的默认状态）。
/// 玩家走进 detectRange 就转去 Walk；如果一开始玩家就已经在攻击范围内，直接进 Attack。
/// </summary>
public class ZombieIdleState : ZombieState
{
    public ZombieIdleState(StateMachine _stateMachine, Zombie _zombie, string _animBoolName)
        : base(_stateMachine, _zombie, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        zombie.StopMoving();
    }

    public override void Update()
    {
        base.Update();

        if (zombie.isDead || !zombie.HasPlayer) return;

        float distance = zombie.DistanceToPlayer;

        if (distance <= zombie.AttackRange)
            zombie.ChangeState(zombie.attackState);
        else if (distance <= zombie.DetectRange)
            zombie.ChangeState(zombie.walkState);
    }
}
