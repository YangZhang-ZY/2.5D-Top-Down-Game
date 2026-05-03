using StateMachine;
using UnityEngine;

/// <summary>
/// 玩家：按住格挡键开始，持续 <see cref="PlayerController.blockDuration"/>；锁输入；
/// Animator <see cref="PlayerController.blockShieldAnimBoolParam"/> 护盾表现。
/// </summary>
public class PlayerBlockState : StateBase<PlayerController>
{
    public override void Enter(PlayerController ctx)
    {
        ctx.blockFinished = false;
        ctx.blockStateTimer = ctx.blockDuration;
        ctx.rb.linearVelocity = Vector2.zero;

        PlayerInputBlocker.Request(ctx.BlockInputBlockerKey);

        if (ctx.animator != null && !string.IsNullOrEmpty(ctx.blockShieldAnimBoolParam))
            ctx.animator.SetBool(ctx.blockShieldAnimBoolParam, true);
    }

    public override void Update(PlayerController ctx, float dt)
    {
        ctx.blockStateTimer -= dt;
        if (ctx.blockStateTimer <= 0f)
            ctx.blockFinished = true;
    }

    public override void Exit(PlayerController ctx)
    {
        PlayerInputBlocker.Release(ctx.BlockInputBlockerKey);

        if (ctx.animator != null && !string.IsNullOrEmpty(ctx.blockShieldAnimBoolParam))
            ctx.animator.SetBool(ctx.blockShieldAnimBoolParam, false);

        ctx.SetBlockCooldown(ctx.blockCooldown);
    }
}
