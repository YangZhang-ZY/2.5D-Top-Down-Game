using UnityEngine;

/// <summary>
/// Harvestable resource (trees, ore, etc.). Uses <see cref="Health"/>; on death spawns drops and destroys this object.
/// Drop prefab should include <see cref="WorldItemPickup"/> (or omit count for VFX-only).
/// </summary>
[RequireComponent(typeof(Health))]
public class ResourceNode : MonoBehaviour
{
    [Header("Drops")]
    [Tooltip("Prefab spawned on death (usually WorldItemPickup).")]
    public GameObject dropPrefab;

    [Tooltip("Stack size written onto the pickup (ignored if no WorldItemPickup).")]
    [Min(1)]
    public int dropCount = 1;

    [Tooltip("Spawn position offset from this transform.")]
    public Vector2 spawnOffset;

    [Tooltip("Random horizontal spread so multiple drops do not overlap.")]
    public float spawnSpread = 0.2f;

    [Header("Pop animation")]
    [Tooltip("If false, drop appears at spawn with no arc.")]
    public bool popOnSpawn = true;

    [Tooltip("Extra height at the midpoint of the arc.")]
    public float popArcHeight = 0.45f;

    [Tooltip("Time in seconds from spawn to landing.")]
    public float popDuration = 0.4f;

    [Tooltip("Random horizontal radius for the landing point.")]
    public float popLandRadius = 0.55f;

    private Health _health;

    private void Awake()
    {
        _health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        _health.OnDeath.AddListener(OnDeath);
    }

    private void OnDisable()
    {
        _health.OnDeath.RemoveListener(OnDeath);
    }

    private void OnDeath()
    {
        if (dropPrefab != null)
        {
            Vector2 jitter = Random.insideUnitCircle * spawnSpread;
            Vector3 spawnPos = transform.position + (Vector3)spawnOffset + new Vector3(jitter.x, jitter.y, 0f);
            Vector2 landJitter = Random.insideUnitCircle * popLandRadius;
            Vector3 landPos = spawnPos + new Vector3(landJitter.x, landJitter.y, 0f);

            var instance = Instantiate(dropPrefab, spawnPos, Quaternion.identity);

            var pickup = instance.GetComponent<WorldItemPickup>();
            if (pickup != null && dropCount >= 1)
                pickup.count = dropCount;

            if (popOnSpawn)
            {
                var pop = instance.GetComponent<LootPopout>();
                if (pop == null)
                    pop = instance.AddComponent<LootPopout>();
                pop.Configure(popArcHeight, popDuration);
                pop.Play(spawnPos, landPos);
            }
        }

        Destroy(gameObject);
    }
}
