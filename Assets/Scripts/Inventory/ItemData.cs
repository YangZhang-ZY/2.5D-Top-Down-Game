using UnityEngine;

/// <summary>
/// 物品数据定义。使用 ScriptableObject 作为物品模板，可在 Inspector 中配置。
/// 右键 Project 窗口 → Create → Inventory → Item Data 创建新物品。
///
/// 扩展预留：后续可添加 rarity、quality、tags 等字段。
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("唯一标识，用于存档和引用")]
    public string id;

    [Tooltip("显示名称")]
    public string displayName;

    [Tooltip("详细描述，用于物品详情界面")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("物品图标")]
    public Sprite icon;

    [Header("分类与堆叠")]
    [Tooltip("物品类型，用于整理和分类")]
    public ItemType itemType = ItemType.Material;

    [Tooltip("最大堆叠数，1 表示不可堆叠")]
    [Min(1)]
    public int maxStack = 99;

    [Header("负重与经济")]
    [Tooltip("单件重量，用于负重计算")]
    [Min(0f)]
    public float weight = 0.1f;

    [Tooltip("基础售价，卖给商店时的价格")]
    [Min(0)]
    public int basePrice = 1;

    /// <summary>
    /// 验证 id 是否有效（非空）
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(id);
}

/// <summary>
/// 物品类型枚举。用于整理、分类和后续扩展。
/// </summary>
public enum ItemType
{
    Material,   // 材料（怪物掉落、采集等）
    Consumable, // 消耗品（药水、食物等）
    Weapon,     // 武器
    Armor,      // 防具
    Key,        // 钥匙、任务物品
    Other       // 其他
}
