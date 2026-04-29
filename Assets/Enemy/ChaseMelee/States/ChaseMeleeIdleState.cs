using StateMachine;

/// <summary>
/// Idle hub (Warrior Stay–like branching, but instant): stand still while <see cref="EnemyBase.GetMoveTarget"/>
/// picks player vs crystal. Transitions choose Chase / Attack; if in attack range on cooldown, remain here.
/// </summary>
public class ChaseMeleeIdleState : StateBase<ChaseMeleeEnemyController>
{
    public override void Enter(ChaseMeleeEnemyController ctx)
    {
        ctx.StopMovingPublic();
    }

    public override void Update(ChaseMeleeEnemyController ctx, float dt)
    {
        ctx.StopMovingPublic();
    }
}
