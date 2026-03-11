using StateMachine;
using UnityEngine;

/// <summary>
/// Warrior 停留状态。
/// 攻击或格挡结束后，在原地停留 1~2 秒，然后由状态机转换到 Patrol 或 Chase。
/// </summary>
public class WarriorStayState : StateBase<WarriorController>
{
    private float _stayTimer;

    public override void Enter(WarriorController ctx)
    {
        ctx.stayFinished = false;
        ctx.StopMovingPublic();
        _stayTimer = Random.Range(ctx.stayDurationMin, ctx.stayDurationMax);
    }

    public override void Update(WarriorController ctx, float dt)
    {
        _stayTimer -= dt;
        if (_stayTimer <= 0f)
            ctx.stayFinished = true;
    }
}
