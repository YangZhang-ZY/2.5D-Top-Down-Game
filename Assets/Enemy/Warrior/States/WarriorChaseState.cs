using StateMachine;
using UnityEngine;

/// <summary>
/// Warrior 追击状态。
/// 当玩家在追击范围内但不在攻击范围内时，Warrior 朝玩家移动。
/// 当玩家进入攻击范围且可攻击时，会设置 wantToAttack 或 wantToBlock，
/// 下一帧状态机将转换到 Attack 或 Block。
/// </summary>
public class WarriorChaseState : StateBase<WarriorController>
{
    /// <summary>
    /// 进入追击时：清除上一状态的标志（可选）。
    /// </summary>
    public override void Enter(WarriorController ctx)
    {
        ctx.wantToAttack = false;
        ctx.wantToBlock = false;
    }

    /// <summary>
    /// 追击时每帧：朝玩家移动。攻击/格挡决策由 WarriorController.TickAIDecision 统一处理。
    /// </summary>
    public override void Update(WarriorController ctx, float dt)
    {
        ctx.MoveTowardsCurrentTargetPublic();
    }
}
