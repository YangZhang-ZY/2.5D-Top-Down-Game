using System.Collections;
using UnityEngine;

/// <summary>
/// 掉落物生成时的抛物线弹出动画。由 <see cref="ResourceNode"/> 在运行时添加并调用 <see cref="Play"/>，
/// 也可挂在拾取物预制体根物体上（会由 ResourceNode 找到并配置）。
/// </summary>
public class LootPopout : MonoBehaviour
{
    [Tooltip("抛物线最高点相对起点与终点连线的额外高度")]
    public float arcHeight = 0.45f;

    [Tooltip("从起点落到终点的时间（秒）")]
    public float duration = 0.4f;

    [Tooltip("落地后是否关闭此组件（省 Update）")]
    public bool disableWhenDone = true;

    private Coroutine _routine;

    /// <summary>由 ResourceNode 写入参数后调用。</summary>
    public void Configure(float height, float seconds)
    {
        arcHeight = height;
        duration = Mathf.Max(0.05f, seconds);
    }

    /// <summary>从 from 沿抛物线落到 to（世界坐标）。</summary>
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
            // 水平/直线基底插值 + 正弦弧高（两端为 0，中间最高）
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
