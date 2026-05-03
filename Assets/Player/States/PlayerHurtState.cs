using StateMachine;
using UnityEngine;

/// <summary>
/// 玩家受击：锁输入、固定击退速度与时长（由 PlayerController 配置）、播放受击动画。
/// </summary>
public class PlayerHurtState : StateBase<PlayerController>
{
    public override void Enter(PlayerController ctx)
    {
        ctx.hurtFinished = false;
        ctx.hurtStateTimer = ctx.hurtStateDuration;
        ctx.ConsumePendingHurtEntry();

        PlayerInputBlocker.Request(ctx.HurtInputBlockerKey);

        Vector2 kb = ctx.QueuedHurtDirection * ctx.hurtKnockbackSpeed;
        ctx.ApplyContactKnockback(kb, ctx.hurtStateDuration);

        if (ctx.animator != null && !string.IsNullOrEmpty(ctx.hurtHitAnimTrigger))
            ctx.animator.SetTrigger(ctx.hurtHitAnimTrigger);
    }

    public override void Update(PlayerController ctx, float dt)
    {
        ctx.hurtStateTimer -= dt;
        if (ctx.hurtStateTimer <= 0f)
            ctx.hurtFinished = true;
    }

    public override void Exit(PlayerController ctx)
    {
        PlayerInputBlocker.Release(ctx.HurtInputBlockerKey);

        if (ctx.RefillPostureAfterHurt)
        {
            var posture = ctx.GetComponent<CombatPosture>();
            if (posture != null)
                posture.RefillPosture();
        }

        ctx.RefillPostureAfterHurt = false;

        ctx.ApplyPostHurtInvincibility();
    }
}
