using UnityEngine;

/// <summary>
/// ScriptableObject template for an item. Create via Project → Create → Inventory → Item Data.
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique id for saves and lookups.")]
    public string id;

    [Tooltip("Display name.")]
    public string displayName;

    [Tooltip("Long description for tooltips / detail UI.")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Inventory icon.")]
    public Sprite icon;

    [Header("Stacking")]
    [Tooltip("Category for sorting.")]
    public ItemType itemType = ItemType.Material;

    [Tooltip("Max stack size; 1 = not stackable.")]
    [Min(1)]
    public int maxStack = 99;

    [Header("Economy")]
    [Tooltip("Weight per unit for carry limit.")]
    [Min(0f)]
    public float weight = 0.1f;

    [Tooltip("Base sell value to shops.")]
    [Min(0)]
    public int basePrice = 1;

    /// <summary>True when id is non-empty.</summary>
    public bool IsValid => !string.IsNullOrEmpty(id);
}

/// <summary>High-level item categories.</summary>
public enum ItemType
{
    Material,
    Consumable,
    Weapon,
    Armor,
    Key,
    Other
}
