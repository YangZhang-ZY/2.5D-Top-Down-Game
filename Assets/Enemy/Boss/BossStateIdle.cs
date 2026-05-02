using StateMachine;

public sealed class BossStateIdle : StateBase<BossController>
{
    public override void Enter(BossController ctx)
    {
        ctx.StopMovingPublic();
        ctx.ClearAttackIntent();
    }

    public override void Update(BossController ctx, float dt)
    {
        ctx.StopMovingPublic();
    }
}
