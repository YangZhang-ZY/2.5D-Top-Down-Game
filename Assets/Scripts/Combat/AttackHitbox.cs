using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Melee hit volume on a child of the attacker. Needs a Collider2D (forced Is Trigger in Awake).
/// Each damageable root is damaged at most once per activation.
///
/// Enable 后同一帧若已与目标重叠，Unity 有时不会发 OnTriggerEnter2D；因此在开启碰撞体后会立刻用 OverlapCollider 扫一遍。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AttackHitbox : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Damage source for DamageInfo.source (usually the player).")]
    public GameObject owner;

    [Header("Targeting (optional)")]
    [Tooltip("勾选后：只伤害 Layer 在下列 Mask 内的碰撞体（用于区分 Default/Enemy/自定义 Building 等）。不勾选则与原先一致，按 IDamageable 命中。")]
    [SerializeField] bool useHitLayerMask;

    [SerializeField] LayerMask hitLayers;

    readonly HashSet<GameObject> _hitThisAttack = new HashSet<GameObject>();
    readonly List<Collider2D> _overlapBuffer = new List<Collider2D>(24);

    Collider2D _collider;

    void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;
        _collider.enabled = false;
    }

    /// <summary>Enables the collider and stores damage parameters for this swing.</summary>
    public void EnableHitbox(float damage, Vector2 direction, float offset, float knockbackForce = 0f)
    {
        _hitThisAttack.Clear();
        _currentDamage = damage;
        _currentKnockbackDir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.down;
        _currentKnockbackForce = knockbackForce;

        Vector3 worldOffset = new Vector3(_currentKnockbackDir.x, _currentKnockbackDir.y, 0f) * offset;
        if (transform.parent != null)
            transform.localPosition = transform.parent.InverseTransformVector(worldOffset);
        else
            transform.localPosition = worldOffset;

        _collider.enabled = true;
        ApplyOverlappingHits();
    }

    public void DisableHitbox()
    {
        _collider.enabled = false;
        _hitThisAttack.Clear();
    }

    float _currentDamage;
    Vector2 _currentKnockbackDir;
    float _currentKnockbackForce;

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    void ApplyOverlappingHits()
    {
        if (!_collider.enabled) return;
        _overlapBuffer.Clear();
        var filter = ContactFilter2D.noFilter;
        int n = _collider.Overlap(filter, _overlapBuffer);
        for (int i = 0; i < n; i++)
            TryHit(_overlapBuffer[i]);
    }

    void TryHit(Collider2D other)
    {
        if (!_collider.enabled || other == null) return;
        if (useHitLayerMask && (hitLayers.value & (1 << other.gameObject.layer)) == 0)
            return;
        if (owner != null)
        {
            GameObject otherGo = other.gameObject;
            if (otherGo == owner || other.transform.IsChildOf(owner.transform))
                return;
        }

        var damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        if (damageable == null) return;
        var host = damageable as MonoBehaviour;
        GameObject dedupeKey = host != null ? host.gameObject : other.gameObject;
        if (_hitThisAttack.Contains(dedupeKey)) return;

        var info = _currentKnockbackForce > 0.01f
            ? DamageInfo.CreateWithKnockback(_currentDamage, owner, _currentKnockbackDir, _currentKnockbackForce)
            : DamageInfo.Create(_currentDamage, owner);

        if (damageable.TakeDamage(info))
            _hitThisAttack.Add(dedupeKey);
    }
}
