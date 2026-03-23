using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 背包核心逻辑。管理槽位、容量、负重、添加/移除/整理。
/// 可挂在 Player 或单例 GameObject 上。
///
/// 使用步骤：
/// 1. 挂到场景中的 GameObject（如 Player）
/// 2. 设置 capacity、maxWeight
/// 3. 收集系统调用 AddItem，商店调用 RemoveItem
/// 4. UI 订阅 OnInventoryChanged 刷新显示
/// </summary>
public class Inventory : MonoBehaviour
{
    [Header("背包配置")]
    [Tooltip("格子数量，后续可升级")]
    [Min(1)]
    public int capacity = 20;

    [Tooltip("最大负重，后续可升级")]
    [Min(0f)]
    public float maxWeight = 100f;

    [Header("事件")]
    [Tooltip("物品变化时触发，供 UI 刷新")]
    public UnityEvent OnInventoryChanged = new UnityEvent();

    [Header("测试")]
    [Tooltip("测试添加物品时使用，留空则从 Resources 查找")]
    [SerializeField] private ItemData testItem;

    [Header("调试")]
    [Tooltip("勾选后，每次背包变化会在 Console 打印所有槽位")]
    public bool debugLogOnInventoryChanged;

    private List<InventorySlot> _slots = new List<InventorySlot>();

    /// <summary>所有槽位（只读，供 UI 绑定）</summary>
    public IReadOnlyList<InventorySlot> Slots => _slots;

    /// <summary>当前负重</summary>
    public float CurrentWeight => GetCurrentWeight();

    /// <summary>是否超重</summary>
    public bool IsOverweight => CurrentWeight > maxWeight;

    private void Awake()
    {
        EnsureSlotCount();
    }

    /// <summary>确保槽位数量与 capacity 一致</summary>
    private void EnsureSlotCount()
    {
        while (_slots.Count < capacity)
            _slots.Add(new InventorySlot());
    }

    /// <summary>在 Console 打印当前所有槽位（运行中：选中物体 → Inventory 组件标题栏右键）</summary>
    [ContextMenu("Debug: 打印所有槽位")]
    public void DebugPrintAllSlots()
    {
        Debug.Log($"=== Inventory [{gameObject.name}] 槽位数={_slots.Count} 负重={GetCurrentWeight():F2}/{maxWeight} ===");
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            if (s == null || s.IsEmpty)
                Debug.Log($"  [{i}] 空");
            else
                Debug.Log($"  [{i}] {s.item?.displayName} (id={s.item?.id}) x{s.count}");
        }
    }

    private void DebugLogSlotsIfEnabled(string reason)
    {
        if (!debugLogOnInventoryChanged) return;
        Debug.Log($"[Inventory] {reason} → 打印槽位");
        DebugPrintAllSlots();
    }

    /// <summary>
    /// 添加物品。优先堆叠到已有同物品槽位，再占用空槽。
    /// 受负重和容量限制，返回实际添加数量。
    /// </summary>
    public int AddItem(ItemData item, int count)
    {
        if (item == null || !item.IsValid || count <= 0) return 0;

        int remaining = count;
        float weightPerUnit = item.weight;

        // 1. 先尝试堆叠到已有同物品槽位（空槽必须走阶段 2 的 Set，不能只 count+= 否则会 item 仍为 null）
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

        // 2. 再占用空槽
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
            DebugLogSlotsIfEnabled($"AddItem 成功 +{added}");
            OnInventoryChanged?.Invoke();
        }

        return added;
    }

    /// <summary>从指定槽位移除物品</summary>
    /// <returns>实际移除数量</returns>
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
            DebugLogSlotsIfEnabled($"RemoveItem 槽{slotIndex} -{toRemove}");
            OnInventoryChanged?.Invoke();
        }

        return toRemove;
    }

    /// <summary>移除指定物品（从后往前找，优先清空尾槽）</summary>
    /// <returns>实际移除数量</returns>
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

    /// <summary>能否放入指定数量的物品（考虑容量、负重、堆叠）</summary>
    public bool HasSpace(ItemData item, int count)
    {
        return CanAdd(item, count);
    }

    /// <summary>能否放入指定数量（纯计算，不修改背包）</summary>
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

    /// <summary>计算当前总负重</summary>
    public float GetCurrentWeight()
    {
        float total = 0f;
        foreach (var slot in _slots)
            total += slot.GetTotalWeight();
        return total;
    }

    /// <summary>整理背包：按类型、名称排序，可堆叠的合并</summary>
    public void Sort()
    {
        // 1. 收集所有非空槽位内容
        var items = new List<(ItemData item, int count)>();
        foreach (var slot in _slots)
        {
            if (!slot.IsEmpty)
            {
                items.Add((slot.item, slot.count));
                slot.Clear();
            }
        }

        // 2. 合并同物品
        var merged = new Dictionary<ItemData, int>();
        foreach (var (item, count) in items)
        {
            if (merged.TryGetValue(item, out int existing))
                merged[item] = existing + count;
            else
                merged[item] = count;
        }

        // 3. 按类型、名称排序后放回
        var sorted = new List<(ItemData item, int count)>();
        foreach (var kv in merged)
            sorted.Add((kv.Key, kv.Value));

        sorted.Sort((a, b) =>
        {
            int typeCompare = a.item.itemType.CompareTo(b.item.itemType);
            if (typeCompare != 0) return typeCompare;
            return string.CompareOrdinal(a.item.id, b.item.id);
        });

        // 4. 按顺序放入槽位（每个槽位可堆叠到 maxStack）
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

    /// <summary>交换两个槽位（用于拖拽、整理）</summary>
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

    /// <summary>升级格子数量（仅可增加）</summary>
    public void UpgradeCapacity(int newCapacity)
    {
        if (newCapacity <= capacity) return;
        capacity = newCapacity;
        EnsureSlotCount();
        DebugLogSlotsIfEnabled("UpgradeCapacity");
        OnInventoryChanged?.Invoke();
    }

    /// <summary>升级最大负重</summary>
    public void UpgradeMaxWeight(float newMaxWeight)
    {
        maxWeight = Mathf.Max(0f, newMaxWeight);
        DebugLogSlotsIfEnabled("UpgradeMaxWeight");
        OnInventoryChanged?.Invoke();
    }

    /// <summary>测试用：添加物品。Inspector 中右键脚本 → Test Add Item</summary>
    [ContextMenu("Test Add Item")]
    private void TestAddItem()
    {
        var item = testItem;
        if (item == null)
        {
            var items = Resources.LoadAll<ItemData>("");
            if (items == null || items.Length == 0)
            {
                Debug.LogWarning("[Inventory] 请在 Inspector 指定 testItem，或在 Resources 文件夹放置 ItemData。");
                return;
            }
            item = items[0];
        }
        int added = AddItem(item, 5);
        Debug.Log($"[Inventory] 测试添加 {item.displayName} x5，实际添加 {added}");
    }
}
