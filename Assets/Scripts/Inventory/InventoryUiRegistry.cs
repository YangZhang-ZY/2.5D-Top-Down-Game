using UnityEngine;

/// <summary>
/// Place one in the scene: assign the player bag <see cref="InventoryUI"/> and the chest-only <see cref="InventoryUI"/>.
/// <see cref="ChestInteractionZone"/> prefabs can leave UI fields empty (built instances cannot reference scene objects); those resolve from here.
/// </summary>
public class InventoryUiRegistry : MonoBehaviour
{
    public static InventoryUiRegistry Instance { get; private set; }

    [Tooltip("InventoryUI on the player inventory panel (inventory / playerInventory both point at the player).")]
    [SerializeField] InventoryUI playerBagInventoryUI;

    [Tooltip("箱子专用 InventoryUI；Inspector 里 Inventory 可留空，打开箱子时由 BindStorageView 绑定。")]
    [SerializeField] InventoryUI chestStorageInventoryUI;

    public InventoryUI PlayerBagInventoryUI => playerBagInventoryUI;
    public InventoryUI ChestStorageInventoryUI => chestStorageInventoryUI;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[InventoryUiRegistry] Multiple instances in scene; keeping the first.", this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
