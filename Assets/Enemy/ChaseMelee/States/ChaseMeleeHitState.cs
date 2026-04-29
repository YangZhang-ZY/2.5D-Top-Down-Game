using StateMachine;
using UnityEngine;

/// <summary>Hit stun: no movement until timer ends. Hit trigger is fired on Enter.</summary>
public class ChaseMeleeHitState : StateBase<ChaseMeleeEnemyController>
{
    public override void Enter(ChaseMeleeEnemyController ctx)
    {
        ctx.hitRecoveryFinished = false;
        ctx.StopMovingPublic();
        ctx.ConsumePendingHit();
        ctx.PlayHitAnimTrigger();
    }

    public override void Update(ChaseMeleeEnemyController ctx, float dt)
    {
        ctx.hitStunTimer -= dt;
        if (ctx.hitStunTimer <= 0f)
            ctx.hitRecoveryFinished = true;
    }
}
