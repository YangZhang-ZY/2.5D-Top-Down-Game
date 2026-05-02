using StateMachine;

/// <summary>破防长眩晕：Stunned Bool + 可选首帧 Hit Trigger。</summary>
public sealed class BossStateStun : StateBase<BossController>
{
    float _t;

    public override void Enter(BossController ctx)
    {
        ctx.stunFinished = false;
        ctx.StopMovingPublic();
        ctx.ConsumePendingStun();
        ctx.PlayHitAnimTriggerOnce();
        ctx.SetStunnedAnim(true);
        _t = ctx.stunDuration;
    }

    public override void Exit(BossController ctx)
    {
        ctx.SetStunnedAnim(false);
        if (ctx.Posture != null)
            ctx.Posture.RefillPosture();
    }

    public override void Update(BossController ctx, float dt)
    {
        ctx.StopMovingPublic();
        _t -= dt;
        if (_t <= 0f)
            ctx.stunFinished = true;
    }
}
