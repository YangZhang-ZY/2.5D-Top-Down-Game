using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Single inventory slot UI: icon, stack count, 点击选中、拖拽与其它格子（含另一只 <see cref="InventoryUI"/> 面板）交互动货。
/// 拖拽时用本格及其子物体上所有 <see cref="Graphic"/> 的 raycast 开关代替 CanvasGroup，避免 MissingComponentException。
/// </summary>
public class InventorySlotUI : MonoBehaviour,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IDropHandler
{
    [Header("References")]
    [Tooltip("Icon image for the item.")]
    public Image iconImage;

    [Tooltip("Stack count text.")]
    public TextMeshProUGUI countText;

    [Header("Selection")]
    [Tooltip("Optional: border / frame shown when this slot is selected.")]
    public GameObject selectionHighlight;

    [Tooltip("Optional: Button on this slot; wires select on click. If null, uses IPointerClickHandler on this GameObject.")]
    public Button slotButton;

    Inventory _inventory;
    int _slotIndex;
    InventoryUI _inventoryUI;
    bool _loggedMissingUIRefs;

    public void Bind(Inventory inventory, int slotIndex, InventoryUI owner = null)
    {
        _inventory = inventory;
        _slotIndex = slotIndex;
        _inventoryUI = owner != null ? owner : GetComponentInParent<InventoryUI>();

        if (slotButton == null)
            slotButton = GetComponent<Button>();

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotClicked);
        }
        else
            EnsureRaycastGraphic();

        Refresh();
    }

    void OnSlotClicked()
    {
        if (_inventoryUI != null)
            _inventoryUI.SelectSlot(_slotIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (slotButton != null) return;
        OnSlotClicked();
    }

    void EnsureRaycastGraphic()
    {
        var graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            var img = gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.001f);
            img.raycastTarget = true;
        }
        else
            graphic.raycastTarget = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_inventory == null) return;
        if (_slotIndex < 0 || _slotIndex >= _inventory.Slots.Count) return;
        if (_inventory.Slots[_slotIndex].IsEmpty) return;

        SetGraphicsRaycastRecursive(false);
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        SetGraphicsRaycastRecursive(true);
    }

    void OnDisable()
    {
        SetGraphicsRaycastRecursive(true);
    }

    void SetGraphicsRaycastRecursive(bool raycastTarget)
    {
        foreach (var g in GetComponentsInChildren<Graphic>(true))
        {
            if (g != null)
                g.raycastTarget = raycastTarget;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        var src = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<InventorySlotUI>() : null;
        if (src == null || src == this) return;
        if (!Inventory.TryDragDropBetweenSlots(src._inventory, src._slotIndex, _inventory, _slotIndex))
            return;

        InventoryUI.ClearSelectionEverywhere();
    }

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.SetActive(selected);
    }

    public void Refresh()
    {
        if (_inventory == null || _slotIndex < 0 || _slotIndex >= _inventory.Slots.Count)
        {
            Clear();
            return;
        }

        var slot = _inventory.Slots[_slotIndex];
        if (slot == null || slot.IsEmpty)
        {
            Clear();
            return;
        }

        if (!_loggedMissingUIRefs && (iconImage == null || countText == null))
        {
            _loggedMissingUIRefs = true;
            Debug.LogWarning(
                $"[InventorySlotUI] Slot {_slotIndex} has item '{slot.item.displayName}' but UI refs are missing: " +
                $"iconImage={(iconImage != null)} countText={(countText != null)}. Assign them on the prefab.",
                this);
        }

        if (iconImage != null)
        {
            iconImage.sprite = slot.item.icon;
            iconImage.enabled = slot.item.icon != null;
            iconImage.gameObject.SetActive(true);
        }

        if (countText != null)
        {
            countText.text = slot.count > 1 ? slot.count.ToString() : "";
            countText.gameObject.SetActive(slot.count > 1);
        }
    }

    public void Clear()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
            iconImage.gameObject.SetActive(false);
        }

        if (countText != null)
        {
            countText.text = "";
            countText.gameObject.SetActive(false);
        }
    }
}
