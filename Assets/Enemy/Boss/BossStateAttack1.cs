using StateMachine;

public sealed class BossStateAttack1 : StateBase<BossController>
{
    public override void Enter(BossController ctx)
    {
        ctx.ClearAttackIntent();
        ctx.ResetAttackCooldownPublic();
        ctx.SetAttack1Anim(true);
        ctx.SetAttack2Anim(false);
        ctx.BeginAttackAnimSafetyTimeout(true);
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
        ctx.SetAttack1Anim(false);
        ctx.DisableAttackHitboxSafe();
    }
}
