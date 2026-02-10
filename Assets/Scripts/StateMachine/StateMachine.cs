using System.Collections.Generic;
using UnityEngine;

namespace StateMachine
{
    /// <summary>
    /// Generic state machine for Player, Enemy, Boss. Manages state lifecycle and transitions.
    /// Call Initialize() to start, Update() each frame.
    /// </summary>
    public class StateMachine<TContext> where TContext : class
    {
        private TContext _context;
        private IState<TContext> _currentState;
        private readonly List<StateTransition<TContext>> _transitions = new();
        private bool _initialized;

        /// <summary>Current active state.</summary>
        public IState<TContext> CurrentState => _currentState;

        /// <summary>
        /// Initialize the state machine with context and starting state.
        /// </summary>
        public void Initialize(TContext context, IState<TContext> initialState)
        {
            _context = context;
            _currentState = initialState;
            _currentState?.Enter(_context);
            _initialized = true;
        }

        /// <summary>
        /// Add a transition: when in fromState and condition(context) is true, switch to toState.
        /// Transitions are checked in order; the first matching transition is used.
        /// </summary>
        public void AddTransition(IState<TContext> fromState, IState<TContext> toState, System.Func<TContext, bool> condition)
        {
            _transitions.Add(new StateTransition<TContext>(fromState, toState, condition));
        }

        /// <summary>
        /// Add a transition from any state to the target state (e.g. for global transitions like Death).
        /// Pass null for fromState to mean "from any state".
        /// </summary>
        public void AddGlobalTransition(IState<TContext> toState, System.Func<TContext, bool> condition)
        {
            _transitions.Add(new StateTransition<TContext>(null, toState, condition));
        }

        /// <summary>
        /// Manually switch to a new state. Call this from within states or external logic.
        /// </summary>
        public void SetState(IState<TContext> newState)
        {
            if (newState == null) return;
            _currentState?.Exit(_context);
            _currentState = newState;
            _currentState.Enter(_context);
        }

        /// <summary>
        /// Call every frame (e.g. from Update()). Runs current state logic and checks transitions.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_initialized || _context == null) return;

            // Check transitions: first global, then from current state
            foreach (var t in _transitions)
            {
                if (t.Condition == null || !t.Condition(_context)) continue;
                if (t.FromState != null && t.FromState != _currentState) continue;
                SetState(t.ToState);
                return;
            }

            _currentState?.Update(_context, deltaTime);
        }
    }
}
