using UnityEngine;

/// <summary>
/// 挂在 **Animator 所在物体** 上（与 ChaseMelee 相同）。
/// Animation Event 调用此处，再转发到父级 <see cref="BossController"/>。
/// </summary>
public class BossAnimationEventBridge : MonoBehaviour
{
    BossController _boss;

    void Awake()
    {
        _boss = GetComponentInParent<BossController>();
    }

    /// <summary>攻击动画结束（ clip 末尾），必须调用，否则会用 Safety Timeout 强退。</summary>
    public void OnBossAttackEnd()
    {
        _boss?.OnBossAttackEnd();
    }

    /// <summary>攻击1 第一段伤害帧。</summary>
    public void OnBossAttack1Hit()
    {
        _boss?.OnBossAttack1Hit();
    }

    /// <summary>攻击1 第二段伤害帧（同一段动画上再挂一个 Event，仍用同一个 AttackHitbox）。</summary>
    public void OnBossAttack1Hit2()
    {
        _boss?.OnBossAttack1Hit2();
    }

    /// <summary>攻击2 第一段伤害帧。</summary>
    public void OnBossAttack2Hit()
    {
        _boss?.OnBossAttack2Hit();
    }

    /// <summary>攻击2 第二段伤害帧（可选）。</summary>
    public void OnBossAttack2Hit2()
    {
        _boss?.OnBossAttack2Hit2();
    }

    /// <summary>后摇动画结束（可选，用于提前切回 Idle）。</summary>
    public void OnBossRecoveryEnd()
    {
        _boss?.OnBossRecoveryEnd();
    }
}
