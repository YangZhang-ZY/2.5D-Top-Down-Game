using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Buy/sell panel: sells from the selected backpack slot, buys from a configured offer list.
/// Wire references in the Inspector; assign one Button per shop offer (same order as offers array).
/// </summary>
public class MerchantUI : MonoBehaviour
{
    [Serializable]
    public class ShopOfferEntry
    {
        public ItemData item;

        [Tooltip("0 = use item.basePrice × buy price multiplier.")]
        public int buyPriceOverride;
    }

    [Header("References")]
    public InventoryUI inventoryUI;
    public PlayerWallet wallet;

    [Tooltip("Optional: shows current gold.")]
    public TextMeshProUGUI goldText;

    [Header("Sell")]
    public Button sellOneButton;
    public Button sellStackButton;

    [Tooltip("Sell price per unit = basePrice × this (rounded, min 1).")]
    [Range(0.01f, 5f)]
    public float sellPriceMultiplier = 0.5f;

    [Header("Buy")]
    public ShopOfferEntry[] shopOffers;

    [Tooltip("One button per shopOffers element (same index).")]
    public Button[] buyButtons;

    [Tooltip("When override is 0: buy price = basePrice × this (rounded, min 1).")]
    [Range(0.01f, 5f)]
    public float buyPriceMultiplier = 1f;

    [Header("Rules")]
    [Tooltip("If true, trading is only allowed while DayNightManager.Instance.IsDay.")]
    public bool onlyTradeDuringDay = true;

    void Start()
    {
        if (sellOneButton != null)
            sellOneButton.onClick.AddListener(OnSellOne);
        if (sellStackButton != null)
            sellStackButton.onClick.AddListener(OnSellStack);

        if (shopOffers != null && buyButtons != null)
        {
            int n = Mathf.Min(shopOffers.Length, buyButtons.Length);
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                buyButtons[i].onClick.RemoveAllListeners();
                buyButtons[i].onClick.AddListener(() => OnBuyOffer(idx));
            }

            if (shopOffers.Length != buyButtons.Length)
            {
                Debug.LogWarning(
                    $"[MerchantUI] shopOffers ({shopOffers.Length}) and buyButtons ({buyButtons.Length}) length differ; only first {n} offers are wired.",
                    this);
            }
        }
    }

    void OnEnable()
    {
        if (wallet != null)
            wallet.OnGoldChanged.AddListener(OnGoldChangedHandler);
        RefreshAll();
    }

    void OnDisable()
    {
        if (wallet != null)
            wallet.OnGoldChanged.RemoveListener(OnGoldChangedHandler);
    }

    void OnGoldChangedHandler(int _)
    {
        RefreshAll();
    }

    /// <summary>Call when opening the panel (e.g. from interaction script).</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        RefreshAll();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void RefreshAll()
    {
        if (goldText != null && wallet != null)
            goldText.text = $"Gold: {wallet.Gold}";

        bool canTrade = CanTrade();
        bool canSell = canTrade && CanSellSelected();

        if (sellOneButton != null)
            sellOneButton.interactable = canSell;
        if (sellStackButton != null)
            sellStackButton.interactable = canSell;

        if (shopOffers == null || buyButtons == null) return;

        int n = Mathf.Min(shopOffers.Length, buyButtons.Length);
        for (int i = 0; i < n; i++)
        {
            var offer = shopOffers[i];
            var btn = buyButtons[i];
            if (offer == null || offer.item == null || !offer.item.IsValid || btn == null)
            {
                if (btn != null) btn.interactable = false;
                continue;
            }

            int price = GetBuyPrice(offer);
            bool canBuy = canTrade && wallet != null && wallet.Gold >= price &&
                          inventoryUI != null && inventoryUI.inventory != null &&
                          inventoryUI.inventory.HasSpace(offer.item, 1);

            btn.interactable = canBuy;

            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = $"Buy {offer.item.displayName} — {price}g";
        }
    }

    bool CanTrade()
    {
        if (!onlyTradeDuringDay) return true;
        if (DayNightManager.Instance == null) return true;
        return DayNightManager.Instance.IsDay;
    }

    bool CanSellSelected()
    {
        if (inventoryUI == null || inventoryUI.inventory == null) return false;
        int i = inventoryUI.SelectedSlotIndex;
        if (i < 0 || i >= inventoryUI.inventory.Slots.Count) return false;
        return !inventoryUI.inventory.Slots[i].IsEmpty;
    }

    int GetSellUnitPrice(ItemData item)
    {
        if (item == null) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(item.basePrice * sellPriceMultiplier));
    }

    int GetBuyPrice(ShopOfferEntry offer)
    {
        if (offer == null || offer.item == null) return int.MaxValue;
        if (offer.buyPriceOverride > 0) return offer.buyPriceOverride;
        return Mathf.Max(1, Mathf.RoundToInt(offer.item.basePrice * buyPriceMultiplier));
    }

    public void OnSellOne()
    {
        if (!CanTrade() || wallet == null || inventoryUI == null || inventoryUI.inventory == null) return;
        int i = inventoryUI.SelectedSlotIndex;
        if (i < 0) return;
        var inv = inventoryUI.inventory;
        if (i >= inv.Slots.Count || inv.Slots[i].IsEmpty) return;

        var item = inv.Slots[i].item;
        int unitPrice = GetSellUnitPrice(item);
        int removed = inv.RemoveItem(i, 1);
        if (removed > 0)
            wallet.AddGold(unitPrice * removed);
    }

    public void OnSellStack()
    {
        if (!CanTrade() || wallet == null || inventoryUI == null || inventoryUI.inventory == null) return;
        int i = inventoryUI.SelectedSlotIndex;
        if (i < 0) return;
        var inv = inventoryUI.inventory;
        if (i >= inv.Slots.Count || inv.Slots[i].IsEmpty) return;

        var item = inv.Slots[i].item;
        int count = inv.Slots[i].count;
        int unitPrice = GetSellUnitPrice(item);
        int removed = inv.RemoveItem(i, count);
        if (removed > 0)
            wallet.AddGold(unitPrice * removed);
    }

    public void OnBuyOffer(int offerIndex)
    {
        if (!CanTrade() || wallet == null || inventoryUI == null || inventoryUI.inventory == null) return;
        if (shopOffers == null || offerIndex < 0 || offerIndex >= shopOffers.Length) return;

        var offer = shopOffers[offerIndex];
        if (offer == null || offer.item == null || !offer.item.IsValid) return;

        int price = GetBuyPrice(offer);
        if (!wallet.TrySpend(price)) return;

        int added = inventoryUI.inventory.AddItem(offer.item, 1);
        if (added < 1)
        {
            wallet.AddGold(price);
            return;
        }

        RefreshAll();
    }
}
