using StateMachine;
using UnityEngine;

/// <summary>
/// 二连击结束后：原地后摇，<see cref="WarriorController.animParamRecovery"/> 对应 Animator Bool。
/// 计时结束或动画事件 <see cref="WarriorController.OnWarriorRecoveryEnd"/> 后进入 <see cref="WarriorStayState"/>。
/// </summary>
public sealed class WarriorRecoveryState : StateBase<WarriorController>
{
    float _timer;

    public override void Enter(WarriorController ctx)
    {
        ctx.recoveryFinished = false;
        ctx.StopMovingPublic();
        ctx.SetRecoveryAnim(true);
        _timer = Mathf.Max(0f, ctx.recoveryStateDuration);
        if (_timer <= 0f)
            ctx.recoveryFinished = true;
    }

    public override void Exit(WarriorController ctx)
    {
        ctx.SetRecoveryAnim(false);
    }

    public override void Update(WarriorController ctx, float dt)
    {
        ctx.StopMovingPublic();
        if (ctx.recoveryFinished) return;
        _timer -= dt;
        if (_timer <= 0f)
            ctx.recoveryFinished = true;
    }
}
