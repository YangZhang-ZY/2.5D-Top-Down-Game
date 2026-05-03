using StateMachine;
using UnityEngine;

/// <summary>
/// 消耗背包物品并回血：持续时间内锁输入；仅在完整读完条退出时扣物品并治疗（被受击等打断则不消耗）。
/// </summary>
public class PlayerHealState : StateBase<PlayerController>
{
    public override void Enter(PlayerController ctx)
    {
        ctx.healFinished = false;
        ctx.healChannelCompleted = false;
        ctx.healStateTimer = ctx.healChannelDuration;

        PlayerInputBlocker.Request(ctx.HealInputBlockerKey);
        ctx.rb.linearVelocity = Vector2.zero;

        if (ctx.animator != null)
        {
            if (!string.IsNullOrEmpty(ctx.healAnimBoolParam))
                ctx.animator.SetBool(ctx.healAnimBoolParam, true);
        }
    }

    public override void Update(PlayerController ctx, float dt)
    {
        ctx.healStateTimer -= dt;
        if (ctx.healStateTimer <= 0f)
        {
            ctx.healChannelCompleted = true;
            ctx.healFinished = true;
        }
    }

    public override void Exit(PlayerController ctx)
    {
        PlayerInputBlocker.Release(ctx.HealInputBlockerKey);

        if (ctx.healChannelCompleted)
        {
            if (ctx.healConsumableItem != null && ctx.healConsumableItem.IsValid)
            {
                var inv = ctx.GetComponent<Inventory>();
                if (inv != null)
                    inv.RemoveItem(ctx.healConsumableItem, ctx.healItemConsumeCount);
            }

            var hp = ctx.GetComponent<Health>();
            if (hp != null)
                hp.Heal(ctx.healHpAmount);
        }

        ctx.healChannelCompleted = false;

        if (ctx.animator != null && !string.IsNullOrEmpty(ctx.healAnimBoolParam))
            ctx.animator.SetBool(ctx.healAnimBoolParam, false);
    }
}
