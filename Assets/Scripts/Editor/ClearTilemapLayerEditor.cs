#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

[CustomEditor(typeof(ClearTilemapLayer))]
public class ClearTilemapLayerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var tool = (ClearTilemapLayer)target;

        EditorGUILayout.Space(8);
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "当前在运行模式：在这里清除只会改「试玩内存」，停止运行后 Tilemap 会按磁盘上的场景还原。\n\n" +
                "要永久擦掉：先停止运行，再用下方按钮或组件右键菜单清除，然后 Ctrl+S 保存场景。",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "编辑模式下清除会标为场景已修改；请按 Ctrl+S 保存场景，之后就会永久生效。",
                MessageType.Info);
        }

        EditorGUILayout.Space(4);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("永久清除区域（写入场景，需保存）"))
            {
                var map = tool.GetComponent<Tilemap>();
                if (map != null)
                    Undo.RecordObject(map, "Clear Tilemap Region");
                tool.ClearRegionNow();
                MarkDirty(tool);
            }
            if (GUILayout.Button("永久清除整层（写入场景，需保存）"))
            {
                var map = tool.GetComponent<Tilemap>();
                if (map != null)
                    Undo.RecordObject(map, "Clear Entire Tilemap");
                tool.ClearAllTilesNow();
                MarkDirty(tool);
            }
        }
    }

    static void MarkDirty(ClearTilemapLayer tool)
    {
        var map = tool.GetComponent<Tilemap>();
        if (map != null)
            EditorUtility.SetDirty(map);
        EditorUtility.SetDirty(tool);
        var go = tool.gameObject;
        if (go.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
#endif
