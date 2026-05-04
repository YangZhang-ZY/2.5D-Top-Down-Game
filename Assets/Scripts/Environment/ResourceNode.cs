using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Harvestable resource (trees, ore, etc.). Uses <see cref="Health"/>; on death spawns drops and destroys this object.
/// Drop prefab should include <see cref="WorldItemPickup"/> (or omit count for VFX-only).
/// Drop quantity is random between <see cref="dropCountMin"/> and <see cref="dropCountMax"/>; each roll spawns one prefab (stack is not merged into one pickup).
/// </summary>
[RequireComponent(typeof(Health))]
public class ResourceNode : MonoBehaviour
{
    [Header("Drops")]
    [Tooltip("Prefab spawned on death (usually WorldItemPickup). Each dropped unit is one instance; count is not stacked on a single prefab.")]
    public GameObject dropPrefab;

    [Tooltip("Minimum number of prefab instances to spawn (inclusive).")]
    [Min(1)]
    [FormerlySerializedAs("dropCount")]
    public int dropCountMin = 1;

    [Tooltip("Maximum number of prefab instances to spawn (inclusive).")]
    [Min(1)]
    public int dropCountMax = 3;

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        dropCountMin = Mathf.Max(1, dropCountMin);
        dropCountMax = Mathf.Max(1, dropCountMax);
        if (dropCountMax < dropCountMin)
            dropCountMax = dropCountMin;
    }
#endif

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
            int min = Mathf.Max(1, dropCountMin);
            int max = Mathf.Max(min, dropCountMax);
            int n = Random.Range(min, max + 1);

            for (int i = 0; i < n; i++)
            {
                Vector2 jitter = Random.insideUnitCircle * spawnSpread;
                Vector3 spawnPos = transform.position + (Vector3)spawnOffset + new Vector3(jitter.x, jitter.y, 0f);
                Vector2 landJitter = Random.insideUnitCircle * popLandRadius;
                Vector3 landPos = spawnPos + new Vector3(landJitter.x, landJitter.y, 0f);

                var instance = Instantiate(dropPrefab, spawnPos, Quaternion.identity);

                var pickup = instance.GetComponent<WorldItemPickup>();
                if (pickup != null)
                    pickup.count = 1;

                if (popOnSpawn)
                {
                    var pop = instance.GetComponent<LootPopout>();
                    if (pop == null)
                        pop = instance.AddComponent<LootPopout>();
                    pop.Configure(popArcHeight, popDuration);
                    pop.Play(spawnPos, landPos);
                }
            }
        }

        Destroy(gameObject);
    }
}
