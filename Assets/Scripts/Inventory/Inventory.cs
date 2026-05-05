using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Inventory: slots, capacity, weight, add/remove/sort.
/// Attach to the player or a dedicated manager object.
///
/// Setup:
/// 1. Add to a GameObject (e.g. Player).
/// 2. Set capacity and maxWeight.
/// 3. Pickups call AddItem; shops call RemoveItem.
/// 4. UI listens to OnInventoryChanged to refresh.
/// </summary>
public class Inventory : MonoBehaviour
{
    [Header("Inventory")]
    [Tooltip("Number of slots (can be increased at runtime).")]
    [Min(1)]
    public int capacity = 20;

    [Tooltip("Maximum carry weight.")]
    [Min(0f)]
    public float maxWeight = 100f;

    [Header("Events")]
    [Tooltip("Raised when contents change; use for UI refresh.")]
    public UnityEvent OnInventoryChanged = new UnityEvent();

    [Header("Starting loadout (optional)")]
    [Tooltip("Item added to the bag at start (same Coin as merchant/UI currency). Leave empty on chests and non-player inventories.")]
    [SerializeField] ItemData startingCurrencyItem;
    [Tooltip("Starting coin count (0 = none).")]
    [Min(0)]
    [SerializeField] int startingCurrencyCount;

    [Header("Testing")]
    [Tooltip("Used by Test Add Item; if empty, loads from Resources.")]
    [SerializeField] private ItemData testItem;

    [Header("Debug")]
    [Tooltip("When enabled, logs all slots to the Console on each change.")]
    public bool debugLogOnInventoryChanged;

    private List<InventorySlot> _slots = new List<InventorySlot>();

    /// <summary>All slots (read-only, for UI binding).</summary>
    public IReadOnlyList<InventorySlot> Slots => _slots;

    /// <summary>Current total weight.</summary>
    public float CurrentWeight => GetCurrentWeight();

    /// <summary>True if over maxWeight.</summary>
    public bool IsOverweight => CurrentWeight > maxWeight;

    private void Awake()
    {
        EnsureSlotCount();
        GrantStartingCurrencyIfConfigured();
        OnInventoryChanged?.Invoke();
    }

    void GrantStartingCurrencyIfConfigured()
    {
        if (startingCurrencyItem == null || !startingCurrencyItem.IsValid) return;
        int n = Mathf.Max(0, startingCurrencyCount);
        if (n <= 0) return;
        AddItem(startingCurrencyItem, n);
    }

