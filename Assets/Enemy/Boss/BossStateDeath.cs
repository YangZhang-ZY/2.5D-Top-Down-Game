using StateMachine;
using UnityEngine;

public sealed class BossStateDeath : StateBase<BossController>
{
    public override void Enter(BossController ctx)
    {
        ctx.CleanupBossHeal();
        ctx.CancelPendingHitboxDisableRoutine();
        ctx.StopMovingPublic();
        ctx.SetAttack1Anim(false);
        ctx.SetAttack2Anim(false);
        ctx.SetHealAnim(false);
        ctx.SetRecoveryAnim(false);
        ctx.DisableAttackHitboxSafe();

        if (ctx.Animator != null && !string.IsNullOrEmpty(ctx.animParamIsDead))
            ctx.Animator.SetBool(ctx.animParamIsDead, true);

        var col = ctx.GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;
    }

    public override void Update(BossController ctx, float dt)
    {
    }
}
