using StateMachine;
using UnityEngine;

public sealed class BossStateMove : StateBase<BossController>
{
    public override void Update(BossController ctx, float dt)
    {
        ctx.MoveTowardsCurrentTargetPublic();
        var t = ctx.GetMoveTarget();
        if (t != null)
        {
            Vector2 d = (Vector2)t.position - (Vector2)ctx.transform.position;
            ctx.UpdateFacingTowards(d);
        }
    }
}
