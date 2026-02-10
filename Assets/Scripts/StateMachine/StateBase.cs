namespace StateMachine
{
    /// <summary>
    /// Abstract base class for states. Provides default empty implementations
    /// so subclasses only override what they need. Use for Player, Enemy, Boss states.
    /// </summary>
    public abstract class StateBase<TContext> : IState<TContext> where TContext : class
    {
        public virtual void Enter(TContext context) { }
        public abstract void Update(TContext context, float deltaTime);
        public virtual void Exit(TContext context) { }
    }
}
