using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns enemies at night / blood moon. Positions are random (dx, dy) rejection samples in an annulus around the base.
/// Count scales with day index; prefab type is weighted random.
/// </summary>
public class WaveManager : MonoBehaviour
{
    [Serializable]
    public class WaveEntry
    {
        public GameObject prefab;
        [Min(1)] public int weight = 1;
        [Tooltip("Max spawns of this type per wave; 0 = unlimited.")]
        public int maxPerType;
    }

    [Header("Spawn area (annulus around spawn center)")]
    public Transform spawnCenter;
    [Tooltip("Inner radius of the ring.")]
    public float innerRadius = 35f;
    [Tooltip("Outer radius of the ring.")]
    public float outerRadius = 45f;

    [Header("Count (scales with night index)")]
    [Tooltip("Base count on the first night.")]
    public int baseCount = 3;
    [Tooltip("Added to count for each additional night.")]
    public int perNightIncrease = 1;
    [Tooltip("Hard cap per wave; 0 = no cap.")]
    public int maxTotalPerWave;

    [Header("Blood moon")]
    [Tooltip("Multiplies the final spawn count on blood moon nights.")]
    public float bloodMoonCountMultiplier = 1.5f;

    [Header("Enemy types")]
    public List<WaveEntry> entries = new();

    [Header("Sampling")]
    [Min(8)]
    public int maxPositionAttempts = 40;

    Transform _spawnRoot;

    void Awake()
    {
        var go = new GameObject("WaveSpawned");
        go.transform.SetParent(transform, false);
        _spawnRoot = go.transform;
    }

    void OnEnable() => SubscribeEvents();

    void Start() => SubscribeEvents();

    void OnDisable() => UnsubscribeEvents();

    void SubscribeEvents()
    {
        var d = DayNightManager.Instance;
        if (d == null) return;
        d.OnNightStart -= OnNormalNight;
        d.OnNightStart += OnNormalNight;
        d.OnBloodMoonStart -= OnBloodMoonNight;
        d.OnBloodMoonStart += OnBloodMoonNight;
    }

    void UnsubscribeEvents()
    {
        if (DayNightManager.Instance == null) return;
        DayNightManager.Instance.OnNightStart -= OnNormalNight;
        DayNightManager.Instance.OnBloodMoonStart -= OnBloodMoonNight;
    }

    void OnNormalNight() => SpawnWave(1f);

    void OnBloodMoonNight() => SpawnWave(bloodMoonCountMultiplier);

    void SpawnWave(float countMultiplier)
    {
        if (entries == null || entries.Count == 0) return;
        if (spawnCenter == null)
        {
            Debug.LogWarning("[WaveManager] spawnCenter is not assigned.");
            return;
        }

        int day = Mathf.Max(1, DayNightManager.Instance != null ? DayNightManager.Instance.DayCount : 1);
        int total = baseCount + (day - 1) * perNightIncrease;
        total = Mathf.RoundToInt(total * countMultiplier);
        if (maxTotalPerWave > 0)
            total = Mathf.Min(total, maxTotalPerWave);

        var typeCounts = new Dictionary<int, int>();
        for (int i = 0; i < entries.Count; i++)
            typeCounts[i] = 0;

        for (int n = 0; n < total; n++)
        {
            int idx = PickWeightedIndex(typeCounts);
            if (idx < 0) break;

            if (!TrySampleSpawnPoint(out Vector2 pos))
            {
                Debug.LogWarning("[WaveManager] Failed to sample point in annulus; using outer radius fallback.");
                Vector2 c = spawnCenter.position;
                pos = c + Vector2.right * outerRadius;
            }

            var prefab = entries[idx].prefab;
            if (prefab == null) continue;

            Instantiate(prefab, pos, Quaternion.identity, _spawnRoot);
            typeCounts[idx]++;
        }
    }

    /// <summary>
    /// Random point in [-outer, outer]^2 relative to center, kept only if distance to center is in [inner, outer].
    /// </summary>
    bool TrySampleSpawnPoint(out Vector2 world)
    {
        Vector2 c = spawnCenter.position;
        float inner = Mathf.Min(innerRadius, outerRadius);
        float outer = Mathf.Max(innerRadius, outerRadius);

        for (int i = 0; i < maxPositionAttempts; i++)
        {
            float dx = UnityEngine.Random.Range(-outer, outer);
            float dy = UnityEngine.Random.Range(-outer, outer);
            Vector2 p = c + new Vector2(dx, dy);
            float dist = Vector2.Distance(p, c);
            if (dist >= inner && dist <= outer)
            {
                world = p;
                return true;
            }
        }

        world = default;
        return false;
    }

    int PickWeightedIndex(Dictionary<int, int> typeCounts)
    {
        int totalWeight = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].prefab == null) continue;
            int cap = entries[i].maxPerType;
            if (cap > 0 && typeCounts[i] >= cap) continue;
            totalWeight += entries[i].weight;
        }
        if (totalWeight <= 0) return -1;

        int r = UnityEngine.Random.Range(0, totalWeight);
        int acc = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].prefab == null) continue;
            int cap = entries[i].maxPerType;
            if (cap > 0 && typeCounts[i] >= cap) continue;
            acc += entries[i].weight;
            if (r < acc) return i;
        }
        return -1;
    }
}
