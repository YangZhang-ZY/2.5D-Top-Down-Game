using StateMachine;
using UnityEngine;

/// <summary>
/// 玩家死亡：锁输入、停移动、关闭攻击盒，Animator IsDead（参数名由 PlayerController 配置）。
/// </summary>
public class PlayerDeathState : StateBase<PlayerController>
{
    public override void Enter(PlayerController ctx)
    {
        PlayerInputBlocker.Request(ctx.DeathInputBlockerKey);
        ctx.rb.linearVelocity = Vector2.zero;
        ctx.attackHitbox?.DisableHitbox();

        if (ctx.animator != null && !string.IsNullOrEmpty(ctx.deathAnimBoolParam))
            ctx.animator.SetBool(ctx.deathAnimBoolParam, true);
    }

    public override void Update(PlayerController ctx, float dt)
    {
    }

    public override void Exit(PlayerController ctx)
    {
    }
}
