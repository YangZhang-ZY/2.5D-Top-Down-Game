using UnityEngine;

/// <summary>
/// 玩家扣血时扣架势的逻辑已并入 <see cref="PlayerController"/>（与格挡、受击入队同顺序）。
/// 保留空组件，避免场景中已挂载的引用被 Unity 剔除。
/// </summary>
[DisallowMultipleComponent]
public class PlayerPostureReactor : MonoBehaviour
{
}
