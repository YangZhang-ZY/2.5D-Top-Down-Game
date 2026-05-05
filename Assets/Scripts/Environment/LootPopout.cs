using System.Collections;
using UnityEngine;

/// <summary>
/// Simple arc motion for world drops. Added at runtime by <see cref="ResourceNode"/> or placed on pickup prefabs.
/// 抛物线「鼓起」方向默认取 <see cref="Camera.main"/> 的 up，这样 Q/E 旋转视角后视觉上仍是朝屏幕上方弹出，而不是死用世界 Y。
/// </summary>
public class LootPopout : MonoBehaviour
{
    [Tooltip("Extra height at the arc midpoint.")]
    public float arcHeight = 0.45f;

    [Tooltip("Travel time in seconds.")]
    public float duration = 0.4f;

    [Tooltip("Disable this component when the motion finishes.")]
    public bool disableWhenDone = true;

    [Tooltip("为 true：抬升沿主摄像机 transform.up（推荐，与旋转视角一致）。为 false：始终用世界 Vector3.up（旧行为）。")]
    public bool useCameraUpForArc = true;

    [Tooltip("可选；不空时用作「朝上」方向（归一化），优先级高于主摄像机。")]
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
