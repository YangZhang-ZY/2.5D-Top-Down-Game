using UnityEngine;

/// <summary>
/// Sets SpriteRenderer sortingOrder from pseudo depth so lower/further objects draw in front.
/// 默认沿 <see cref="sortCamera"/> 的 <b>up</b> 轴在世界坐标里做点积（旋转正交相机后仍与画面纵深感一致）；
/// 可切回仅使用世界 -Y（老行为）。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SortByY : MonoBehaviour
{
    public enum SortAxisMode
    {
        [Tooltip("sortKey = -worldY（相机旋转后易与画面不一致）")]
        WorldNegY,
        [Tooltip("sortKey = -dot(position, camera.up)，适合 2D/正交相机绕视轴或轻微旋转")]
        AlongCameraUp,
    }

    [Header("Sorting")]
    [Tooltip("留空则用 Camera.main")]
    public Camera sortCamera;

    public SortAxisMode axisMode = SortAxisMode.AlongCameraUp;

    [Tooltip("Multiplier applied to sortKey when computing sortingOrder.")]
    public int sortOrderScale = 100;
    [Tooltip("Added after for fine tuning.")]
    public int orderOffset;

    SpriteRenderer[] _renderers;

    void Awake()
    {
        if (sortCamera == null)
            sortCamera = Camera.main;

        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (_renderers == null || _renderers.Length == 0)
            _renderers = new[] { GetComponent<SpriteRenderer>() };
    }

    void LateUpdate()
    {
        float sortKey;
        if (axisMode == SortAxisMode.WorldNegY)
            sortKey = -transform.position.y;
        else
        {
            if (sortCamera == null)
                sortKey = -transform.position.y;
            else
                sortKey = -Vector3.Dot(transform.position, sortCamera.transform.up);
        }

        int order = Mathf.RoundToInt(sortKey * sortOrderScale) + orderOffset;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].sortingOrder = order;
        }
    }
}
