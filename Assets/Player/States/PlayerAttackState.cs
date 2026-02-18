using StateMachine;
using UnityEngine;

public class PlayerAttackState : StateBase<PlayerController>
{
    public override void Enter(PlayerController ctx)
    {
        ctx.rb.linearVelocity = Vector2.zero;
        // TODO: 播放攻击动画
        // TODO: 启动攻击冷却
        // TODO: 触发攻击判定（或通过动画事件）
    }

    public override void Update(PlayerController ctx, float dt)
    {
        // TODO: 攻击进行中（可做计时、或等动画事件）
        // TODO: 攻击结束时设置 ctx.AttackFinished = true
    }

    public override void Exit(PlayerController ctx)
    {
        // TODO: 停止攻击相关逻辑
    }
}
