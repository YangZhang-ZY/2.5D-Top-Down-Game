using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Single merchant slot UI: icon, count, price, selection highlight.
/// </summary>
public class MerchantSlotUI : MonoBehaviour, IPointerClickHandler
{
    public Image iconImage;
    public TextMeshProUGUI countText;
    public TextMeshProUGUI priceText;
    public GameObject selectionHighlight;
    public Button slotButton;

    MerchantUI _owner;
    int _slotIndex;

    public void Bind(MerchantUI owner, int slotIndex)
    {
        _owner = owner;
        _slotIndex = slotIndex;

        if (slotButton == null)
            slotButton = GetComponent<Button>();

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotClicked);
        }
        else
            EnsureRaycastGraphic();
    }

    public void SetData(ItemData item, int count, int unitPrice, bool showAsBuyback)
    {
        bool hasItem = item != null && count > 0;

        if (iconImage != null)
        {
            iconImage.sprite = hasItem ? item.icon : null;
            iconImage.enabled = hasItem && item.icon != null;
            iconImage.gameObject.SetActive(hasItem);
        }

        if (countText != null)
        {
            countText.text = hasItem && count > 1 ? count.ToString() : string.Empty;
            countText.gameObject.SetActive(hasItem && count > 1);
        }

        if (priceText != null)
        {
            if (hasItem)
                priceText.text = $"{Mathf.Max(0, item.basePrice)}g";
            else
                priceText.text = string.Empty;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.SetActive(selected);
    }

    void OnSlotClicked()
    {
        if (_owner != null)
            _owner.SelectMerchantSlot(_slotIndex);
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
}
