using UnityEngine;

/// <summary>
/// Projectile fired by a turret. Expects Rigidbody2D (dynamic, no gravity) and a trigger Collider2D.
/// Damages <see cref="IDamageable"/> targets and destroys itself on hit.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class TurretProjectile : MonoBehaviour
{
    [HideInInspector] public float damage;
    [HideInInspector] public GameObject damageSource;

    [Tooltip("Lifetime in seconds; destroys the projectile afterwards.")]
    [SerializeField] private float lifetime = 4f;

    [Header("Targeting (optional)")]
    [Tooltip("勾选后：只伤害 Layer 在下列 Mask 内的碰撞体（与 AttackHitbox 相同；不勾选则按 IDamageable 命中，不按层过滤）。")]
    [SerializeField] bool useHitLayerMask;

    [SerializeField] LayerMask hitLayers;

    [Header("Facing (arrow sprites)")]
    [Tooltip("Aligns rotation to velocity. Offset in degrees if the art faces +X (0) or +Y (-90), etc.")]
    [SerializeField] private float rotationOffsetDegrees;

    private Rigidbody2D _rb;
    private float _timer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.bodyType = RigidbodyType2D.Dynamic;

        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    /// <summary>Called by Turret right after Instantiate.</summary>
    public void Launch(Vector2 worldVelocity, float dmg, GameObject source)
    {
        damage = dmg;
        damageSource = source;
        _timer = 0f;

        if (worldVelocity.sqrMagnitude > 0.0001f)
        {
            Vector2 d = worldVelocity.normalized;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + rotationOffsetDegrees;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (_rb != null)
            _rb.linearVelocity = worldVelocity;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= lifetime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        if (damageSource != null &&
            (other.gameObject == damageSource || other.transform.IsChildOf(damageSource.transform)))
            return;

        if (useHitLayerMask && (hitLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        var dmg = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        if (dmg == null) return;

        var src = damageSource != null ? damageSource : gameObject;
        dmg.TakeDamage(DamageInfo.Create(damage, src));
        Destroy(gameObject);
    }
}
