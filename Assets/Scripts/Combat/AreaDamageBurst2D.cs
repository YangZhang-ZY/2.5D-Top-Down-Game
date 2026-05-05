using UnityEngine;

/// <summary>
/// One-shot circular burst: on spawn, damages each <see cref="IDamageable"/> in radius once, then optionally destroys after a delay.
/// For the block-success explosion prefab; can be omitted if the prefab is VFX-only.
/// </summary>
public class AreaDamageBurst2D : MonoBehaviour
{
    [Tooltip("Damage radius in world units.")]
    public float radius = 4f;
    [Tooltip("伤害数值")]
    public float damage = 30f;
    [Tooltip("Destroy entire GameObject after this many seconds (particles can run slightly longer).")]
    public float destroyAfterSeconds = 2f;

    [Tooltip("If true, only damages hierarchies that have the Enemy tag on self-to-root (block explosion enables this automatically).")]
    public bool onlyDamageEnemyTag;

    /// <summary>Set by <see cref="PlayerController"/> after spawn to exclude self-damage.</summary>
    public GameObject damageSource;

    void Start()
    {
        ApplyBurst();
        if (destroyAfterSeconds > 0f)
            Destroy(gameObject, destroyAfterSeconds);
    }

    void ApplyBurst()
    {
        if (radius <= 0f || damage <= 0f) return;

        var src = damageSource != null ? damageSource : gameObject;
        var hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var col in hits)
        {
            if (col == null) continue;
            var dmg = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
            if (dmg == null) continue;
            var host = dmg as MonoBehaviour;
            if (host != null && (host.gameObject == src || host.transform.IsChildOf(src.transform)))
                continue;

            if (onlyDamageEnemyTag && !HierarchyHasEnemyTag(host.gameObject))
                continue;

            dmg.TakeDamage(DamageInfo.Create(damage, src));
        }
    }

    static bool HierarchyHasEnemyTag(GameObject go)
    {
        if (go == null) return false;
        for (Transform t = go.transform; t != null; t = t.parent)
        {
            if (t.gameObject.CompareTag("Enemy"))
                return true;
        }

        return false;
    }
}
