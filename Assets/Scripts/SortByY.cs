using UnityEngine;

/// <summary>
/// Sets SpriteRenderer sortingOrder from world Y so lower objects draw in front (pseudo depth).
/// Use on actors and props that share one sorting layer.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SortByY : MonoBehaviour
{
    [Header("Sorting")]
    [Tooltip("Multiplier applied to -Y when computing sortingOrder.")]
    public int sortOrderScale = 100;
    [Tooltip("Added after the Y-based order for fine tuning.")]
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
