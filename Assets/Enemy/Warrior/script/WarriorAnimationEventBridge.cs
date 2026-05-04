using UnityEngine;

/// <summary>
/// 动画事件桥接。挂在 Animator 所在的 GameObject 上。
/// 若 Animator 在子物体，Animation Event 会调用该子物体上的脚本，需通过此桥接转发到 WarriorController。
/// 若 Animator 与 WarriorController 在同一物体，可不挂此脚本，直接在 Event 中调用 WarriorController 的方法。
/// </summary>
public class WarriorAnimationEventBridge : MonoBehaviour
{
    private WarriorController _warrior;

    private void Awake()
    {
        _warrior = GetComponentInParent<WarriorController>();
    }

    /// <summary>动画事件：挥砍帧。参数：Int 1 或 2</summary>
    public void OnAttackHit(int index)
    {
        _warrior?.OnAttackHit(index);
    }

    /// <summary>动画事件：第一段攻击动画最后一帧</summary>
    public void OnAttack1End()
    {
        _warrior?.OnAttack1End();
    }

    /// <summary>动画事件：第二段攻击动画最后一帧</summary>
    public void OnAttack2End()
    {
        _warrior?.OnAttack2End();
    }

    /// <summary>动画事件：连击后摇 Recovery 提前结束（可省略，仅用 recoveryStateDuration）</summary>
    public void OnWarriorRecoveryEnd()
    {
        _warrior?.OnWarriorRecoveryEnd();
    }
}
