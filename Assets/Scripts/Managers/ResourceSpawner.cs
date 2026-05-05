using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Each day start (<see cref="DayNightManager.OnDayStart"/>) and on scene <see cref="Start"/>,
/// spawns up to <see cref="ResourceEntry.spawnPerDay"/> per entry inside three rings, capped by <see cref="ResourceEntry.maxCount"/>.
/// Destroyed/gathered instances leave the tracked list so the next day can refill to the cap.
/// Avoids sampling inside build safe radius; optional exclusion zones.
/// </summary>
public class ResourceSpawner : MonoBehaviour
{
    [System.Serializable]
    public class ResourceEntry
    {
        [Tooltip("Prefab to spawn (trees, rocks, chests, etc.).")]
        public GameObject prefab;

        [Tooltip("Max new spawns per day until maxCount. First scene Start counts as one refill.")]
        [Min(0)]
        [FormerlySerializedAs("weight")]
        public int spawnPerDay = 1;

        [Tooltip("Max concurrent instances of this entry in this ring (no more spawns that day once reached).")]
        [Min(1)]
        public int maxCount = 5;

        [HideInInspector]
        public readonly List<GameObject> alive = new();
    }

    [System.Serializable]
    public class RingConfig
    {
        [Tooltip("Inner radius (closest distance to center).")]
        public float innerRadius = 5f;

        [Tooltip("Outer radius (farthest distance to center).")]
        public float outerRadius = 15f;

        [Tooltip("Resource entries spawned in this ring.")]
        public List<ResourceEntry> entries = new();
    }

    [System.Serializable]
    public class ExclusionZone
    {
        [Tooltip("Zone center in world space.")]
        public Vector2 center;

        [Tooltip("Exclusion radius.")]
        public float radius = 5f;
    }

    [Header("Ring center (base / map origin)")]
    public Transform ringCenter;

    [Header("Build / safe zone (no spawns inside this radius)")]
    [Tooltip("No resources inside this radius from ring center (towers, walls). Should be less than inner ring innerRadius or sampling clamps accordingly.")]
    public float buildRingOuterRadius = 8f;

    [Header("Inner ring (common resources)")]
    public RingConfig innerRing;

    [Header("Middle ring")]
    public RingConfig middleRing;

    [Header("Outer ring")]
    public RingConfig outerRing;

    [Header("Exclusion zones (e.g. boss arena)")]
    public List<ExclusionZone> exclusionZones = new();

    [Header("Sampling")]
    [Min(10)]
    public int maxSampleAttempts = 60;

    [Header("Scene gizmo")]
    [Tooltip("When false, no rings drawn in the Scene view.")]
    public bool showGizmos = true;

    [Tooltip("When true, gizmos show only with this object selected.")]
    public bool gizmoOnlyWhenSelected = true;

    [Tooltip("Cyan: build / safe zone.")]
    public bool gizmoShowBuildRing = true;

    [Tooltip("Green: inner resource ring.")]
    public bool gizmoShowInnerRing = true;

    [Tooltip("Yellow: middle ring.")]
    public bool gizmoShowMiddleRing = true;

    [Tooltip("Red: outer ring.")]
    public bool gizmoShowOuterRing = true;

    [Tooltip("Exclusion spheres and circles.")]
    public bool gizmoShowExclusions = true;

    private Transform _innerParent;
    private Transform _middleParent;
    private Transform _outerParent;

    private void Awake()
    {
        _innerParent  = CreateParent("Ring_Inner");
        _middleParent = CreateParent("Ring_Middle");
        _outerParent  = CreateParent("Ring_Outer");
    }

    private void OnEnable()
    {
        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnDayStart += OnDayStart;
    }

    private void Start()
    {
        if (DayNightManager.Instance != null)
        {
            DayNightManager.Instance.OnDayStart -= OnDayStart;
            DayNightManager.Instance.OnDayStart += OnDayStart;
        }

        RefillAll();
    }

    private void OnDisable()
    {
        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnDayStart -= OnDayStart;
    }

    private void OnDayStart() => RefillAll();

    private void RefillAll()
    {
        RefillRing(innerRing,  _innerParent);
        RefillRing(middleRing, _middleParent);
        RefillRing(outerRing,  _outerParent);
    }

    private void RefillRing(RingConfig ring, Transform parent)
    {
        if (ring.entries == null || ring.entries.Count == 0) return;
        if (ringCenter == null)
        {
            Debug.LogWarning("[ResourceSpawner] ringCenter is not assigned.");
            return;
        }

        foreach (var entry in ring.entries)
            entry.alive.RemoveAll(go => go == null);

        foreach (var entry in ring.entries)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning(
                    "[ResourceSpawner] Ring entry has no prefab (unassigned, missing asset, or destroyed reference). Assign a project Prefab in the Inspector.",
                    this);
                continue;
            }

            int room = entry.maxCount - entry.alive.Count;
            if (room <= 0) continue;

            int toSpawn = Mathf.Min(entry.spawnPerDay, room);
            for (int i = 0; i < toSpawn; i++)
            {
                if (!TrySamplePoint(ring, out Vector2 point)) break;

                // Identity rotation keeps billboard + 2D colliders consistent.
                var go = Instantiate(entry.prefab, point, Quaternion.identity, parent);
                entry.alive.Add(go);
            }
        }
    }

    private bool TrySamplePoint(RingConfig ring, out Vector2 result)
    {
        Vector2 center = ringCenter.position;
        float rMin = Mathf.Max(ring.innerRadius, buildRingOuterRadius);
        float rMax = ring.outerRadius;

        if (rMax <= rMin)
        {
            Debug.LogWarning($"[ResourceSpawner] Invalid ring: outer {rMax} must exceed max(inner radius, build zone) = {rMin}.");
            result = Vector2.zero;
            return false;
        }

        for (int attempt = 0; attempt < maxSampleAttempts; attempt++)
        {
            float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(rMin, rMax);
            Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            if (IsInBuildZone(candidate, center)) continue;
            if (IsInExclusionZone(candidate)) continue;

            result = candidate;
            return true;
        }

        result = Vector2.zero;
        return false;
    }

    private bool IsInBuildZone(Vector2 point, Vector2 center)
    {
        if (buildRingOuterRadius <= 0f) return false;
        return Vector2.Distance(point, center) < buildRingOuterRadius;
    }

    private bool IsInExclusionZone(Vector2 point)
    {
        foreach (var zone in exclusionZones)
        {
            if (Vector2.Distance(point, zone.center) < zone.radius)
                return true;
        }
        return false;
    }

    private Transform CreateParent(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    // Ring drawing: see Editor/ResourceSpawnerGizmoDrawer.cs (Handles-based).
}
