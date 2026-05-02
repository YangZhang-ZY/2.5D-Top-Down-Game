using StateMachine;

/// <summary>攻击后摇：原地不动，播放 Recovery 动画（Bool），可用动画事件提前结束。</summary>
public sealed class BossStateRecovery : StateBase<BossController>
{
    float _t;

    public override void Enter(BossController ctx)
    {
        ctx.recoveryFinished = false;
        ctx.StopMovingPublic();
        ctx.SetRecoveryAnim(true);
        _t = ctx.recoveryStateDuration;
    }

    public override void Update(BossController ctx, float dt)
    {
        ctx.StopMovingPublic();
        _t -= dt;
        if (_t <= 0f)
            ctx.recoveryFinished = true;
    }

    public override void Exit(BossController ctx)
    {
        ctx.SetRecoveryAnim(false);
    }
}
