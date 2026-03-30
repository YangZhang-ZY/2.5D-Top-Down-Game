using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 每天「白天开始」时按三个圆环带刷新资源（补满模式）。
/// 环心内侧有「建造环」半径内不刷资源；采样受禁区过滤。
/// 2.5D 预制体生成时使用 identity 旋转，避免 Billboard + 2D 碰撞体错位。
/// </summary>
public class ResourceSpawner : MonoBehaviour
{
    // ==================== 数据结构 ====================

    [System.Serializable]
    public class ResourceEntry
    {
        [Tooltip("要生成的资源预制体（如树木、石头、宝箱）")]
        public GameObject prefab;

        [Tooltip("随机权重（同环内各项按权重比例抽取）")]
        [Min(1)]
        public int weight = 1;

        [Tooltip("该环内这种资源的场上最大数量")]
        [Min(1)]
        public int maxCount = 5;

        [HideInInspector]
        public readonly List<GameObject> alive = new();
    }

    [System.Serializable]
    public class RingConfig
    {
        [Tooltip("环带内半径（距中心最近距离）")]
        public float innerRadius = 5f;

        [Tooltip("环带外半径（距中心最远距离）")]
        public float outerRadius = 15f;

        [Tooltip("该环内资源配置列表（可多种）")]
        public List<ResourceEntry> entries = new();
    }

    [System.Serializable]
    public class ExclusionZone
    {
        [Tooltip("禁区中心（世界坐标）")]
        public Vector2 center;

        [Tooltip("禁区半径")]
        public float radius = 5f;
    }

    // ==================== Inspector ====================

    [Header("环带中心（一般填基地 / 地图中心）")]
    public Transform ringCenter;

    [Header("建造 / 安全区（内环之内，不刷任何资源）")]
    [Tooltip("从环心到此半径内不生成资源，供玩家建塔、围墙。应小于内环资源带的 innerRadius，或由代码与内环取 max。")]
    public float buildRingOuterRadius = 8f;

    [Header("内环（树木、石头等简单资源）")]
    public RingConfig innerRing;

    [Header("中环（矿石、宝箱等）")]
    public RingConfig middleRing;

    [Header("外环（贵重奖励、宝箱等）")]
    public RingConfig outerRing;

    [Header("禁区（Boss 位置等不刷资源区域）")]
    public List<ExclusionZone> exclusionZones = new();

    [Header("拒绝采样最大尝试次数")]
    [Min(10)]
    public int maxSampleAttempts = 60;

    [Header("Scene Gizmo")]
    [Tooltip("关闭则不在 Scene 视图绘制任何环")]
    public bool showGizmos = true;

    [Tooltip("开启：仅选中本物体时显示；关闭：未选中时也会显示（方便对齐地图）")]
    public bool gizmoOnlyWhenSelected = true;

    [Tooltip("青色：建造 / 安全区")]
    public bool gizmoShowBuildRing = true;

    [Tooltip("绿色：内环资源带")]
    public bool gizmoShowInnerRing = true;

    [Tooltip("黄色：中环")]
    public bool gizmoShowMiddleRing = true;

    [Tooltip("红色：外环")]
    public bool gizmoShowOuterRing = true;

    [Tooltip("禁区球体与红圈")]
    public bool gizmoShowExclusions = true;

    // ==================== 内部 ====================

    private Transform _innerParent;
    private Transform _middleParent;
    private Transform _outerParent;

    // ==================== 生命周期 ====================

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

        // 第一天白天直接刷一次
        RefillAll();
    }

    private void OnDisable()
    {
        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnDayStart -= OnDayStart;
    }

    // ==================== 事件回调 ====================

    private void OnDayStart() => RefillAll();

    // ==================== 刷新逻辑 ====================

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
            Debug.LogWarning("[ResourceSpawner] ringCenter 未设置。");
            return;
        }

        // 清理已销毁（被采集/破坏）的引用
        foreach (var entry in ring.entries)
            entry.alive.RemoveAll(go => go == null);

        // 尝试补满每种资源
        foreach (var entry in ring.entries)
        {
            int need = entry.maxCount - entry.alive.Count;
            for (int i = 0; i < need; i++)
            {
                if (!TrySamplePoint(ring, out Vector2 point)) continue;

                // 2.5D Billboard：不要随机 Z 旋转，否则 BoxCollider2D 与 Sprite 视觉会错位
                var go = Instantiate(entry.prefab, point, Quaternion.identity, parent);
                entry.alive.Add(go);
            }
        }
    }

    // ==================== 点采样 ====================

    private bool TrySamplePoint(RingConfig ring, out Vector2 result)
    {
        Vector2 center = ringCenter.position;
        float rMin = Mathf.Max(ring.innerRadius, buildRingOuterRadius);
        float rMax = ring.outerRadius;

        if (rMax <= rMin)
        {
            Debug.LogWarning($"[ResourceSpawner] 环带无效：外半径 {rMax} 应大于 max(内半径, 建造区) = {rMin}。");
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

    // ==================== 辅助 ====================

    private Transform CreateParent(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    // Scene 中的环由 Editor/ResourceSpawnerGizmoDrawer 绘制（Handles，不被遮挡）
}
