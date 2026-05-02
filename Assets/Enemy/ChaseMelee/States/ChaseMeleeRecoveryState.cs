using StateMachine;
using UnityEngine;

/// <summary>
/// Post-attack recovery: stationary, <see cref="ChaseMeleeEnemyController.animParamRecovery"/> bool for Animator.
/// After <see cref="ChaseMeleeEnemyController.recoveryStateDuration"/>, returns to <see cref="ChaseMeleeIdleState"/>.
/// </summary>
public class ChaseMeleeRecoveryState : StateBase<ChaseMeleeEnemyController>
{
    float _timer;
    bool _refillPostureOnExit;

    public override void Enter(ChaseMeleeEnemyController ctx)
    {
        ctx.recoveryFinished = false;
        ctx.attackFinished = false;
        ctx.StopMovingPublic();
        ctx.SetRecoveryAnim(true);
        _timer = Mathf.Max(0f, ctx.recoveryStateDuration);
        _refillPostureOnExit = ctx.NextRecoveryShouldRefillPosture;
        if (_refillPostureOnExit)
            ctx.NextRecoveryShouldRefillPosture = false;
        if (_timer <= 0f)
            ctx.recoveryFinished = true;
    }

    public override void Exit(ChaseMeleeEnemyController ctx)
    {
        ctx.SetRecoveryAnim(false);
        if (_refillPostureOnExit)
            ctx.Posture?.RefillPosture();
    }

    public override void Update(ChaseMeleeEnemyController ctx, float dt)
    {
        ctx.StopMovingPublic();
        if (ctx.recoveryFinished) return;
        _timer -= dt;
        if (_timer <= 0f)
            ctx.recoveryFinished = true;
    }
}
