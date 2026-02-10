=== State Machine Base - Usage Guide ===

Files:
- IState<TContext>      : Interface for all states
- StateBase<TContext>   : Abstract base, override OnEnter/OnUpdate/OnExit
- StateMachine<TContext>: Runs states, handles transitions
- StateTransition       : Condition + target state
- StateMachineRunner    : Optional MonoBehaviour to drive Update (or call Update manually)

Basic usage (e.g. in PlayerController):

1. Define context: PlayerController (or EnemyController, BossController)
2. Create states: MovementState, AttackState, HitState, etc. inheriting StateBase<PlayerController>
3. Create state machine: _sm = new StateMachine<PlayerController>()
4. Initialize: _sm.Initialize(this, movementState)
5. Add transitions:
   _sm.AddTransition(movementState, attackState, ctx => ctx.WantsToAttack && ctx.CanAttack);
   _sm.AddGlobalTransition(deathState, ctx => ctx.Health <= 0);
6. Each frame: _sm.Update(Time.deltaTime)

State example:
  public class IdleState : StateBase<PlayerController>
  {
    public override void OnUpdate(PlayerController ctx, float dt)
    {
      // optional: check internal transition
      if (ctx.SomeCondition) ctx.RequestStateChange(attackState);
    }
  }

For manual transitions from inside a state: store a ref to the StateMachine in context,
or use an event/callback. The state machine's SetState() can be called from context.
