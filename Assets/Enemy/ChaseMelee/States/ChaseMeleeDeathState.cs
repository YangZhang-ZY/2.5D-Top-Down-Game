using StateMachine;
using UnityEngine;

/// <summary>Death: stop, play death anim, disable collider (mirrors WarriorDeathState).</summary>
public class ChaseMeleeDeathState : StateBase<ChaseMeleeEnemyController>
{
    public override void Enter(ChaseMeleeEnemyController ctx)
    {
        ctx.StopMovingPublic();

        if (ctx.Animator != null && !string.IsNullOrEmpty(ctx.animParamIsDead))
            ctx.Animator.SetBool(ctx.animParamIsDead, true);

        var col = ctx.GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;
    }

    public override void Update(ChaseMeleeEnemyController ctx, float dt)
    {
    }
}
