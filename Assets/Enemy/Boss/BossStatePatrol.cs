using StateMachine;

/// <summary>Boss 在巡逻半径内随机选点行走，到达后停顿 <see cref="BossController.patrolPauseAtWaypointDuration"/> 秒再选下一点，直至开战。</summary>
public sealed class BossStatePatrol : StateBase<BossController>
{
    public override void Enter(BossController ctx)
    {
        ctx.ResetPatrolWaitAndPickDestination();
    }

    public override void Update(BossController ctx, float dt)
    {
        ctx.TickPatrolMovement(dt);
    }

    public override void Exit(BossController ctx)
    {
        ctx.StopMovingPublic();
    }
}
