using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Melee hit volume on a child of the attacker. Needs a trigger Collider2D.
/// Each target is damaged at most once per activation.
///
/// Setup:
/// 1. Child under the player named AttackHitbox (or similar).
/// 2. CircleCollider2D or similar, Is Trigger, sized for the attack arc.
/// 3. This component on the same GameObject.
/// 4. Reference from the player controller as the active hitbox.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AttackHitbox : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Damage source for DamageInfo.source (usually the player).")]
    public GameObject owner;

    private readonly HashSet<GameObject> _hitThisAttack = new HashSet<GameObject>();

    private Collider2D _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;
        _collider.enabled = false;
    }

    /// <summary>Enables the collider and stores damage parameters for this swing.</summary>
    /// <param name="damage">Damage amount.</param>
    /// <param name="direction">Facing for placement / knockback.</param>
    /// <param name="offset">Local offset along direction from the owner.</param>
    /// <param name="knockbackForce">0 for none.</param>
    public void EnableHitbox(float damage, Vector2 direction, float offset, float knockbackForce = 0f)
    {
        _hitThisAttack.Clear();
        _currentDamage = damage;
        _currentKnockbackDir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.down;
        _currentKnockbackForce = knockbackForce;

        transform.localPosition = (Vector3)(_currentKnockbackDir * offset);

        _collider.enabled = true;
    }

    /// <summary>Disables the collider until the next EnableHitbox.</summary>
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
