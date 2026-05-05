using StateMachine;
using UnityEngine;

/// <summary>
/// Player attack state: fixed combo sequence, no buffering window. Each step uses a separate button press; runs the step at <see cref="PlayerController.attackSequenceIndex"/>.
/// Depends on PlayerController: attackSteps, attackSequenceIndex, attackFinished, LastMoveDiraction, animator, rb.
/// </summary>
public class PlayerAttackState : StateBase<PlayerController>
{
    private float _attackDurationTimer;

    public override void Enter(PlayerController ctx)
    {
        ctx.attackFinished = false;
        ctx.rb.linearVelocity = Vector2.zero;
        ctx.StartAttackCooldown();

        ctx.TryConsumeChargedExplosionOnAttack();

        StartStep(ctx, ctx.attackSequenceIndex);
    }

    public override void Update(PlayerController ctx, float dt)
    {
        _attackDurationTimer -= dt;
        if (_attackDurationTimer <= 0f)
            ctx.attackFinished = true;
    }

    /// <summary>
    /// Starts the attack step at index <paramref name="step"/>: timer, optional lunge, Animator params, hitbox.
    /// </summary>
    private void StartStep(PlayerController ctx, int step)
    {
        if (ctx.attackSteps == null || step < 1 || step > ctx.attackSteps.Length)
            return;

        var config = ctx.attackSteps[step - 1];
        _attackDurationTimer = config.duration;

        if (config.lungeDistance > 0.01f)
        {
            Vector2 dirInput = ctx.LastMoveDiraction.sqrMagnitude > 0.01f
                ? ctx.LastMoveDiraction.normalized
                : Vector2.down;
            Vector2 dirWorld = ctx.TransformMoveInputToWorldPlanar(dirInput);
            ctx.rb.position += dirWorld * config.lungeDistance;
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
            Vector2 dirInputHit = ctx.LastMoveDiraction.sqrMagnitude > 0.01f ? ctx.LastMoveDiraction.normalized : Vector2.down;
            Vector2 dirWorldHit = ctx.TransformMoveInputToWorldPlanar(dirInputHit);
            float offset = config.GetHitboxOffsetForDirection(dirInputHit);
            ctx.attackHitbox.EnableHitbox(config.attackDamage, dirWorldHit, offset, 0f);
        }

        ctx.PlayAttackSwingSound(step);
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
