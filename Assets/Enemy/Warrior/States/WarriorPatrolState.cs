using StateMachine;
using UnityEngine;

/// <summary>
/// Warrior 巡逻状态。
/// 在巡逻范围内随机选点移动，到达后随机停留 2~3 秒，再前往下一个点。
/// </summary>
public class WarriorPatrolState : StateBase<WarriorController>
{
    private Vector2 _currentTarget;
    private float _waitTimer;
    private const float ArriveThreshold = 0.3f;

    public override void Enter(WarriorController ctx)
    {
        _currentTarget = ctx.GetRandomPatrolPoint();
        _waitTimer = 0f;
    }

    public override void Update(WarriorController ctx, float dt)
    {
        Vector2 pos = ctx.transform.position;
        float sqrDist = (pos - _currentTarget).sqrMagnitude;

        if (sqrDist <= ArriveThreshold * ArriveThreshold)
        {
            if (_waitTimer <= 0f)
            {
                _waitTimer = Random.Range(ctx.patrolWaitMin, ctx.patrolWaitMax);
                ctx.StopMovingPublic();
            }
        }

        if (_waitTimer > 0f)
        {
            _waitTimer -= dt;
            if (_waitTimer <= 0f)
                _currentTarget = ctx.GetRandomPatrolPoint();
            return;
        }

        ctx.MoveTowardsPointPublic(_currentTarget);
    }
}
