using UnityEngine;

/// <summary>
/// Death：播放死亡动画，然后停在原地等对象池回收。死亡是终点状态，不会再切回其他状态；
/// 僵尸被池子取出来复用时会通过 Zombie.ResetForReuse() 重新回到 Idle。
/// </summary>
public class ZombieDeathState : ZombieState
{
    public ZombieDeathState(StateMachine _stateMachine, Zombie _zombie, string _animBoolName)
        : base(_stateMachine, _zombie, _animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        zombie.StopMoving();

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;      // 尸体不再被玩家或子弹推着走
        }

        Collider body = zombie.GetComponent<Collider>();
        if (body != null) body.enabled = false;   // 尸体不再挡路、挡子弹

        // 尸体不用自己销毁：死亡时 Zombie.Die() 会通知对象池 ZombiePool，
        // 池子等 corpseLingerTime 秒后把僵尸回收（SetActive(false)），下次刷怪直接复用
    }

    /// <summary>死亡是终点：不关掉 Death 参数，也不再响应动画事件。</summary>
    public override void Exit()
    {
    }
}
