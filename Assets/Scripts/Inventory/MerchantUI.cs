using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Merchant panel with slot-based shop inventory:
/// - Select one merchant slot.
/// - Press one Buy button to buy from selected slot.
/// - Sell from selected backpack slot into buyback slots.
/// - At each new day start: clear all shop slots and roll fresh items.
/// </summary>
public class MerchantUI : MonoBehaviour
{
    [Serializable]
    class MerchantOfferSlot
    {
        public ItemData item;
        public int count;
        public int unitPrice;
        public bool isBuyback;
        public bool IsEmpty => item == null || count <= 0;

        public void Clear()
        {
            item = null;
            count = 0;
            unitPrice = 0;
            isBuyback = false;
        }
    }

    [Header("References")]
    public InventoryUI inventoryUI;

    [Header("Currency")]
    [Tooltip("若赋值：以玩家背包中该道具的数量为「钱」（与 ItemData 金币道具一致）；不赋值则使用 PlayerWallet。")]
    [SerializeField] ItemData currencyItem;
    public PlayerWallet wallet;
    public TextMeshProUGUI goldText;

    [Header("Merchant Slots UI")]
    [Tooltip("Prefab with MerchantSlotUI component.")]
    public GameObject merchantSlotPrefab;
    [Tooltip("Grid parent for merchant slots.")]
    public Transform merchantSlotGridParent;
    [Min(1)] public int merchantSlotCount = 12;

    [Header("Buttons")]
    public Button buySelectedButton;
    public Button sellOneButton;
    public Button closeButton;
    [Tooltip("Also closes inventory UI when merchant panel closes.")]
    public bool closeInventoryOnClose = true;

    [Header("Daily Stock")]
    [Tooltip("Random pool used when a new day starts.")]
    public ItemData[] dailyStockPool;
    [Min(1)] public int dailyStockEntryCount = 6;
    [Min(1)] public int dailyStockStackSize = 3;

    [Header("Pricing")]
    [Tooltip("NPC buying price per unit = basePrice × this (sell from player).")]
    [Range(0.01f, 5f)] public float sellPriceMultiplier = 0.5f;
    [Tooltip("Normal stock buy price per unit = basePrice × this.")]
    [Range(0.01f, 5f)] public float buyPriceMultiplier = 1f;
    [Tooltip("Buyback slot price per unit = sold price × this.")]
    [Range(0.01f, 5f)] public float buybackPriceMultiplier = 1f;

    [Header("Rules")]
    [Tooltip("If true, trading is only allowed during day.")]
    public bool onlyTradeDuringDay = true;

    readonly List<MerchantOfferSlot> _merchantSlots = new List<MerchantOfferSlot>();
    readonly List<MerchantSlotUI> _merchantSlotUIs = new List<MerchantSlotUI>();
    int _selectedMerchantSlotIndex = -1;
    bool _initialized;
    bool _inputBlockRequested;

    void Start()
    {
        if (buySelectedButton != null)
            buySelectedButton.onClick.AddListener(OnBuySelected);
        if (sellOneButton != null)
            sellOneButton.onClick.AddListener(OnSellOne);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (inventoryUI != null && inventoryUI.inventory != null)
            inventoryUI.inventory.OnInventoryChanged.AddListener(RefreshAll);

        EnsureInitialized();
        BuildNewDayStock();
    }

    void OnEnable()
    {
        if (!UsesItemCurrency() && wallet != null)
            wallet.OnGoldChanged.AddListener(OnGoldChangedHandler);
        RegisterDayNightCallbacks();
        RefreshAll();
    }

    void OnDisable()
    {
        ReleaseInputBlockIfNeeded();
        if (!UsesItemCurrency() && wallet != null)
            wallet.OnGoldChanged.RemoveListener(OnGoldChangedHandler);
        UnregisterDayNightCallbacks();
    }

    void OnDestroy()
    {
        ReleaseInputBlockIfNeeded();
        if (inventoryUI != null && inventoryUI.inventory != null)
            inventoryUI.inventory.OnInventoryChanged.RemoveListener(RefreshAll);
        UnregisterDayNightCallbacks();
    }

