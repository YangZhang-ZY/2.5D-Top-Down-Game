using UnityEngine;

/// <summary>
/// 场景里只放一个：拖「玩家背包」与「箱子专用」两只 <see cref="InventoryUI"/>。
/// <see cref="ChestInteractionZone"/>  prefab 上不必拖 UI（建造生成后引用不到场景物体），留空时会自动用这里的引用。
/// </summary>
public class InventoryUiRegistry : MonoBehaviour
{
    public static InventoryUiRegistry Instance { get; private set; }

    [Tooltip("玩家 Inventory Panel 上的 InventoryUI（inventory / playerInventory 都指向玩家）。")]
    [SerializeField] InventoryUI playerBagInventoryUI;

    [Tooltip("箱子专用 InventoryUI；Inspector 里 Inventory 可留空，打开箱子时由 BindStorageView 绑定。")]
    [SerializeField] InventoryUI chestStorageInventoryUI;

    public InventoryUI PlayerBagInventoryUI => playerBagInventoryUI;
    public InventoryUI ChestStorageInventoryUI => chestStorageInventoryUI;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[InventoryUiRegistry] 场景里存在多个，保留第一个。", this);
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
