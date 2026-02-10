using System;

namespace StateMachine
{
    /// <summary>
    /// Defines a transition from one state to another when a condition is met.
    /// Condition is a function that receives the context and returns true when the transition should fire.
    /// </summary>
    public class StateTransition<TContext> where TContext : class
    {
        public IState<TContext> FromState { get; }
        public IState<TContext> ToState { get; }
        public Func<TContext, bool> Condition { get; }

        public StateTransition(IState<TContext> from, IState<TContext> to, Func<TContext, bool> condition)
        {
            FromState = from;
            ToState = to;
            Condition = condition;
        }
    }
}
