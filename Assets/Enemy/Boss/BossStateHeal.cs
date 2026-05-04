using StateMachine;

public sealed class BossStateHeal : StateBase<BossController>
{
    float _t;

    public override void Enter(BossController ctx)
    {
        ctx.ClearAttackIntent();
        ctx.SetHealAnim(true);
        ctx.BeginHealPhase();
        _t = ctx.healStateDuration;
        ctx.StopMovingPublic();
    }

    public override void Update(BossController ctx, float dt)
    {
        ctx.StopMovingPublic();
        _t -= dt;
        if (_t <= 0f)
            ctx.healFinished = true;
    }

    public override void Exit(BossController ctx)
    {
        ctx.EndHealStateCleanup();
        ctx.SetHealAnim(false);
    }
}
