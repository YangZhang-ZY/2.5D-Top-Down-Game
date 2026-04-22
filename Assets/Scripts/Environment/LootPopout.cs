using System.Collections;
using UnityEngine;

/// <summary>
/// Simple arc motion for world drops. Added at runtime by <see cref="ResourceNode"/> or placed on pickup prefabs.
/// </summary>
public class LootPopout : MonoBehaviour
{
    [Tooltip("Extra height at the arc midpoint.")]
    public float arcHeight = 0.45f;

    [Tooltip("Travel time in seconds.")]
    public float duration = 0.4f;

    [Tooltip("Disable this component when the motion finishes.")]
    public bool disableWhenDone = true;

    private Coroutine _routine;

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
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(PopRoutine(from, to));
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
            basePos.y += lift;
            transform.position = basePos;
            yield return null;
        }

        transform.position = to;
        _routine = null;
        if (disableWhenDone)
            enabled = false;
    }
}
