using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main inventory panel: slot widgets, weight text, sort button.
/// Place on the inventory panel root.
///
/// Wire-up:
/// 1. Inventory — object with Inventory (e.g. Player)
/// 2. Slot UI prefab — InventorySlotUI prefab
/// 3. Slot grid parent — RectTransform with Grid Layout Group
/// 4. Weight text — TMP for current/max weight
/// 5. Sort button — optional
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Runtime inventory, usually on the player.")]
    public Inventory inventory;

    [Tooltip("Prefab for one slot (InventorySlotUI on root).")]
    public GameObject slotUIPrefab;

    [Tooltip("Parent for slot instances (e.g. SlotGrid with Grid Layout Group).")]
    public Transform slotGridParent;

    [Tooltip("Displays current / max weight.")]
    public TextMeshProUGUI weightText;

    [Tooltip("Invokes Inventory.Sort.")]
    public Button sortButton;

    private List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();

    private int _selectedSlotIndex = -1;

    /// <summary>Currently selected slot, or -1 if none.</summary>
    public int SelectedSlotIndex => _selectedSlotIndex;

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
                slotUI.Bind(inventory, i, this);
                _slotUIs.Add(slotUI);
            }
        }
    }

    private void RefreshAll()
    {
        if (inventory != null)
        {
            if (_selectedSlotIndex >= inventory.Slots.Count)
                _selectedSlotIndex = -1;
            else if (_selectedSlotIndex >= 0 && inventory.Slots[_selectedSlotIndex].IsEmpty)
                _selectedSlotIndex = -1;
        }

        foreach (var slotUI in _slotUIs)
            slotUI.Refresh();

        ApplySelectionVisuals();

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

    /// <summary>Selects a slot for sell / context actions. Invalid index is ignored.</summary>
    public void SelectSlot(int index)
    {
        if (inventory == null) return;
        if (index < 0 || index >= inventory.Slots.Count) return;
        _selectedSlotIndex = index;
        ApplySelectionVisuals();
    }

    public void ClearSelection()
    {
        _selectedSlotIndex = -1;
        ApplySelectionVisuals();
    }

    void ApplySelectionVisuals()
    {
        for (int i = 0; i < _slotUIs.Count; i++)
            _slotUIs[i].SetSelected(i == _selectedSlotIndex);
    }

    /// <summary>Shows the panel.</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        RefreshAll();
    }

    /// <summary>Hides the panel.</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>Toggles visibility.</summary>
    public void Toggle()
    {
        if (gameObject.activeSelf)
            Close();
        else
            Open();
    }
}
