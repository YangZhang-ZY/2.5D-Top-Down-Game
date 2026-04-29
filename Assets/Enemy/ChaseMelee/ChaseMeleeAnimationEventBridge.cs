using UnityEngine;

/// <summary>
/// 动画事件桥接。挂在 Animator 所在的 GameObject 上。
/// 当 Animator 在子物体时，Animation Event 会调用子物体上的脚本，
/// 通过此桥接将事件转发到父物体的 ChaseMeleeEnemyController。
/// </summary>
public class ChaseMeleeAnimationEventBridge : MonoBehaviour
{
    ChaseMeleeEnemyController _enemy;

    void Awake()
    {
        _enemy = GetComponentInParent<ChaseMeleeEnemyController>();
    }

    /// <summary>动画事件：攻击结束（攻击动画最后一帧）</summary>
    public void OnChaseMeleeAttackEnd()
    {
        _enemy?.OnChaseMeleeAttackEnd();
    }

    /// <summary>动画事件：攻击命中帧（可选）</summary>
    public void OnChaseMeleeAttackHit()
    {
        _enemy?.OnChaseMeleeAttackHit();
    }
}
