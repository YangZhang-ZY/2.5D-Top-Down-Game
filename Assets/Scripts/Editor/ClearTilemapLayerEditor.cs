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
                "Play mode: clearing here only changes play-session memory. Stopping restores the Tilemap from disk.\n\n" +
                "To permanently erase: exit Play, use the buttons below or the component context menu, then Ctrl+S save the scene.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Edit mode: clearing marks the scene dirty; press Ctrl+S to save so changes persist.",
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
