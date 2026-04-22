using UnityEngine;

/// <summary>
/// One inventory stack: item reference and count.
/// </summary>
[System.Serializable]
public class InventorySlot
{
    [Tooltip("Item template; null when empty.")]
    public ItemData item;

    [Tooltip("Stack count.")]
    [Min(0)]
    public int count;

    /// <summary>True when there is no valid stack.</summary>
    public bool IsEmpty => item == null || count <= 0;

    /// <summary>Whether more of itemToAdd can merge into this slot.</summary>
    public bool CanAddMore(ItemData itemToAdd)
    {
        if (itemToAdd == null || !itemToAdd.IsValid) return false;
        if (IsEmpty) return true;
        if (item != itemToAdd) return false;
        return count < item.maxStack;
    }

    /// <summary>How many more of itemToAdd fit here; empty slot returns maxStack for that item.</summary>
    public int RemainingStackSpace(ItemData itemToAdd)
    {
        if (itemToAdd == null || !itemToAdd.IsValid) return 0;
        if (IsEmpty) return itemToAdd.maxStack;
        if (item != itemToAdd) return 0;
        return Mathf.Max(0, item.maxStack - count);
    }

    /// <summary>Total weight of this stack.</summary>
    public float GetTotalWeight()
    {
        if (IsEmpty) return 0f;
        return item.weight * count;
    }

    /// <summary>Clears the slot.</summary>
    public void Clear()
    {
        item = null;
        count = 0;
    }

    /// <summary>Sets stack contents (used when sorting / swapping).</summary>
    public void Set(ItemData newItem, int newCount)
    {
        item = newItem;
        count = Mathf.Max(0, newCount);
        if (count <= 0) item = null;
    }
}
