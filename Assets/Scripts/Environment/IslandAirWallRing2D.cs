using UnityEngine;

/// <summary>
/// Builds an invisible circular <see cref="EdgeCollider2D"/> ring (closed polyline) around the island center to block actors leaving.
/// Uses this object's <see cref="Transform.position"/> as center; walkable inside <see cref="islandRadius"/>.
/// Scene gizmo: green = walk bounds circle, orange = outer wall thickness hint.
/// </summary>
[DisallowMultipleComponent]
public class IslandAirWallRing2D : MonoBehaviour
{
    [Tooltip("Circle radius from island center to wall centerline (world units). A 90×90 square island can use half-edge ~45 as inscribed circle.")]
    [Min(0.1f)]
    public float islandRadius = 45f;

    [Tooltip("EdgeCollider2D edgeRadius; thickens the edge to reduce high-speed tunneling; roughly half visible wall thickness.")]
    [Min(0.001f)]
    public float wallThickness = 1f;

    [Tooltip("Segments in the polyline circle; higher is smoother (typical 32–64).")]
    [Range(8, 128)]
    public int circleSegments = 48;

    [Tooltip("Prefix for runtime-generated child object names.")]
    public string childNamePrefix = "AirWall";

    [Header("Gizmos")]
    [Tooltip("Draw bounds circle in Scene view (edit and play modes).")]
    [SerializeField] bool drawGizmos = true;

    [SerializeField] Color gizmoIslandBoundsColor = new Color(0.2f, 0.9f, 0.4f, 0.95f);

    [SerializeField] Color gizmoWallOuterColor = new Color(1f, 0.65f, 0.15f, 0.95f);

    Transform _holder;

    void Awake()
    {
        RebuildWalls();
    }

    /// <summary>可在编辑器按钮或其它脚本里调用，改尺寸后强制刷新。</summary>
    [ContextMenu("Rebuild Air Walls")]
    public void RebuildWalls()
    {
        EnsureHolder();
        ClearGeneratedChildren();

        float r = Mathf.Max(0.1f, islandRadius);
        int segs = Mathf.Clamp(circleSegments, 8, 128);
        float edgeRad = Mathf.Max(0.001f, wallThickness * 0.5f);

        var go = new GameObject($"{childNamePrefix}_Circle");
        go.layer = gameObject.layer;
        go.transform.SetParent(_holder, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var edge = go.AddComponent<EdgeCollider2D>();
        edge.points = BuildCirclePointsLocal(r, segs);
        edge.edgeRadius = edgeRad;
    }

    static Vector2[] BuildCirclePointsLocal(float radius, int segments)
    {
        // 首尾同点闭合，满足 Unity 对闭合 Edge 的判定
        int n = segments + 1;
        var pts = new Vector2[n];
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        pts[segments] = pts[0];
        return pts;
    }

    void EnsureHolder()
    {
        if (_holder != null) return;
        var existing = transform.Find("_AirWallRing");
        if (existing != null)
        {
            _holder = existing;
            return;
        }

        var go = new GameObject("_AirWallRing");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        _holder = go.transform;
    }

    void ClearGeneratedChildren()
    {
        EnsureHolder();
        for (int i = _holder.childCount - 1; i >= 0; i--)
            DestroyImmediateSafe(_holder.GetChild(i).gameObject);
    }

    static void DestroyImmediateSafe(Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
#else
        Destroy(obj);
#endif
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        float r = Mathf.Max(0.1f, islandRadius);
        float t = Mathf.Max(0.001f, wallThickness);
        int segs = Mathf.Max(16, Mathf.Min(64, circleSegments));
        Vector3 c = transform.position;

        DrawCircleWireXY(c, r, segs, gizmoIslandBoundsColor);
        DrawCircleWireXY(c, r + t, segs, gizmoWallOuterColor);
    }

    static void DrawCircleWireXY(Vector3 centerWorld, float radius, int segments, Color color)
    {
        if (segments < 3 || radius <= 0f) return;
        Gizmos.color = color;
        float da = Mathf.PI * 2f / segments;
        Vector3 prev = centerWorld + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * da;
            var next = centerWorld + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
