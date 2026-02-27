using StateMachine;
using UnityEngine;

/// <summary>
/// 玩家冲刺状态：朝输入方向高速位移，可打断攻击。冲刺不刷新攻击顺序。
/// 依赖 PlayerController：Moveinput、LastMoveDiraction、rb、dashFinished、SetDashCooldown、IsInvincible
/// </summary>
public class PlayerDashState : StateBase<PlayerController>
{
    private float _durationTimer;

    public override void Enter(PlayerController ctx)
    {
        ctx.dashFinished = false;
        ctx.IsInvincible = true;

        Vector2 dir = ctx.Moveinput.sqrMagnitude > 0.01f
            ? ctx.Moveinput.normalized
            : ctx.LastMoveDiraction.normalized;

        float speed = ctx.dashDistance / Mathf.Max(0.01f, ctx.dashDuration);
        ctx.rb.linearVelocity = dir * speed;

        _durationTimer = ctx.dashDuration;

        if (ctx.animator != null)
        {
            ctx.animator.SetBool("IsDashing", true);
            ctx.animator.SetFloat("MoveX", dir.x);
            ctx.animator.SetFloat("MoveY", dir.y);
        }
    }

    public override void Update(PlayerController ctx, float dt)
    {
        _durationTimer -= dt;
        if (_durationTimer <= 0f)
            ctx.dashFinished = true;
    }

    public override void Exit(PlayerController ctx)
    {
        ctx.rb.linearVelocity = Vector2.zero;
        ctx.SetDashCooldown(ctx.dashCooldown);
        ctx.IsInvincible = false;

        if (ctx.animator != null)
            ctx.animator.SetBool("IsDashing", false);
    }
}
