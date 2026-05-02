using UnityEngine;

/// <summary>
/// 挂在 <b>CameraPosition / CameraRig</b> 等架子上：只负责把自身世界坐标跟到 <see cref="target"/>（可带世界偏移与平滑）。
/// <b>不处理旋转</b>；同一物体可再挂 <see cref="PlayerCameraOrbitPivot"/> 做 Q/E 转台。
/// Main Camera 建议作为子物体，上面用 <see cref="SimpleFollowCamera"/>（关 Update Position）调焦点与朝向。
/// </summary>
[DisallowMultipleComponent]
public class CameraFollowTarget : MonoBehaviour
{
    [Tooltip("一般为玩家 Transform。")]
    public Transform target;

    [Tooltip("目标世界位置 + 该偏移 = 架子期望的世界位置。")]
    public Vector3 worldOffset;

    [Tooltip(">0：SmoothDamp 跟随（秒）；0：与目标同步无延迟。")]
    [SerializeField] float smoothTime;

    Vector3 _smoothed;
    Vector3 _velocity;

    void Start()
    {
        if (target != null)
            _smoothed = target.position + worldOffset;
        else
            _smoothed = transform.position;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 goal = target.position + worldOffset;
        if (smoothTime > 0f)
            _smoothed = Vector3.SmoothDamp(_smoothed, goal, ref _velocity, smoothTime);
        else
        {
            _smoothed = goal;
            _velocity = Vector3.zero;
        }

        transform.position = _smoothed;
    }
}
