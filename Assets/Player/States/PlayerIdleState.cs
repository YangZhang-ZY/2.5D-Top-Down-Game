
using StateMachine;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerIdleState : StateBase<PlayerController>
{
    public override void Enter(PlayerController ctx)
    {
        ctx.rb.linearVelocity = Vector2.zero;
        ctx.animator.SetBool("Is Moving",false);
        ctx.animator.SetFloat("MoveX", ctx.LastMoveDiraction.x);
        ctx.animator.SetFloat("MoveY", ctx.LastMoveDiraction.y);
    }

    public override void Update(PlayerController ctx, float deltaTime)
    {
        
    }

    public override void Exit(PlayerController ctx)
    {

    }
}
