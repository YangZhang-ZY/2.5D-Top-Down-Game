using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 攻击碰撞盒。挂在玩家子物体上，该子物体需有 Collider2D（isTrigger）。
/// 攻击时启用，碰撞到 IDamageable 时造成伤害。
/// 每次攻击只对同一对象造成一次伤害（避免重复扣血）。
///
/// 使用步骤：
/// 1. 在 Player 下创建子物体，命名为 AttackHitbox
/// 2. 给子物体添加 Circle Collider 2D，勾选 Is Trigger，调整 Radius
/// 3. 给子物体添加本脚本
/// 4. 在 PlayerController 的 attackHitbox 槽位拖入该子物体
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AttackHitbox : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("伤害来源（通常是玩家），用于 DamageInfo.source")]
    public GameObject owner;

    /// <summary>本帧/本次攻击已命中的对象，避免重复伤害</summary>
    private readonly HashSet<GameObject> _hitThisAttack = new HashSet<GameObject>();

    private Collider2D _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;
        _collider.enabled = false;
    }

    /// <summary>
    /// 开启攻击碰撞盒，开始检测伤害。
    /// </summary>
    /// <param name="damage">本次攻击伤害值</param>
    /// <param name="direction">攻击方向（用于 Hitbox 位置和击退）</param>
    /// <param name="offset">Hitbox 相对玩家的偏移距离，每段攻击可不同</param>
    /// <param name="knockbackForce">击退力度，0 表示无</param>
    public void EnableHitbox(float damage, Vector2 direction, float offset, float knockbackForce = 0f)
    {
        _hitThisAttack.Clear();
        _currentDamage = damage;
        _currentKnockbackDir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.down;
        _currentKnockbackForce = knockbackForce;

        transform.localPosition = (Vector3)(_currentKnockbackDir * offset);

        _collider.enabled = true;
    }

    /// <summary>
    /// 关闭攻击碰撞盒
    /// </summary>
    public void DisableHitbox()
    {
        _collider.enabled = false;
        _hitThisAttack.Clear();
    }

    private float _currentDamage;
    private Vector2 _currentKnockbackDir;
    private float _currentKnockbackForce;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_collider.enabled) return;

        var go = other.gameObject;
        if (_hitThisAttack.Contains(go)) return;

        var damageable = go.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = go.GetComponentInParent<IDamageable>();

        if (damageable == null) return;

        var info = _currentKnockbackForce > 0.01f
            ? DamageInfo.CreateWithKnockback(_currentDamage, owner, _currentKnockbackDir, _currentKnockbackForce)
            : DamageInfo.Create(_currentDamage, owner);

        if (damageable.TakeDamage(info))
            _hitThisAttack.Add(go);
    }
}