    void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);
        if (!Application.isPlaying || _slots == null) return;
        EnsureSlotCount();
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Keeps internal slot list length in sync with <see cref="capacity"/> (grow or shrink trailing empty slots).</summary>
    private void EnsureSlotCount()
    {
        while (_slots.Count < capacity)
            _slots.Add(new InventorySlot());

        while (_slots.Count > capacity)
        {
            var last = _slots[_slots.Count - 1];
            if (!last.IsEmpty)
            {
                capacity = _slots.Count;
                break;
            }

            _slots.RemoveAt(_slots.Count - 1);
        }
    }

    /// <summary>Adds empty slots (e.g. NPC bag upgrade). Increases <see cref="capacity"/> and fires <see cref="OnInventoryChanged"/>.</summary>
    public void ExpandCapacity(int additionalSlots)
    {
        if (additionalSlots <= 0) return;
        capacity += additionalSlots;
        EnsureSlotCount();
        OnInventoryChanged?.Invoke();
        DebugLogSlotsIfEnabled($"ExpandCapacity +{additionalSlots} (capacity={capacity})");
    }

    /// <summary>Logs every slot (context menu on component in Play mode).</summary>
    [ContextMenu("Debug: Print All Slots")]
    public void DebugPrintAllSlots()
    {
        Debug.Log($"=== Inventory [{gameObject.name}] slots={_slots.Count} weight={GetCurrentWeight():F2}/{maxWeight} ===");
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s == null || s.IsEmpty)
                Debug.Log($"  [{i}] empty");
            else
                Debug.Log($"  [{i}] {s.item?.displayName} (id={s.item?.id}) x{s.count}");
        }
    }

    private void DebugLogSlotsIfEnabled(string reason)
    {
        if (!debugLogOnInventoryChanged) return;
        Debug.Log($"[Inventory] {reason} — dumping slots");
        DebugPrintAllSlots();
    }

    /// <summary>
    /// Adds items: stacks into matching slots first, then empty slots.
    /// Respects weight and capacity; returns how many were actually added.
    /// </summary>
    public int AddItem(ItemData item, int count)
    {
        if (item == null || !item.IsValid || count <= 0) return 0;

        int remaining = count;
        float weightPerUnit = item.weight;

        // 1) Stack onto existing stacks (empty slots are handled in step 2 with Set)
        foreach (var slot in _slots)
        {
            if (remaining <= 0) break;
            if (slot.IsEmpty) continue;
            if (!slot.CanAddMore(item)) continue;

            int space = slot.RemainingStackSpace(item);
            if (space <= 0) continue;

            float wouldAddWeight = weightPerUnit * Mathf.Min(space, remaining);
            if (GetCurrentWeight() + wouldAddWeight > maxWeight)
            {
                int canAddByWeight = Mathf.FloorToInt((maxWeight - GetCurrentWeight()) / weightPerUnit);
                if (canAddByWeight <= 0) break;
                space = Mathf.Min(space, canAddByWeight);
            }

            int toAdd = Mathf.Min(space, remaining);
            slot.count += toAdd;
            remaining -= toAdd;
        }

        // 2) Fill empty slots
        foreach (var slot in _slots)
        {
            if (remaining <= 0) break;
            if (!slot.IsEmpty) continue;

            int toAdd = Mathf.Min(item.maxStack, remaining);
            float wouldAddWeight = weightPerUnit * toAdd;
            if (GetCurrentWeight() + wouldAddWeight > maxWeight)
            {
                int canAddByWeight = Mathf.FloorToInt((maxWeight - GetCurrentWeight()) / weightPerUnit);
                if (canAddByWeight <= 0) break;
                toAdd = Mathf.Min(toAdd, canAddByWeight);
            }

            if (toAdd <= 0) break;

            slot.Set(item, toAdd);
            remaining -= toAdd;
        }

        int added = count - remaining;
        if (added > 0)
        {
            DebugLogSlotsIfEnabled($"AddItem +{added}");
            OnInventoryChanged?.Invoke();
        }

        return added;
    }

    /// <summary>Removes from a specific slot.</summary>
    /// <returns>Amount actually removed.</returns>
    public int RemoveItem(int slotIndex, int count)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return 0;
        var slot = _slots[slotIndex];
        if (slot.IsEmpty || count <= 0) return 0;

        int toRemove = Mathf.Min(count, slot.count);
        slot.count -= toRemove;
        if (slot.count <= 0)
            slot.Clear();

        if (toRemove > 0)
        {
            DebugLogSlotsIfEnabled($"RemoveItem slot {slotIndex} -{toRemove}");
            OnInventoryChanged?.Invoke();
        }

        return toRemove;
    }

    /// <summary>Removes by item type (searches from the end of the list).</summary>
    /// <returns>Amount actually removed.</returns>
    public int RemoveItem(ItemData item, int count)
    {
        if (item == null || count <= 0) return 0;

        int remaining = count;
        for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var slot = _slots[i];
            if (slot.IsEmpty || slot.item != item) continue;

            int toRemove = Mathf.Min(remaining, slot.count);
            slot.count -= toRemove;
            if (slot.count <= 0)
                slot.Clear();
            remaining -= toRemove;
        }

        int removed = count - remaining;
        if (removed > 0)
        {
            DebugLogSlotsIfEnabled($"RemoveItem(ItemData) -{removed}");
            OnInventoryChanged?.Invoke();
        }

        return removed;
    }

    /// <summary>Whether the inventory can accept this many items (capacity, weight, stacks).</summary>
    public bool HasSpace(ItemData item, int count)
    {
        return CanAdd(item, count);
    }

    /// <summary>Whether addition is possible without modifying the inventory.</summary>
    public bool CanAdd(ItemData item, int count)
    {
        if (item == null || !item.IsValid || count <= 0) return false;

        float weightPerUnit = item.weight;
        float maxAddWeight = maxWeight - GetCurrentWeight();
        if (maxAddWeight <= 0f) return false;
        if (weightPerUnit * count > maxAddWeight)
        {
            int maxByWeight = Mathf.FloorToInt(maxAddWeight / weightPerUnit);
            if (maxByWeight <= 0) return false;
            count = maxByWeight;
        }

        int canAdd = 0;
        foreach (var slot in _slots)
        {
            if (slot.CanAddMore(item))
                canAdd += slot.RemainingStackSpace(item);
            if (canAdd >= count) return true;
        }
        return canAdd >= count;
    }

    /// <summary>Current total weight.</summary>
    public float GetCurrentWeight()
    {
        float total = 0f;
        foreach (var slot in _slots)
            total += slot.GetTotalWeight();
        return total;
    }

    /// <summary>Total count of an item across all stacks.</summary>
    public int GetItemCount(ItemData item)
    {
        if (item == null || !item.IsValid) return 0;
        int total = 0;
        foreach (var slot in _slots)
        {
            if (!slot.IsEmpty && slot.item == item)
                total += slot.count;
        }
        return total;
    }

    /// <summary>True if at least count items are present. count &lt;= 0 always passes.</summary>
    public bool HasCount(ItemData item, int count)
    {
        if (count <= 0) return true;
        if (item == null || !item.IsValid) return false;
        return GetItemCount(item) >= count;
    }

    /// <summary>Sorts by type and id, merges stacks.</summary>
    public void Sort()
    {
        // 1) Collect non-empty slots
        var items = new List<(ItemData item, int count)>();
        foreach (var slot in _slots)
        {
            if (!slot.IsEmpty)
            {
                items.Add((slot.item, slot.count));
                slot.Clear();
            }
        }

        // 2) Merge same item
        var merged = new Dictionary<ItemData, int>();
        foreach (var (item, count) in items)
        {
            if (merged.TryGetValue(item, out int existing))
                merged[item] = existing + count;
            else
                merged[item] = count;
        }

        // 3) Sort by type then id
        var sorted = new List<(ItemData item, int count)>();
        foreach (var kv in merged)
            sorted.Add((kv.Key, kv.Value));

        sorted.Sort((a, b) =>
        {
            int typeCompare = a.item.itemType.CompareTo(b.item.itemType);
            if (typeCompare != 0) return typeCompare;
            return string.CompareOrdinal(a.item.id, b.item.id);
        });

        // 4) Refill slots up to maxStack
        int slotIdx = 0;
        foreach (var (item, totalCount) in sorted)
        {
            int remaining = totalCount;
            while (remaining > 0 && slotIdx < _slots.Count)
            {
                var slot = _slots[slotIdx];
                int toPut = Mathf.Min(item.maxStack, remaining);
                slot.Set(item, toPut);
                remaining -= toPut;
                if (slot.count >= item.maxStack || remaining <= 0)
                    slotIdx++;
            }
        }

        DebugLogSlotsIfEnabled("Sort");
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Swap two slots (drag-and-drop, etc.).</summary>
    public void SwapSlots(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= _slots.Count || indexB < 0 || indexB >= _slots.Count) return;
        if (indexA == indexB) return;

        var slotA = _slots[indexA];
        var slotB = _slots[indexB];

        var itemA = slotA.item;
        var countA = slotA.count;
        var itemB = slotB.item;
        var countB = slotB.count;

        slotA.Set(itemB, countB);
        slotB.Set(itemA, countA);

        DebugLogSlotsIfEnabled("SwapSlots");
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// UI drag-drop: move/merge/swap within one bag, or between bags (respects weight and stack limits).
    /// </summary>
    public static bool TryDragDropBetweenSlots(Inventory fromInv, int fromIndex, Inventory toInv, int toIndex)
    {
        if (fromInv == null || toInv == null) return false;
        if (fromIndex < 0 || fromIndex >= fromInv._slots.Count) return false;
        if (toIndex < 0 || toIndex >= toInv._slots.Count) return false;
        if (fromInv == toInv && fromIndex == toIndex) return false;

        if (fromInv == toInv)
            return fromInv.TryDragDropInternal(fromIndex, toIndex);

        return TryDragDropCross(fromInv, fromIndex, toInv, toIndex);
    }

    bool TryDragDropInternal(int from, int to)
    {
        var sFrom = _slots[from];
        var sTo = _slots[to];
        if (sFrom.IsEmpty) return false;

        if (sTo.IsEmpty)
        {
            sTo.Set(sFrom.item, sFrom.count);
            sFrom.Clear();
            DebugLogSlotsIfEnabled("TryDragDropInternal move");
            OnInventoryChanged?.Invoke();
            return true;
        }

        if (sTo.item == sFrom.item && sTo.CanAddMore(sFrom.item))
        {
            int space = sTo.RemainingStackSpace(sFrom.item);
            int move = Mathf.Min(space, sFrom.count);
            if (move <= 0) return false;
            sTo.count += move;
            sFrom.count -= move;
            if (sFrom.count <= 0) sFrom.Clear();
            DebugLogSlotsIfEnabled("TryDragDropInternal merge");
            OnInventoryChanged?.Invoke();
            return true;
        }

        SwapSlots(from, to);
        return true;
    }

    static bool TryDragDropCross(Inventory a, int idxA, Inventory b, int idxB)
    {
        var sA = a._slots[idxA];
        var sB = b._slots[idxB];
        if (sA.IsEmpty) return false;

        if (sB.IsEmpty)
        {
            int move = MaxMoveCountToEmptySlot(a, idxA, b);
            if (move <= 0) return false;
            b._slots[idxB].Set(sA.item, move);
            sA.count -= move;
            if (sA.count <= 0) sA.Clear();
            a.DebugLogSlotsIfEnabled("TryDragDropCross move");
            b.DebugLogSlotsIfEnabled("TryDragDropCross move");
            a.OnInventoryChanged?.Invoke();
            b.OnInventoryChanged?.Invoke();
            return true;
        }

        if (sB.item == sA.item && sB.CanAddMore(sA.item))
        {
            int space = sB.RemainingStackSpace(sA.item);
            int move = Mathf.Min(space, sA.count);
            move = Mathf.Min(move, MaxExtraCountByWeight(b, sA.item, move));
            if (move <= 0) return false;
            sB.count += move;
            sA.count -= move;
            if (sA.count <= 0) sA.Clear();
            a.DebugLogSlotsIfEnabled("TryDragDropCross merge");
            b.DebugLogSlotsIfEnabled("TryDragDropCross merge");
            a.OnInventoryChanged?.Invoke();
            b.OnInventoryChanged?.Invoke();
            return true;
        }

        return TrySwapSlotsCross(a, idxA, b, idxB);
    }

    static int MaxMoveCountToEmptySlot(Inventory fromInv, int fromIdx, Inventory toInv)
    {
        var sFrom = fromInv._slots[fromIdx];
        if (sFrom.IsEmpty || sFrom.item == null) return 0;
        int count = Mathf.Min(sFrom.count, sFrom.item.maxStack);
        float w = sFrom.item.weight;
        if (w <= 0f) return count;
        float room = toInv.maxWeight - toInv.GetCurrentWeight();
        if (room <= 0f) return 0;
        int maxByWeight = Mathf.FloorToInt(room / w);
        return Mathf.Max(0, Mathf.Min(count, maxByWeight));
    }

    static int MaxExtraCountByWeight(Inventory inv, ItemData item, int desired)
    {
        if (item == null || desired <= 0) return 0;
        float w = item.weight;
        if (w <= 0f) return desired;
        float room = inv.maxWeight - inv.GetCurrentWeight();
        if (room <= 0f) return 0;
        return Mathf.Max(0, Mathf.Min(desired, Mathf.FloorToInt(room / w)));
    }

    static bool TrySwapSlotsCross(Inventory invA, int idxA, Inventory invB, int idxB)
    {
        var slotA = invA._slots[idxA];
        var slotB = invB._slots[idxB];
        float wA = slotA.GetTotalWeight();
        float wB = slotB.GetTotalWeight();
        double newWa = invA.GetCurrentWeight() - wA + wB;
        double newWb = invB.GetCurrentWeight() - wB + wA;
        if (newWa > invA.maxWeight + 1e-5 || newWb > invB.maxWeight + 1e-5)
            return false;

        var itemA = slotA.item;
        var countA = slotA.count;
        var itemB = slotB.item;
        var countB = slotB.count;

        slotA.Set(itemB, countB);
        slotB.Set(itemA, countA);

        invA.DebugLogSlotsIfEnabled("TrySwapSlotsCross");
        invB.DebugLogSlotsIfEnabled("TrySwapSlotsCross");
        invA.OnInventoryChanged?.Invoke();
        invB.OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Increases slot count (only grows).</summary>
    public void UpgradeCapacity(int newCapacity)
    {
        if (newCapacity <= capacity) return;
        capacity = newCapacity;
        EnsureSlotCount();
        DebugLogSlotsIfEnabled("UpgradeCapacity");
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Sets a new max weight.</summary>
    public void UpgradeMaxWeight(float newMaxWeight)
    {
        maxWeight = Mathf.Max(0f, newMaxWeight);
        DebugLogSlotsIfEnabled("UpgradeMaxWeight");
        OnInventoryChanged?.Invoke();
    }

    /// <summary>Context menu: add test stack.</summary>
    [ContextMenu("Test Add Item")]
    private void TestAddItem()
    {
        var item = testItem;
        if (item == null)
        {
            var items = Resources.LoadAll<ItemData>("");
            if (items == null || items.Length == 0)
            {
                Debug.LogWarning("[Inventory] Assign testItem in the Inspector or place ItemData assets under Resources.");
                return;
            }
            item = items[0];
        }
        int added = AddItem(item, 5);
        Debug.Log($"[Inventory] Test add {item.displayName} x5 — added {added}.");
    }
}
