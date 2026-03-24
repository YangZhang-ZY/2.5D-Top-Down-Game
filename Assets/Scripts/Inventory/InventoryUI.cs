using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包主面板 UI。管理格子显示、负重、整理按钮。
/// 挂在 InventoryPanel 上。
///
/// 绑定步骤：
/// 1. Inventory：拖入有 Inventory 组件的物体（如 Player）
/// 2. Slot UIPrefab：拖入 InventorySlotUI Prefab
/// 3. Slot Grid Parent：拖入 SlotGrid 物体
/// 4. Weight Text：拖入显示负重的 Text
/// 5. Sort Button：拖入整理按钮
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("引用（拖拽绑定）")]
    [Tooltip("背包逻辑，通常挂在 Player 上")]
    public Inventory inventory;

    [Tooltip("格子 Prefab（InventorySlotUI）")]
    public GameObject slotUIPrefab;

    [Tooltip("格子的父物体（SlotGrid，需要有 Grid Layout Group）")]
    public Transform slotGridParent;

    [Tooltip("显示负重的 Text")]
    public TextMeshProUGUI weightText;

    [Tooltip("整理按钮")]
    public Button sortButton;

    private List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();

    private void Start()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged.AddListener(RefreshAll);
            InitSlots();
            RefreshAll();
        }

        if (sortButton != null)
            sortButton.onClick.AddListener(OnSortClicked);

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged.RemoveListener(RefreshAll);
    }

    private void InitSlots()
    {
        if (inventory == null || slotUIPrefab == null || slotGridParent == null) return;

        for (int i = 0; i < inventory.Slots.Count; i++)
        {
            var go = Instantiate(slotUIPrefab, slotGridParent);
            var slotUI = go.GetComponent<InventorySlotUI>();
            if (slotUI != null)
            {
                slotUI.Bind(inventory, i);
                _slotUIs.Add(slotUI);
            }
        }
    }

    private void RefreshAll()
    {
        foreach (var slotUI in _slotUIs)
            slotUI.Refresh();

        if (weightText != null && inventory != null)
        {
            weightText.text = $"Weight: {inventory.CurrentWeight:F1}/{inventory.maxWeight}";
            if (inventory.IsOverweight)
                weightText.color = Color.red;
            else
                weightText.color = Color.white;
        }
    }

    private void OnSortClicked()
    {
        inventory?.Sort();
    }

    /// <summary>打开背包</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        RefreshAll();
    }

    /// <summary>关闭背包</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>切换打开/关闭</summary>
    public void Toggle()
    {
        if (gameObject.activeSelf)
            Close();
        else
            Open();
    }
}
