using StateMachine;
using UnityEngine;

/// <summary>
/// Warrior 格挡状态。
/// 格挡期间免疫伤害，播放格挡动画。持续时间结束后设置 blockFinished = true，
/// 状态机将转换回 Idle 或 Chase，并进入格挡冷却。
/// </summary>
public class WarriorBlockState : StateBase<WarriorController>
{
    private float _blockTimer;

    /// <summary>
    /// 进入格挡时：停止移动，开启格挡（免疫伤害 + 播放动画），开始计时。
    /// </summary>
    public override void Enter(WarriorController ctx)
    {
        ctx.blockFinished = false;
        ctx.wantToAttack = false;
        ctx.wantToBlock = false;
        ctx.StopMovingPublic();
        ctx.SetBlocking(true);
        _blockTimer = ctx.blockDuration;
        var tgt = ctx.GetMoveTarget();
        if (tgt != null)
        {
            Vector2 dir = ((Vector2)tgt.position - (Vector2)ctx.transform.position).normalized;
            ctx.UpdateFacing(dir);
        }
    }

    /// <summary>
    /// 格挡时每帧：倒计时，时间到则结束格挡。
    /// </summary>
    public override void Update(WarriorController ctx, float dt)
    {
        _blockTimer -= dt;
        if (_blockTimer <= 0f)
        {
            ctx.SetBlocking(false);
            ctx.blockFinished = true;
        }
    }

    /// <summary>
    /// 退出格挡时：确保关闭格挡状态（防止异常退出时残留）。
    /// </summary>
    public override void Exit(WarriorController ctx)
    {
        ctx.SetBlocking(false);
    }
}
