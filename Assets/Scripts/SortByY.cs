using UnityEngine;

/// <summary>
/// 根据世界 Y 坐标动态设置 SpriteRenderer 的 sortingOrder，实现“在下方的物体画在前面”的遮挡效果。
/// 挂在玩家、敌人、树、石头等需要互相遮挡的物体上。
/// 确保这些物体都在同一个 Sorting Layer。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SortByY : MonoBehaviour
{
    [Header("排序")]
    [Tooltip("Y 坐标乘以该系数得到 sortingOrder，数值越大同一高度差下顺序差越大")]
    public int sortOrderScale = 100;
    [Tooltip("在此物体计算出的 order 上再加的偏移，可用于同 Y 时微调前后")]
    public int orderOffset;

    private SpriteRenderer[] _renderers;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (_renderers == null || _renderers.Length == 0)
            _renderers = new[] { GetComponent<SpriteRenderer>() };
    }

    private void LateUpdate()
    {
        int order = Mathf.RoundToInt(-transform.position.y * sortOrderScale) + orderOffset;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].sortingOrder = order;
        }
    }
}
