using StateMachine;
using UnityEngine;

/// <summary>
/// Warrior 第一段攻击状态。设置 Animator Attack1=true，由动画事件 OnAttack1End 结束。
/// </summary>
public class WarriorAttack1State : StateBase<WarriorController>
{
    public override void Enter(WarriorController ctx)
    {
        ctx.attack1Finished = false;
        ctx.attack2Finished = false;
        ctx.wantToAttack = false;
        ctx.wantToBlock = false;
        ctx.StopMovingPublic();
        ctx.ResetAttackCooldownPublic();

        ctx.SetAttack1(true);
        ctx.SetAttack2(false);
        var tgt = ctx.GetMoveTarget();
        if (tgt != null)
        {
            Vector2 dir = ((Vector2)tgt.position - (Vector2)ctx.transform.position).normalized;
            if (dir.sqrMagnitude > 0.01f)
                ctx.UpdateFacing(dir);
        }
    }

    public override void Update(WarriorController ctx, float dt)
    {
    }
}
