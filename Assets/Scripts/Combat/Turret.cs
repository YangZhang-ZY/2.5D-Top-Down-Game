using UnityEngine;

/// <summary>
/// Basic turret: finds the nearest valid target with <see cref="Physics2D.OverlapCircleAll"/> and fires <see cref="TurretProjectile"/> from <see cref="firePoint"/>.
/// Usually placed on the turret prefab root from a build slot (same layer as sprites).
/// </summary>
public class Turret : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Detection radius in world units.")]
    [SerializeField] private float range = 5f;

    [Tooltip("Only these layers are considered. Prefer a dedicated Enemy layer.")]
    [SerializeField] private LayerMask enemyLayers = -1;

    [Tooltip("If true, the collider hit must have a parent with Health and must not be dead.")]
    [SerializeField] private bool requireHealth = true;

    [Header("Firing")]
    [SerializeField] private float damage = 10f;

    [SerializeField] private float shotsPerSecond = 2f;

    [Tooltip("Prefab needs Rigidbody2D + CircleCollider2D + TurretProjectile.")]
    [SerializeField] private TurretProjectile bulletPrefab;

    [Tooltip("Muzzle transform; defaults to this object if empty.")]
    [SerializeField] private Transform firePoint;

    [SerializeField] private float bulletSpeed = 12f;

    [Header("Aiming")]
    [Tooltip("Added to target world position (XY) before aim direction. Use for 2.5D billboard offset; negative Y often lowers the aim point.")]
    [SerializeField] private Vector2 aimTargetWorldOffset;

    [Header("Visual (optional)")]
    [Tooltip("If set, rotated toward the target each shot (2D Z rotation).")]
    [SerializeField] private Transform aimPivot;

    private float _fireCooldown;
    private bool _basesCached;
    private float _baseDamage;
    private float _baseRange;
    private float _baseShotsPerSecond;
    Health _structureHealth;

    void Awake()
    {
        _structureHealth = GetComponent<Health>();
    }

    /// <summary>Inspector values are treated as level 1 after <see cref="ApplyUpgradeLevel"/> is used.</summary>
    private void CacheBasesIfNeeded()
    {
        if (_basesCached) return;
        _baseDamage = damage;
        _baseRange = range;
        _baseShotsPerSecond = shotsPerSecond;
        _basesCached = true;
    }

    /// <summary>Recompute stats from level-1 base values. Level 1 matches the Inspector.</summary>
    public void ApplyUpgradeLevel(int level, float damageAddPerLevel, float rangeAddPerLevel, float fireRateAddPerLevel)
    {
        CacheBasesIfNeeded();
        int L = Mathf.Max(1, level);
        damage = _baseDamage + damageAddPerLevel * (L - 1);
        range = Mathf.Max(0.1f, _baseRange + rangeAddPerLevel * (L - 1));
        shotsPerSecond = Mathf.Max(0.01f, _baseShotsPerSecond + fireRateAddPerLevel * (L - 1));
    }

    private void Update()
    {
        if (_structureHealth != null && _structureHealth.IsDead)
            return;
        if (bulletPrefab == null) return;

        _fireCooldown -= Time.deltaTime;
        if (_fireCooldown > 0f) return;

        if (!TryGetNearestTarget(out Transform target)) return;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        Vector3 aimPoint = target.position + new Vector3(aimTargetWorldOffset.x, aimTargetWorldOffset.y, 0f);
        Vector2 dir = (Vector2)(aimPoint - origin);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        if (aimPivot != null)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            aimPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        Fire(dir, origin);

        _fireCooldown = shotsPerSecond > 0.01f ? 1f / shotsPerSecond : 0.1f;
    }

    private bool TryGetNearestTarget(out Transform target)
    {
        target = null;
        var hits = Physics2D.OverlapCircleAll(transform.position, range, enemyLayers);
        if (hits == null || hits.Length == 0) return false;

        float bestSq = float.MaxValue;

        foreach (var col in hits)
        {
            if (col == null) continue;

            if (requireHealth)
            {
                var h = col.GetComponentInParent<Health>();
                if (h == null) continue;
                if (h.IsDead) continue;
            }

            float sq = (col.transform.position - transform.position).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                target = col.transform;
            }
        }

        return target != null;
    }

    private void Fire(Vector2 direction, Vector3 spawnPos)
    {
        var go = Instantiate(bulletPrefab.gameObject, spawnPos, Quaternion.identity);
        var proj = go.GetComponent<TurretProjectile>();
        if (proj != null)
            proj.Launch(direction * bulletSpeed, damage, gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
#endif
}
