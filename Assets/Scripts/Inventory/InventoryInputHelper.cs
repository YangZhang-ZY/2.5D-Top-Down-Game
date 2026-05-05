using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggles inventory UI from the Inventory input action.
/// Assign Input Action Reference from InputSystem_Actions → Player → Inventory.
/// Place on any scene object; assign the GameObject that has InventoryUI (e.g. InventoryPanel).
/// </summary>
public class InventoryInputHelper : MonoBehaviour
{
    [Tooltip("Inventory UI root (object with InventoryUI).")]
    public InventoryUI inventoryUI;

    [Tooltip("Default I key; change in Input Actions.")]
    [SerializeField] private InputActionReference inventoryAction;

    private void Awake()
    {
        if (inventoryAction == null || inventoryAction.action == null)
            Debug.LogWarning(
                "[InventoryInputHelper] Assign Inventory Input Action Reference (InputSystem_Actions → Player → Inventory).",
                this);
    }

    private void OnEnable()
    {
        if (inventoryAction != null && inventoryAction.action != null)
            inventoryAction.action.Enable();
    }

    private void OnDisable()
    {
        if (inventoryAction != null && inventoryAction.action != null)
            inventoryAction.action.Disable();
    }

    private void Update()
    {
        if (inventoryUI == null) return;
        if (inventoryAction == null || inventoryAction.action == null) return;

        if (inventoryAction.action.WasPressedThisFrame())
            inventoryUI.Toggle(); // 关闭任意视图；从关闭状态打开时固定为玩家背包
    }
}
