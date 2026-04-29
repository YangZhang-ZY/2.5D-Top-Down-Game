using StateMachine;
using UnityEngine;

/// <summary>
/// 玩家攻击状态：攻击顺序制，无连击窗口。每段攻击单独按键，按 attackSequenceIndex 执行。
/// 依赖 PlayerController：attackSteps、attackSequenceIndex、attackFinished、LastMoveDiraction、animator、rb
/// </summary>
public class PlayerAttackState : StateBase<PlayerController>
{
    private float _attackDurationTimer;

    public override void Enter(PlayerController ctx)
    {
        ctx.attackFinished = false;
        ctx.rb.linearVelocity = Vector2.zero;
        ctx.StartAttackCooldown();

        StartStep(ctx, ctx.attackSequenceIndex);
    }

    public override void Update(PlayerController ctx, float dt)
    {
        _attackDurationTimer -= dt;
        if (_attackDurationTimer <= 0f)
            ctx.attackFinished = true;
    }

    /// <summary>
    /// 开始指定段数的攻击，设置计时器、位移、Animator 参数
    /// </summary>
    private void StartStep(PlayerController ctx, int step)
    {
        if (ctx.attackSteps == null || step < 1 || step > ctx.attackSteps.Length)
            return;

        var config = ctx.attackSteps[step - 1];
        _attackDurationTimer = config.duration;

        if (config.lungeDistance > 0.01f)
        {
            Vector2 dir = ctx.LastMoveDiraction.sqrMagnitude > 0.01f
                ? ctx.LastMoveDiraction.normalized
                : Vector2.down;
            ctx.rb.position += dir * config.lungeDistance;
        }

        if (ctx.animator != null)
        {
            ctx.animator.SetBool("IsAttacking", true);
            ctx.animator.SetInteger("ComboStep", step);
            ctx.animator.SetFloat("MoveX", ctx.LastMoveDiraction.x);
            ctx.animator.SetFloat("MoveY", ctx.LastMoveDiraction.y);
        }

        if (ctx.attackHitbox != null)
        {
            Vector2 dir = ctx.LastMoveDiraction.sqrMagnitude > 0.01f ? ctx.LastMoveDiraction.normalized : Vector2.down;
            float offset = config.GetHitboxOffsetForDirection(dir);
            ctx.attackHitbox.EnableHitbox(config.attackDamage, dir, offset, config.knockbackForce);
        }
    }

    public override void Exit(PlayerController ctx)
    {
        ctx.rb.linearVelocity = Vector2.zero;

        if (ctx.attackSteps != null && ctx.attackSequenceIndex >= 1 && ctx.attackSequenceIndex <= ctx.attackSteps.Length)
        {
            var config = ctx.attackSteps[ctx.attackSequenceIndex - 1];
            ctx.SetAttackRecovery(config.recoveryTime);

            int maxSteps = ctx.attackSteps.Length;
            ctx.attackSequenceIndex = (ctx.attackSequenceIndex % maxSteps) + 1;
        }

        if (ctx.animator != null)
        {
            ctx.animator.SetBool("IsAttacking", false);
            ctx.animator.SetInteger("ComboStep", 0);
        }

        ctx.attackHitbox?.DisableHitbox();
    }
}
