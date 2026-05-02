using UnityEngine;

/// <summary>
/// 挂在 <b>Main Camera</b>：调 <b>位置</b>（世界跟随或 Local Offset）、<b>旋转</b>（可选对准焦点，平滑可控）、<b>焦点</b>（跟踪点 + 注视偏移）。
/// 有父级 Pivot 且 <see cref="aimAtFocus"/> 时只改俯仰，水平转角由 <see cref="PlayerCameraOrbitPivot"/> 负责。
/// </summary>
[DefaultExecutionOrder(10)]
[DisallowMultipleComponent]
public class SimpleFollowCamera : MonoBehaviour
{
    [Header("Focus")]
    [Tooltip("一般为玩家。未开 Aim At Focus 且未开 Update World Position 且无 Apply Local Offset 时可留空。")]
    public Transform followTarget;

    [Tooltip("跟踪基础点 = 目标位置 + 该偏移。")]
    public Vector3 followPointOffset;

    [Tooltip("注视点 = 平滑后的跟踪点 + 该偏移。")]
    public Vector3 lookAtOffset;

    [Tooltip("对跟踪基础点做 SmoothDamp（秒）；用于 Aim / 世界跟随。")]
    [SerializeField] float positionSmoothTime;

    [Header("Position")]
    [Tooltip("无父物体时：每帧用「跟踪点 + Offset From Focus」写世界坐标。有父物体时自动关闭。")]
    [SerializeField] bool updateWorldPosition;

    [Tooltip("相对跟踪点；世界跟随时作世界或相机空间偏移；勾选 Apply 时作 Local Position。")]
    public Vector3 offsetFromFocus = new Vector3(0f, 0f, -10f);

    [SerializeField] OffsetSpace offsetSpace = OffsetSpace.World;

    public enum OffsetSpace
    {
        World,
        CameraSpace,
    }

    [Tooltip("有父物体且未开世界跟随时：每帧 localPosition = Offset From Focus。")]
    [SerializeField] bool applyOffsetAsLocalPosition;

    [Header("Rotation")]
    [Tooltip("有父物体：只调 Local X 俯仰对准注视点。无父物体：整段 LookAt。")]
    [SerializeField] bool aimAtFocus = true;

    [SerializeField] Vector3 worldUp = Vector3.up;

    [Tooltip("瞄准时旋转平滑时间（秒）；0 = 每帧立刻对准。")]
    [SerializeField] float lensSmoothTime;

    Vector3 _smoothedFollowBase;
    Vector3 _followVelocity;
    float _pitchSmVel;

    void OnValidate()
    {
        if (transform.parent != null)
            updateWorldPosition = false;
    }

    void Awake()
    {
        if (transform.parent != null)
            updateWorldPosition = false;
    }

    void Start()
    {
        bool needsTarget = updateWorldPosition || aimAtFocus;
        if (followTarget == null)
        {
            if (needsTarget)
                Debug.LogWarning("[SimpleFollowCamera] 需要 Follow Target（当前选项依赖目标）。", this);
            WarnIfCinemachineBrainPresent();
            return;
        }

        _smoothedFollowBase = followTarget.position + followPointOffset;
        WarnIfCinemachineBrainPresent();
    }

    void WarnIfCinemachineBrainPresent()
    {
        foreach (var b in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (b == null || !b.enabled) continue;
            if (b.GetType().Name == "CinemachineBrain")
            {
                Debug.LogWarning(
                    "[SimpleFollowCamera] 检测到 CinemachineBrain，会与自定义相机抢行。",
                    this);
                return;
            }
        }
    }

    void LateUpdate()
    {
        bool needsTargetForFraming = followTarget != null && (updateWorldPosition || aimAtFocus);
        if (needsTargetForFraming)
        {
            Vector3 rawBase = followTarget.position + followPointOffset;
            if (positionSmoothTime > 0f)
                _smoothedFollowBase = Vector3.SmoothDamp(
                    _smoothedFollowBase, rawBase, ref _followVelocity, positionSmoothTime);
            else
            {
                _smoothedFollowBase = rawBase;
                _followVelocity = Vector3.zero;
            }
        }

        Vector3 aimPoint = _smoothedFollowBase + lookAtOffset;

        if (updateWorldPosition && followTarget != null)
        {
            if (offsetSpace == OffsetSpace.CameraSpace && transform.parent == null && aimAtFocus)
            {
                ApplyWorldRotationTowards(aimPoint);
                transform.position = _smoothedFollowBase + transform.TransformDirection(offsetFromFocus);
                ApplyWorldRotationTowards(aimPoint);
            }
            else if (offsetSpace == OffsetSpace.CameraSpace)
            {
                transform.position = _smoothedFollowBase + transform.TransformDirection(offsetFromFocus);
            }
            else
            {
                transform.position = _smoothedFollowBase + offsetFromFocus;
                if (transform.parent == null && aimAtFocus)
                    ApplyWorldRotationTowards(aimPoint);
            }
        }

        if (applyOffsetAsLocalPosition && transform.parent != null && !updateWorldPosition)
            transform.localPosition = offsetFromFocus;

        if (aimAtFocus && followTarget != null)
        {
            if (!(updateWorldPosition && transform.parent == null && aimAtFocus))
                AimAtWorldPoint(aimPoint);
        }
    }

    void ApplyWorldRotationTowards(Vector3 worldPoint)
    {
        Vector3 toFocus = worldPoint - transform.position;
        if (toFocus.sqrMagnitude < 1e-8f) return;
        Quaternion target = Quaternion.LookRotation(toFocus.normalized, worldUp);
        if (lensSmoothTime <= 0f)
            transform.rotation = target;
        else
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                target,
                Mathf.Clamp01(Time.deltaTime / lensSmoothTime));
    }

    void AimAtWorldPoint(Vector3 worldPoint)
    {
        Vector3 toFocus = worldPoint - transform.position;
        if (toFocus.sqrMagnitude < 1e-8f) return;

        Transform p = transform.parent;
        if (p != null)
        {
            Vector3 parentUp = p.up;
            Vector3 horiz = Vector3.ProjectOnPlane(toFocus, parentUp);
            Vector3 axis = p.right;
            float targetPitch;
            if (horiz.sqrMagnitude < 1e-8f)
            {
                targetPitch = Vector3.Dot(toFocus.normalized, parentUp) > 0f ? 89f : -89f;
            }
            else
            {
                horiz.Normalize();
                targetPitch = Vector3.SignedAngle(horiz, toFocus.normalized, axis);
            }

            var e = transform.localEulerAngles;
            if (lensSmoothTime <= 0f)
                e.x = targetPitch;
            else
                e.x = Mathf.SmoothDampAngle(e.x, targetPitch, ref _pitchSmVel, lensSmoothTime);
            e.y = 0f;
            e.z = 0f;
            transform.localEulerAngles = e;
        }
        else
        {
            ApplyWorldRotationTowards(worldPoint);
        }
    }
}
