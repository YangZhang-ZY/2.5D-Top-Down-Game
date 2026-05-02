using StateMachine;

/// <summary>削韧短受击：Trigger Hit，不保持 Stunned Loop。</summary>
public sealed class BossStateHit : StateBase<BossController>
{
    float _t;

    public override void Enter(BossController ctx)
    {
        ctx.hitReactionFinished = false;
        ctx.StopMovingPublic();
        ctx.ConsumePendingHit();
        ctx.SetStunnedAnim(false);
        ctx.PlayHitAnimTriggerOnce();
        _t = ctx.chipHitDuration;
    }

    public override void Update(BossController ctx, float dt)
    {
        ctx.StopMovingPublic();
        _t -= dt;
        if (_t <= 0f)
            ctx.hitReactionFinished = true;
    }
}
