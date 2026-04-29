using StateMachine;
using UnityEngine;

/// <summary>
/// 所有使用 <see cref="StateMachine{TContext}"/> 的敌人共用框架：
/// - <see cref="EnemyBase"/>：移动、仇恨、攻击冷却、受伤、死亡标记等
/// - 本类：统一 Start 里装配状态机、Update 驱动顺序、死亡时切入 Death 状态
///
/// 派生类实现 <see cref="InitializeStateMachine"/>，创建状态与转换，最后调用
/// <see cref="RegisterDeathState"/>；并实现 <see cref="OnEnemyTickBeforeStateMachine"/>（如 AI 决策）。
/// </summary>
public abstract class StatefulEnemyControllerBase<TSelf> : EnemyBase where TSelf : StatefulEnemyControllerBase<TSelf>
{
    protected StateMachine<TSelf> _stateMachine;

    IState<TSelf> _deathState;

    protected override void Start()
    {
        base.Start();
        InitializeStateMachine();
    }

    /// <summary>创建状态实例、配置转换、最后必须 <see cref="RegisterDeathState"/>。</summary>
    protected abstract void InitializeStateMachine();

    /// <summary>在 <see cref="InitializeStateMachine"/> 末尾调用一次。</summary>
    protected void RegisterDeathState(IState<TSelf> deathState)
    {
        _deathState = deathState;
    }

    protected override void OnDeath()
    {
        base.OnDeath();
        if (_stateMachine != null && _deathState != null && _stateMachine.CurrentState != _deathState)
            _stateMachine.SetState(_deathState);
    }

    protected override void Update()
    {
        if (isDead) return;

        TickKnockbackMovementPause(Time.deltaTime);
        TickAttackCooldown();
        OnEnemyExtraTimers(Time.deltaTime);
        UpdateAggroProvoke();
        OnEnemyTickBeforeStateMachine();

        _stateMachine?.Update(Time.deltaTime);
        UpdateAnimatorParameters();
    }

    protected virtual void TickAttackCooldown()
    {
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.deltaTime;
    }

    /// <summary>额外计时（例如 Warrior 格挡冷却）。默认无。</summary>
    protected virtual void OnEnemyExtraTimers(float deltaTime)
    {
    }

    /// <summary>状态机 Tick 之前：如 Warrior 的 TickAIDecision、ChaseMelee 的 TickAttackIntent。</summary>
    protected abstract void OnEnemyTickBeforeStateMachine();

    protected override void UpdateAI()
    {
    }

    protected override void PerformAttack()
    {
    }
}
