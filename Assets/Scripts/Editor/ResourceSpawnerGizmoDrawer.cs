#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Draws ResourceSpawner rings with Handles so they are visible above terrain when configured.
/// </summary>
public static class ResourceSpawnerGizmoDrawer
{
    /// <summary>2D gameplay on XY plane; disc normal is +Z. For XZ ground, switch to Vector3.up in both code paths.</summary>
    private static readonly Vector3 DiscNormal = Vector3.forward;

    [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.NonSelected)]
    private static void DrawGizmo(ResourceSpawner sp, GizmoType gizmoType)
    {
        if (sp == null || !sp.showGizmos) return;

        if (sp.gizmoOnlyWhenSelected && (gizmoType & GizmoType.NonSelected) != 0)
            return;

        var prevZ = Handles.zTest;
        Handles.zTest = CompareFunction.Always;

        Vector3 center = sp.ringCenter != null ? sp.ringCenter.position : sp.transform.position;

        if (sp.gizmoShowBuildRing && sp.buildRingOuterRadius > 0f)
        {
            Handles.color = new Color(0.3f, 0.85f, 1f, 1f);
            Handles.DrawWireDisc(center, DiscNormal, sp.buildRingOuterRadius);
        }

        if (sp.gizmoShowInnerRing)
            DrawRing(sp.innerRing, new Color(0.2f, 0.9f, 0.35f, 1f), center);
        if (sp.gizmoShowMiddleRing)
            DrawRing(sp.middleRing, new Color(1f, 0.92f, 0.2f, 1f), center);
        if (sp.gizmoShowOuterRing)
            DrawRing(sp.outerRing, new Color(1f, 0.35f, 0.3f, 1f), center);

        if (sp.gizmoShowExclusions && sp.exclusionZones != null)
        {
            foreach (var zone in sp.exclusionZones)
            {
                var zc = new Vector3(zone.center.x, zone.center.y, 0f);
                Handles.color = new Color(1f, 0.25f, 0.25f, 0.9f);
                Handles.DrawWireDisc(zc, DiscNormal, zone.radius);
                Handles.color = new Color(1f, 0f, 0f, 0.12f);
                Handles.DrawSolidDisc(zc, DiscNormal, zone.radius);
            }
        }

        Handles.zTest = prevZ;
    }

    private static void DrawRing(ResourceSpawner.RingConfig ring, Color color, Vector3 center)
    {
        if (ring == null) return;
        Handles.color = color;
        if (ring.innerRadius > 0f)
            Handles.DrawWireDisc(center, DiscNormal, ring.innerRadius);
        if (ring.outerRadius > 0f)
            Handles.DrawWireDisc(center, DiscNormal, ring.outerRadius);
    }
}
#endif
