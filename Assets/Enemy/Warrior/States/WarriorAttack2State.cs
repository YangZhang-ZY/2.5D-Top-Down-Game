using StateMachine;
using UnityEngine;

/// <summary>
/// Warrior 第二段攻击状态。设置 Animator Attack2=true，由动画事件 OnAttack2End 结束。
/// </summary>
public class WarriorAttack2State : StateBase<WarriorController>
{
    public override void Enter(WarriorController ctx)
    {
        ctx.attack2Finished = false;
        ctx.SetAttack1(false);
        ctx.SetAttack2(true);
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
