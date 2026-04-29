using StateMachine;
using UnityEngine;

/// <summary>
/// Single melee swing: stop moving; Animator bool <see cref="ChaseMeleeEnemyController.animParamAttack"/>;
/// strike frame <see cref="ChaseMeleeEnemyController.OnChaseMeleeAttackHit"/>; end event → <see cref="ChaseMeleeRecoveryState"/>.
/// </summary>
public class ChaseMeleeAttackState : StateBase<ChaseMeleeEnemyController>
{
    public override void Enter(ChaseMeleeEnemyController ctx)
    {
        ctx.attackFinished = false;
        ctx.StopMovingPublic();
        ctx.ResetAttackCooldownPublic();

        ctx.SetAttackAnim(true);
        var tgt = ctx.GetMoveTarget();
        if (tgt != null)
        {
            Vector2 dir = ((Vector2)tgt.position - (Vector2)ctx.transform.position).normalized;
            if (dir.sqrMagnitude > 0.01f)
                ctx.UpdateFacing(dir);
        }
    }

    public override void Exit(ChaseMeleeEnemyController ctx)
    {
        ctx.SetAttackAnim(false);
        ctx.DisableAttackHitboxPublic();
    }

    public override void Update(ChaseMeleeEnemyController ctx, float dt)
    {
        ctx.StopMovingPublic();
    }
}
