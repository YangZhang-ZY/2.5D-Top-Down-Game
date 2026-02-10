using UnityEngine;

namespace StateMachine
{
    /// <summary>
    /// MonoBehaviour helper to drive a StateMachine each frame.
    /// Attach to your Player/Enemy/Boss GameObject and assign the state machine.
    /// Example: GetComponent&lt;PlayerController&gt;() provides context; its state machine uses this runner.
    /// </summary>
    public abstract class StateMachineRunner<TContext> : MonoBehaviour where TContext : class
    {
        protected abstract StateMachine<TContext> GetStateMachine();

        protected virtual void Update()
        {
            GetStateMachine()?.Update(Time.deltaTime);
        }
    }
}
