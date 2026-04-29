using StateMachine;

/// <summary>
/// Chase / fly: move toward <see cref="EnemyBase.GetMoveTarget"/> (crystal or player per aggro).
/// In attack range but on cooldown → transition to Idle (stand still). Out of range → move.
/// </summary>
public class ChaseMeleeChaseState : StateBase<ChaseMeleeEnemyController>
{
    public override void Update(ChaseMeleeEnemyController ctx, float dt)
    {
        if (ctx.IsCurrentTargetInAttackRange())
        {
            ctx.StopMovingPublic();
            return;
        }

        ctx.MoveTowardsCurrentTargetPublic();
    }
}
