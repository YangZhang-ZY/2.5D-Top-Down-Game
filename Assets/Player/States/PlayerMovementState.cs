using StateMachine;
using UnityEngine;

public class PlayerMovementState : StateBase<PlayerController>
{
    public override void Enter(PlayerController ctx) 
    {
        ctx.animator.SetBool("Is Moving", true);
        ctx.animator.SetFloat("MoveX",ctx.Moveinput.normalized.x);
        ctx.animator.SetFloat("MoveY", ctx.Moveinput.normalized.y);
    }

    public override void Update(PlayerController ctx, float dt)
    {
        
        Vector2 MoveInput = ctx.Moveinput;
        if (MoveInput.sqrMagnitude > 0.01f)
        {
            MoveInput = MoveInput.normalized;
        }
        ctx.rb.linearVelocity = MoveInput * ctx.MoveSpeed;
        if (MoveInput.sqrMagnitude > 0.01f)
        {
            ctx.LastMoveDiraction = ctx.Moveinput.normalized;
            ctx.animator.SetFloat("MoveX", MoveInput.x);
            ctx.animator.SetFloat("MoveY", MoveInput.y);
        }
    }

    public override void Exit(PlayerController ctx) 
    {
        ctx.animator.SetBool("Is Moving", false);
    }
}
