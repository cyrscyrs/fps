using UnityEngine;

/// <summary>
/// Walk：朝玩家走过去，边走边转向玩家。
/// 玩家进入攻击范围 -> Attack；玩家跑出 loseRange -> Idle（太远了就不追了）。
/// </summary>
public class ZombieWalkState : ZombieState
{
    public ZombieWalkState(StateMachine _stateMachine, Zombie _zombie, string _animBoolName)
        : base(_stateMachine, _zombie, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        if (zombie.HasPlayer) zombie.FacePosition(zombie.Player.position);
    }

    public override void Update()
    {
        base.Update();

        if (zombie.isDead) return;

        // 没有玩家时 DistanceToPlayer 是正无穷，下面的判断会走回 Idle
        float distance = zombie.DistanceToPlayer;

        if (distance <= zombie.AttackRange)
        {
            zombie.ChangeState(zombie.attackState);
            return;
        }

        if (distance > zombie.LoseRange)
        {
            zombie.ChangeState(zombie.idleState);
            return;
        }

        // 转身优先朝实际走的方向：绕障碍物时僵尸会跟着路拐弯，而不是横着平移
        zombie.FaceMoveDirectionOrPlayer();
    }

    public override void FixedUpdate()
    {
        if (zombie.isDead || !zombie.HasPlayer) return;

        zombie.MoveTowards(zombie.Player.position, zombie.MoveSpeed);
    }
}