    void RegisterDayNightCallbacks()
    {
        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnDayStart += HandleDayStart;
    }

    void UnregisterDayNightCallbacks()
    {
        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnDayStart -= HandleDayStart;
    }

    void HandleDayStart()
    {
        BuildNewDayStock();
    }

    void OnGoldChangedHandler(int _)
    {
        RefreshAll();
    }

    void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _merchantSlots.Clear();
        for (int i = 0; i < merchantSlotCount; i++)
            _merchantSlots.Add(new MerchantOfferSlot());

        InitSlotUIInstances();
    }

    void InitSlotUIInstances()
    {
        _merchantSlotUIs.Clear();
        if (merchantSlotPrefab == null || merchantSlotGridParent == null) return;

        for (int i = 0; i < merchantSlotCount; i++)
        {
            var go = Instantiate(merchantSlotPrefab, merchantSlotGridParent);
            var slotUI = go.GetComponent<MerchantSlotUI>();
            if (slotUI == null)
            {
                Debug.LogWarning("[MerchantUI] merchantSlotPrefab is missing MerchantSlotUI.", this);
                continue;
            }

            slotUI.Bind(this, i);
            _merchantSlotUIs.Add(slotUI);
        }
    }

    /// <summary>Call when opening the panel (e.g. from interaction script).</summary>
    public void Open()
    {
        if (onlyTradeDuringDay && DayNightManager.Instance != null && !DayNightManager.Instance.IsDay)
            return;

        gameObject.SetActive(true);
        RequestInputBlockIfNeeded();
        RefreshAll();
    }

    public void Close()
    {
        ReleaseInputBlockIfNeeded();
        gameObject.SetActive(false);
        if (closeInventoryOnClose && inventoryUI != null)
            inventoryUI.Close();
    }

    public void SelectMerchantSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _merchantSlots.Count) return;
        _selectedMerchantSlotIndex = slotIndex;
        ApplySelectionVisuals();
        RefreshButtonsOnly();
    }

    public void RefreshAll()
    {
        EnsureInitialized();

        if (goldText != null)
        {
            if (UsesItemCurrency())
                goldText.text = $"Gold: {GetSpendableCurrency()}";
            else if (wallet != null)
                goldText.text = $"Gold: {wallet.Gold}";
        }

        ValidateSelectedIndex();

        for (int i = 0; i < _merchantSlotUIs.Count && i < _merchantSlots.Count; i++)
        {
            var data = _merchantSlots[i];
            _merchantSlotUIs[i].SetData(data.item, data.count, data.unitPrice, data.isBuyback);
        }

        ApplySelectionVisuals();
        RefreshButtonsOnly();
    }

    void RefreshButtonsOnly()
    {
        bool canTrade = CanTrade();
        bool canSell = canTrade && CanSellSelected(1);
        bool canBuy = canTrade && CanBuySelectedMerchantSlot();

        if (sellOneButton != null)
            sellOneButton.interactable = canSell;
        if (buySelectedButton != null)
            buySelectedButton.interactable = canBuy;
    }

    void ApplySelectionVisuals()
    {
        for (int i = 0; i < _merchantSlotUIs.Count; i++)
            _merchantSlotUIs[i].SetSelected(i == _selectedMerchantSlotIndex);
    }

    void ValidateSelectedIndex()
    {
        if (_selectedMerchantSlotIndex < 0 || _selectedMerchantSlotIndex >= _merchantSlots.Count) return;
        if (_merchantSlots[_selectedMerchantSlotIndex].IsEmpty)
            _selectedMerchantSlotIndex = -1;
    }

    bool CanTrade()
    {
        if (!onlyTradeDuringDay) return true;
        if (DayNightManager.Instance == null) return true;
        return DayNightManager.Instance.IsDay;
    }

    int GetSelectedPlayerSlotCount()
    {
        if (inventoryUI == null || inventoryUI.inventory == null) return 0;
        int i = inventoryUI.SelectedSlotIndex;
        if (i < 0 || i >= inventoryUI.inventory.Slots.Count) return 0;
        var slot = inventoryUI.inventory.Slots[i];
        return slot.IsEmpty ? 0 : slot.count;
    }

    bool CanSellSelected(int countToSell)
    {
        if (countToSell <= 0) return false;
        if (inventoryUI == null || inventoryUI.inventory == null) return false;
        int i = inventoryUI.SelectedSlotIndex;
        if (i < 0 || i >= inventoryUI.inventory.Slots.Count) return false;

        var slot = inventoryUI.inventory.Slots[i];
        if (slot.IsEmpty || slot.item == null || !slot.item.IsValid) return false;

        int sellUnitPrice = GetSellUnitPrice(slot.item);
        int buybackPrice = GetBuybackUnitPrice(sellUnitPrice);
        return HasSpaceForBuyback(slot.item, buybackPrice);
    }

    bool CanBuySelectedMerchantSlot()
    {
        if (_selectedMerchantSlotIndex < 0 || _selectedMerchantSlotIndex >= _merchantSlots.Count) return false;
        if (inventoryUI == null || inventoryUI.inventory == null) return false;
        if (!UsesItemCurrency() && wallet == null) return false;

        var selected = _merchantSlots[_selectedMerchantSlotIndex];
        if (selected.IsEmpty || selected.item == null || !selected.item.IsValid) return false;
        if (GetSpendableCurrency() < selected.unitPrice) return false;
        return inventoryUI.inventory.HasSpace(selected.item, 1);
    }

    int GetSellUnitPrice(ItemData item)
    {
        if (item == null) return 0;
        return Mathf.Max(1, Mathf.RoundToInt(item.basePrice * sellPriceMultiplier));
    }

    int GetNormalBuyUnitPrice(ItemData item)
    {
        if (item == null) return int.MaxValue;
        return Mathf.Max(1, Mathf.RoundToInt(item.basePrice * buyPriceMultiplier));
    }

    int GetBuybackUnitPrice(int soldUnitPrice)
    {
        return Mathf.Max(1, Mathf.RoundToInt(soldUnitPrice * buybackPriceMultiplier));
    }

    public void OnSellOne()
    {
        TrySellSelected(1);
    }

    void TrySellSelected(int requestedCount)
    {
        if (!CanTrade() || requestedCount <= 0) return;
        if ((!UsesItemCurrency() && wallet == null) || inventoryUI == null || inventoryUI.inventory == null) return;

        var inv = inventoryUI.inventory;
        int i = inventoryUI.SelectedSlotIndex;
        if (i < 0 || i >= inv.Slots.Count) return;
        if (inv.Slots[i].IsEmpty) return;

        ItemData item = inv.Slots[i].item;
        int maxSell = Mathf.Min(requestedCount, inv.Slots[i].count);
        int sellUnitPrice = GetSellUnitPrice(item);
        int buybackPrice = GetBuybackUnitPrice(sellUnitPrice);
        if (!HasSpaceForBuyback(item, buybackPrice)) return;

        int removed = inv.RemoveItem(i, maxSell);
        if (removed <= 0) return;

        AddCurrency(removed * sellUnitPrice);
        AddToBuybackStock(item, removed, buybackPrice);
        RefreshAll();
    }

    public void OnBuySelected()
    {
        if (!CanTrade()) return;
        if (!CanBuySelectedMerchantSlot()) return;

        var selected = _merchantSlots[_selectedMerchantSlotIndex];
        int price = selected.unitPrice;
        if (!TrySpendCurrency(price)) return;

        int added = inventoryUI.inventory.AddItem(selected.item, 1);
        if (added < 1)
        {
            AddCurrency(price);
            return;
        }

        TrySelectInventorySlotWithItem(selected.item);

        selected.count -= 1;
        if (selected.count <= 0)
            selected.Clear();

        RefreshAll();
    }

    void TrySelectInventorySlotWithItem(ItemData item)
    {
        if (item == null || inventoryUI == null || inventoryUI.inventory == null) return;
        var slots = inventoryUI.inventory.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && slots[i].item == item)
            {
                inventoryUI.SelectSlot(i);
                return;
            }
        }
    }

    void BuildNewDayStock()
    {
        EnsureInitialized();
        ClearAllMerchantSlots();
        _selectedMerchantSlotIndex = -1;

        if (dailyStockPool == null || dailyStockPool.Length == 0)
        {
            RefreshAll();
            return;
        }

        int entries = Mathf.Min(dailyStockEntryCount, merchantSlotCount);
        for (int i = 0; i < entries; i++)
        {
            var item = PickRandomDailyItem();
            if (item == null) continue;

            int price = GetNormalBuyUnitPrice(item);
            AddToStock(item, dailyStockStackSize, price, false);
        }

        RefreshAll();
    }

    ItemData PickRandomDailyItem()
    {
        if (dailyStockPool == null || dailyStockPool.Length == 0) return null;

        for (int tries = 0; tries < dailyStockPool.Length * 2; tries++)
        {
            int idx = UnityEngine.Random.Range(0, dailyStockPool.Length);
            var item = dailyStockPool[idx];
            if (item != null && item.IsValid)
                return item;
        }

        return null;
    }

    void ClearAllMerchantSlots()
    {
        for (int i = 0; i < _merchantSlots.Count; i++)
            _merchantSlots[i].Clear();
    }

    void AddToBuybackStock(ItemData item, int count, int unitPrice)
    {
        AddToStock(item, count, unitPrice, true);
    }

    void AddToStock(ItemData item, int count, int unitPrice, bool isBuyback)
    {
        if (item == null || !item.IsValid || count <= 0) return;

        for (int i = 0; i < _merchantSlots.Count; i++)
        {
            var slot = _merchantSlots[i];
            if (slot.IsEmpty) continue;
            if (slot.item != item) continue;
            if (slot.unitPrice != unitPrice) continue;
            if (slot.isBuyback != isBuyback) continue;
            slot.count += count;
            return;
        }

        for (int i = 0; i < _merchantSlots.Count; i++)
        {
            var slot = _merchantSlots[i];
            if (!slot.IsEmpty) continue;

            slot.item = item;
            slot.count = count;
            slot.unitPrice = unitPrice;
            slot.isBuyback = isBuyback;
            return;
        }

        Debug.LogWarning("[MerchantUI] Merchant slots full, cannot add more offers.", this);
    }

    bool HasSpaceForBuyback(ItemData item, int unitPrice)
    {
        for (int i = 0; i < _merchantSlots.Count; i++)
        {
            var slot = _merchantSlots[i];
            if (slot.IsEmpty) return true;
            if (slot.item == item && slot.unitPrice == unitPrice && slot.isBuyback)
                return true;
        }
        return false;
    }

    void RequestInputBlockIfNeeded()
    {
        if (_inputBlockRequested) return;
        PlayerInputBlocker.Request(this);
        _inputBlockRequested = true;
    }

    void ReleaseInputBlockIfNeeded()
    {
        if (!_inputBlockRequested) return;
        PlayerInputBlocker.Release(this);
        _inputBlockRequested = false;
    }

    Inventory PlayerBag => inventoryUI != null ? inventoryUI.playerInventory : null;

    bool UsesItemCurrency()
    {
        return currencyItem != null && currencyItem.IsValid && PlayerBag != null;
    }

    int GetSpendableCurrency()
    {
        if (UsesItemCurrency())
            return PlayerBag.GetItemCount(currencyItem);
        return wallet != null ? wallet.Gold : 0;
    }

    bool TrySpendCurrency(int amount)
    {
        if (amount <= 0) return true;
        if (UsesItemCurrency())
        {
            if (!PlayerBag.HasCount(currencyItem, amount)) return false;
            int removed = PlayerBag.RemoveItem(currencyItem, amount);
            return removed >= amount;
        }
        return wallet != null && wallet.TrySpend(amount);
    }

    void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        if (UsesItemCurrency())
            PlayerBag.AddItem(currencyItem, amount);
        else if (wallet != null)
            wallet.AddGold(amount);
    }
}
