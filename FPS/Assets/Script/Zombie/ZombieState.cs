using UnityEngine;

/// <summary>
/// 僵尸状态基类：进入 / 退出时开关 Animator 上的 Bool 参数，并记录状态时间。
/// Idle / Walk / Attack / Death 四个状态都继承它。
/// </summary>
public class ZombieState
{
    protected StateMachine stateMachine;
    protected Zombie zombie;

    protected Rigidbody rb;

    private readonly string animBoolName;
    private readonly bool hasAnimBool;

    protected float stateTimer;     // 进入这个状态之后经过的时间
    protected bool triggerCalled;   // 动画事件是否已经回调过

    public ZombieState(StateMachine _stateMachine, Zombie _zombie, string _animBoolName)
    {
        this.stateMachine = _stateMachine;
        this.zombie = _zombie;
        this.animBoolName = _animBoolName;
        this.hasAnimBool = !string.IsNullOrEmpty(animBoolName);
    }

    public virtual void Enter()
    {
        // Idle 是 Animator 的默认状态，没有对应参数，所以传空字符串时跳过
        if (hasAnimBool) zombie.anim.SetBool(animBoolName, true);

        rb = zombie.rb;
        stateTimer = 0f;
        triggerCalled = false;

        zombie.LogState(GetType().Name);
    }

    public virtual void Update()
    {
        stateTimer += Time.deltaTime;
    }

    /// <summary>物理相关的移动放在这里，避免每帧 Update 里直接改刚体速度。</summary>
    public virtual void FixedUpdate()
    {
    }

    public virtual void Exit()
    {
        if (hasAnimBool) zombie.anim.SetBool(animBoolName, false);
    }

    /// <summary>Animation Event 回调，用来标记动画播完了（例如攻击动画、死亡动画）。</summary>
    public virtual void AnimationFinishTrigger()
    {
        triggerCalled = true;
    }
}
