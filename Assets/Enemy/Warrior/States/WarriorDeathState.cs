using StateMachine;
using UnityEngine;

/// <summary>
/// Warrior 死亡状态。
/// 当 isDead 为 true 时，状态机会从任意状态转换到此状态。
/// 停止移动，播放死亡动画，关闭碰撞。具体销毁逻辑可由 EnemyBase.OnDeath 或外部处理。
/// </summary>
public class WarriorDeathState : StateBase<WarriorController>
{
    /// <summary>
    /// 进入死亡时：停止移动，播放死亡动画，关闭碰撞体。
    /// </summary>
    public override void Enter(WarriorController ctx)
    {
        ctx.StopMovingPublic();

        if (ctx.Animator != null && !string.IsNullOrEmpty(ctx.animParamIsDead))
            ctx.Animator.SetBool(ctx.animParamIsDead, true);

        var col = ctx.GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;
    }

    /// <summary>
    /// 死亡时每帧：什么都不做，保持死亡状态。
    /// </summary>
    public override void Update(WarriorController ctx, float dt)
    {
    }
}
