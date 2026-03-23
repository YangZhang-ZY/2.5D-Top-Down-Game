using UnityEngine;

/// <summary>
/// 临时用于按 B 键打开/关闭背包。后续可移到 UIManager。
/// 挂到场景中任意物体（如 Canvas 或 Player），把 InventoryPanel 上的 InventoryUI 拖给 inventoryUI。
/// </summary>
public class InventoryInputHelper : MonoBehaviour
{
    [Tooltip("背包 UI，拖入 InventoryPanel（有 InventoryUI 组件的物体）")]
    public InventoryUI inventoryUI;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B) && inventoryUI != null)
            inventoryUI.Toggle();
    }
}
