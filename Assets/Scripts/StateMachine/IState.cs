namespace StateMachine
{
    /// <summary>
    /// Base interface for all states used by Player, Enemy, and Boss.
    /// TContext: the owner (e.g. PlayerController, EnemyController) that the state operates on.
    /// </summary>
    public interface IState<TContext> where TContext : class
    {
        void Enter(TContext context);
        void Update(TContext context, float deltaTime);
        void Exit(TContext context);
    }
}
