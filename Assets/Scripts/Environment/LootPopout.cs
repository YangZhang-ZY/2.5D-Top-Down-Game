using System.Collections;
using UnityEngine;

/// <summary>
/// Simple arc motion for world drops. Used by <see cref="ResourceNode"/> or on pickup prefabs.
/// By default the arc bulge follows <see cref="Camera.main"/> up so Q/E camera rotation still pops "up" on screen instead of locking to world +Y.
/// </summary>
public class LootPopout : MonoBehaviour
{
    [Tooltip("Extra height at the arc midpoint.")]
    public float arcHeight = 0.45f;

    [Tooltip("Travel time in seconds.")]
    public float duration = 0.4f;

    [Tooltip("Disable this component when the motion finishes.")]
    public bool disableWhenDone = true;

    [Tooltip("If true, lift along Camera.main up (recommended). If false, use world Vector3.up.")]
    public bool useCameraUpForArc = true;

    [Tooltip("Optional. If set, this transform's up is the arc direction (overrides camera).")]
    public Transform arcUpOverride;

    private Coroutine _routine;
    Vector3 _arcUpUnit = Vector3.up;

    /// <summary>Called by ResourceNode before Play.</summary>
    public void Configure(float height, float seconds)
    {
        arcHeight = height;
        duration = Mathf.Max(0.05f, seconds);
    }

    /// <summary>Moves from world position from to to along an arc.</summary>
    public void Play(Vector3 from, Vector3 to)
    {
        transform.position = from;
        _arcUpUnit = ResolveArcUpUnit();
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(PopRoutine(from, to));
    }

    Vector3 ResolveArcUpUnit()
    {
        if (arcUpOverride != null)
        {
            Vector3 u = arcUpOverride.up;
            if (u.sqrMagnitude > 1e-6f)
                return u.normalized;
        }

        if (useCameraUpForArc && Camera.main != null)
        {
            Vector3 u = Camera.main.transform.up;
            if (u.sqrMagnitude > 1e-6f)
                return u.normalized;
        }

        return Vector3.up;
    }

    private IEnumerator PopRoutine(Vector3 from, Vector3 to)
    {
        float t = 0f;
        float dur = duration;

        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            Vector3 basePos = Vector3.LerpUnclamped(from, to, k);
            float lift = Mathf.Sin(k * Mathf.PI) * arcHeight;
            basePos += _arcUpUnit * lift;
            transform.position = basePos;
            yield return null;
        }

        transform.position = to;
        _routine = null;
        if (disableWhenDone)
            enabled = false;
    }
}
