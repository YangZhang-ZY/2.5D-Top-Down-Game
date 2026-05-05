using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 箱子交互：<b>两只</b> <see cref="InventoryUI"/>（玩家背包面板 + 拷贝出来的箱子面板）同时打开/关闭，
/// 两边格子用拖拽在玩家 <see cref="Inventory"/> 与箱子 <see cref="Inventory"/> 之间搬物品。
///
/// 配置：箱子物体上挂 <see cref="Inventory"/>（设 capacity/maxWeight）；挂本脚本；Collider2D 勾选 Is Trigger。
/// UI：可在 Inspector 拖两只 <see cref="InventoryUI"/>；若为空则使用场景里 <see cref="InventoryUiRegistry"/>（推荐——建造的箱子 prefab 无法拖场景引用）。
/// 打开箱子时会 <see cref="InventoryUI.BindStorageView"/> 绑定<strong>当前</strong>箱子的库存（多箱子共用一个箱子面板）。/// </summary>
public class ChestInteractionZone : MonoBehaviour
{
    [Header("箱子数据")]
    [SerializeField] Inventory chestInventory;

    [Header("两只 UI 面板")]
    [Tooltip("玩家背包对应的 InventoryUI（Player Inventory 上的字段都绑玩家 Inventory）。")]
    [SerializeField] InventoryUI playerBagInventoryUI;

    [Tooltip("箱子专用的 InventoryUI（两条 Inventory 引用都绑本箱子的 Inventory）。")]
    [SerializeField] InventoryUI chestInventoryUI;

    [Tooltip("例如 Player/Interact 或 Build（E）。")]
    [SerializeField] InputActionReference interactAction;

    [Header("Filter")]
    [SerializeField] string playerTag = "Player";

    [Header("Leave zone")]
    [Tooltip("离开触发区时，若箱子面板仍开着则同时关掉两只面板。")]
    [SerializeField] bool closePanelsWhenPlayerLeaves = true;

    bool _playerInRange;

    void Awake()
    {
        if (chestInventory == null)
            chestInventory = GetComponent<Inventory>() ?? GetComponentInChildren<Inventory>();

        if (chestInventory == null)
            Debug.LogWarning("[ChestInteractionZone] 请在箱子上挂 Inventory 组件。", this);
        if (interactAction == null || interactAction.action == null)
            Debug.LogWarning("[ChestInteractionZone] 指定交互键 Input Action。", this);
    }

    void Start()
    {
        var reg = InventoryUiRegistry.Instance;
        if (reg != null)
        {
            if (playerBagInventoryUI == null)
                playerBagInventoryUI = reg.PlayerBagInventoryUI;
            if (chestInventoryUI == null)
                chestInventoryUI = reg.ChestStorageInventoryUI;
        }

        if (playerBagInventoryUI == null)
            Debug.LogWarning("[ChestInteractionZone] 指定玩家背包 InventoryUI，或在场景里添加 InventoryUiRegistry。", this);
        if (chestInventoryUI == null)
            Debug.LogWarning("[ChestInteractionZone] 指定箱子 InventoryUI，或在场景里添加 InventoryUiRegistry。", this);
    }

    void OnEnable()
    {
        if (interactAction != null && interactAction.action != null)
            interactAction.action.Enable();
    }

    void OnDisable()
    {
        if (interactAction != null && interactAction.action != null)
            interactAction.action.Disable();
    }

    void Update()
    {
        if (!_playerInRange) return;
        if (interactAction == null || interactAction.action == null) return;
        if (!interactAction.action.WasPressedThisFrame()) return;

        bool chestOpen = chestInventoryUI != null && chestInventoryUI.gameObject.activeSelf;
        if (chestOpen)
        {
            playerBagInventoryUI?.Close();
            chestInventoryUI.Close();
        }
        else
        {
            playerBagInventoryUI?.OpenPlayerBag();
            if (chestInventoryUI != null)
            {
                chestInventoryUI.BindStorageView(chestInventory);
                chestInventoryUI.Open();
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
            _playerInRange = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (string.IsNullOrEmpty(playerTag) || other.CompareTag(playerTag))
        {
            _playerInRange = false;
            if (!closePanelsWhenPlayerLeaves) return;
            if (chestInventoryUI != null && chestInventoryUI.gameObject.activeSelf)
            {
                playerBagInventoryUI?.Close();
                chestInventoryUI.Close();
            }
        }
    }
}
