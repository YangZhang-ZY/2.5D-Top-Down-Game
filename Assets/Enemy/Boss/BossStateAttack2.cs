using StateMachine;

public sealed class BossStateAttack2 : StateBase<BossController>
{
    public override void Enter(BossController ctx)
    {
        ctx.ClearAttackIntent();
        ctx.ResetAttackCooldownPublic();
        ctx.SetAttack2Anim(true);
        ctx.SetAttack1Anim(false);
        ctx.BeginAttackAnimSafetyTimeout(false);
        ctx.DisableAttackHitboxSafe();
    }

    public override void Update(BossController ctx, float dt)
    {
        ctx.StopMovingPublic();
        ctx.TickAttackAnimSafetyTimeout(dt);
    }

    public override void Exit(BossController ctx)
    {
        ctx.CancelPendingHitboxDisableRoutine();
        ctx.SetAttack2Anim(false);
        ctx.DisableAttackHitboxSafe();
    }
}
