using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// On a <see cref="Tilemap"/>:
/// - <b>Permanent clear</b>: in <strong>Edit mode</strong> (not playing), use Inspector buttons or component context menu, then <strong>Ctrl+S save scene</strong>.
/// - In <strong>Play mode</strong>, clear only affects memory; stopping restores tiles — do not bake maps in Play.
/// - Region = <see cref="cellsMin"/> + <see cref="cellsSize"/> in this Tilemap's cell space.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Tilemap))]
public class ClearTilemapLayer : MonoBehaviour
{
    public enum PlayClearMode
    {
        None = 0,
        EntireLayer = 1,
        Region = 2
    }

    [Header("区域清除（格子坐标）")]
    [Tooltip("矩形区域起点：本 Tilemap 上格子的整数坐标（左下角那一格）。可在 Scene 里对照 Tilemap 网格数。")]
    public Vector3Int cellsMin;

    [Tooltip("沿 X/Y/Z 各方向清的格子数量（至少为 1）。2D 地图 Z 一般填 1。")]
    public Vector3Int cellsSize = new Vector3Int(32, 32, 1);

    [Header("进入 Play 时（仅本次试玩内存，不会保存到场景）")]
    [Tooltip("None=不自动清；EntireLayer/Region 仅在运行中生效，停止运行会还原地图。")]
    [SerializeField] PlayClearMode clearWhenEnteringPlay;

    void Start()
    {
        switch (clearWhenEnteringPlay)
        {
            case PlayClearMode.EntireLayer:
                ClearAllTilesNow();
                break;
            case PlayClearMode.Region:
                ClearRegionNow();
                break;
        }
    }

    [ContextMenu("清除区域内 Tile（按 cellsMin / cellsSize）")]
    public void ClearRegionNow()
    {
        var map = GetComponent<Tilemap>();
        if (map == null) return;

        var size = new Vector3Int(
            Mathf.Max(1, cellsSize.x),
            Mathf.Max(1, cellsSize.y),
            Mathf.Max(1, cellsSize.z));
        var bounds = new BoundsInt(cellsMin, size);
        int n = size.x * size.y * size.z;
        var empty = new TileBase[n];
        map.SetTilesBlock(bounds, empty);
        MarkSceneDirtyIfEditMode(map);
    }

    [ContextMenu("清除本层所有 Tile")]
    public void ClearAllTilesNow()
    {
        var map = GetComponent<Tilemap>();
        if (map == null) return;
        map.ClearAllTiles();
        MarkSceneDirtyIfEditMode(map);
    }

#if UNITY_EDITOR
    static void MarkSceneDirtyIfEditMode(Tilemap map)
    {
        UnityEditor.EditorUtility.SetDirty(map);
        UnityEditor.EditorUtility.SetDirty(map.gameObject);
        if (!Application.isPlaying && map.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
    }
#else
    static void MarkSceneDirtyIfEditMode(Tilemap map) { }
#endif

    void OnDrawGizmosSelected()
    {
        var map = GetComponent<Tilemap>();
        if (map == null) return;

        int w = Mathf.Max(1, cellsSize.x);
        int h = Mathf.Max(1, cellsSize.y);
        int d = Mathf.Max(1, cellsSize.z);

        Vector3 p0 = map.CellToWorld(new Vector3Int(cellsMin.x, cellsMin.y, cellsMin.z));
        Vector3 pCorner = map.CellToWorld(new Vector3Int(cellsMin.x + w - 1, cellsMin.y + h - 1, cellsMin.z + d - 1));
        Vector3 cs = map.cellSize;
        Vector3 p1 = pCorner + new Vector3(cs.x, cs.y, cs.z);

        var center = (p0 + p1) * 0.5f;
        var sizeW = p1 - p0;

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.95f);
        Gizmos.DrawWireCube(center, sizeW);
    }
}
