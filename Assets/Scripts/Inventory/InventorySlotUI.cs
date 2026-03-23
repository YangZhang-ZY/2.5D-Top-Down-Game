using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 背包单个格子的 UI。显示物品图标和数量。
/// 挂在 InventorySlotUI Prefab 的根物体上。
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    [Header("引用（拖拽绑定）")]
    [Tooltip("显示物品图标的 Image")]
    public Image iconImage;

    [Tooltip("显示数量的 Text")]
    public TextMeshProUGUI countText;

    private Inventory _inventory;
    private int _slotIndex;
    private bool _loggedMissingUIRefs;

    /// <summary>绑定到指定背包的指定槽位</summary>
    public void Bind(Inventory inventory, int slotIndex)
    {
        _inventory = inventory;
        _slotIndex = slotIndex;
        Refresh();
    }

    /// <summary>刷新显示（背包变化时调用）</summary>
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
                $"[InventorySlotUI] 槽位索引={_slotIndex} 有物品「{slot.item.displayName}」但 UI 未绑定: " +
                $"iconImage={(iconImage != null)} countText={(countText != null)}。请在 Prefab 上拖好引用。");
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

    /// <summary>清空显示</summary>
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
