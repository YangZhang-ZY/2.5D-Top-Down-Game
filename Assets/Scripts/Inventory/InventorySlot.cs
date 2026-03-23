using UnityEngine;

/// <summary>
/// 背包单个槽位。存储物品引用和数量。
/// 供 Inventory 使用，支持堆叠判断。
/// </summary>
[System.Serializable]
public class InventorySlot
{
    [Tooltip("物品数据引用，空槽位为 null")]
    public ItemData item;

    [Tooltip("当前数量")]
    [Min(0)]
    public int count;

    /// <summary>槽位是否为空</summary>
    public bool IsEmpty => item == null || count <= 0;

    /// <summary>
    /// 能否向此槽位添加更多指定物品。
    /// 空槽位可接受任何物品；非空槽位仅当物品相同且未满时可添加。
    /// </summary>
    public bool CanAddMore(ItemData itemToAdd)
    {
        if (itemToAdd == null || !itemToAdd.IsValid) return false;
        if (IsEmpty) return true;
        if (item != itemToAdd) return false;
        return count < item.maxStack;
    }

    /// <summary>
    /// 此槽位还能堆叠多少指定物品。
    /// 空槽位返回该物品的 maxStack；不同物品返回 0。
    /// </summary>
    public int RemainingStackSpace(ItemData itemToAdd)
    {
        if (itemToAdd == null || !itemToAdd.IsValid) return 0;
        if (IsEmpty) return itemToAdd.maxStack;
        if (item != itemToAdd) return 0;
        return Mathf.Max(0, item.maxStack - count);
    }

    /// <summary>获取此槽位总重量</summary>
    public float GetTotalWeight()
    {
        if (IsEmpty) return 0f;
        return item.weight * count;
    }

    /// <summary>清空槽位</summary>
    public void Clear()
    {
        item = null;
        count = 0;
    }

    /// <summary>设置槽位内容（用于初始化或交换）</summary>
    public void Set(ItemData newItem, int newCount)
    {
        item = newItem;
        count = Mathf.Max(0, newCount);
        if (count <= 0) item = null;
    }
}
