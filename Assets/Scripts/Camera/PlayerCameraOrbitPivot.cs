using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// <b>「相机当玩家子物体」+ 《饥荒》式水平旋转：</b>可挂在 <c>Player → CameraRig</c>，或与 <see cref="CameraFollowTarget"/> 同挂在独立的 <c>CameraPosition</c> 上。
/// 本物体只做<b>水平</b>步进旋转；俯仰角只做在 <b>子物体 Main Camera 的 Local Euler X</b>。详见同目录 <c>CAMERA_RIG_SETUP.txt</c>。
/// 输入：两个 <see cref="InputActionReference"/>（Button），或勾选 <see cref="useDirectKeys"/>。
/// 关掉 Main Camera 上的 <b>CinemachineBrain</b>；<see cref="SimpleFollowCamera"/> 应对带 Pivot 的子相机使用「仅俯仰」对准，水平_yaw 由本组件负责。
/// </summary>
[DefaultExecutionOrder(-50)]
public class PlayerCameraOrbitPivot : MonoBehaviour
{
    public enum PivotYawSpace
    {
        [Tooltip("绕本地 Y（父物体有旋转时与世界 Y 不同）。")]
        LocalY = 0,
        [Tooltip("绕世界 Y：地面在 XZ、竖直为 Y 时用（常见 3D 斜俯视）。")]
        WorldY = 1,
        [Tooltip("绕本地 Z：地面在 XY（2D）时常用，Pivot 为玩家子物体时像在水平面上环视。")]
        LocalZ = 2,
        [Tooltip("绕世界 Z：与 LocalZ 在玩家无倾斜时效果接近。")]
        WorldZ = 3
    }

    public enum GroundPlanePreset
    {
        [Tooltip("XZ 地面、Y 朝上 → 用 WorldY。")]
        XZ_VerticalY = 0,
        [Tooltip("XY 地面（2D）→ 用 LocalZ。")]
        XY_VerticalZ = 1,
        [Tooltip("在下面 Yaw Space 里自选。")]
        Custom = 2
    }

    [Tooltip("要转的物体；留空 = 本物体（应挂在你命名的 CameraPivot 上）。")]
    [SerializeField] Transform pivot;

    [Tooltip("一键匹配行走平面；选 Custom 时用 Yaw Space。")]
    [SerializeField] GroundPlanePreset groundPlane = GroundPlanePreset.XZ_VerticalY;

    [SerializeField] PivotYawSpace yawSpace = PivotYawSpace.WorldY;

    [SerializeField] float stepAngleDegrees = 45f;

    [SerializeField] bool smoothRotation;

    [SerializeField] float smoothRotateSpeed = 360f;

    [SerializeField] InputActionReference rotateCounterClockwise;

    [SerializeField] InputActionReference rotateClockwise;

    [Header("Optional (debug / fallback)")]
    [SerializeField] bool useDirectKeys;

    [SerializeField] Key directCounterClockwise = Key.Q;

    [SerializeField] Key directClockwise = Key.R;

    float _visualYaw;
    float _targetYaw;
    bool _warnedMissingPivot;

    Transform Pivot => pivot != null ? pivot : transform;

    void OnValidate()
    {
        switch (groundPlane)
        {
            case GroundPlanePreset.XZ_VerticalY:
                yawSpace = PivotYawSpace.WorldY;
                break;
            case GroundPlanePreset.XY_VerticalZ:
                yawSpace = PivotYawSpace.LocalZ;
                break;
        }
    }

    void Awake()
    {
        if (pivot == null)
            pivot = transform;

        OnValidate();
        InitYawFromPivot();
    }

    void InitYawFromPivot()
    {
        _visualYaw = _targetYaw = ReadPivotAngle();
    }

    float ReadPivotAngle()
    {
        switch (yawSpace)
        {
            case PivotYawSpace.LocalY:
                return Pivot.localEulerAngles.y;
            case PivotYawSpace.WorldY:
                return Pivot.eulerAngles.y;
            case PivotYawSpace.LocalZ:
                return Pivot.localEulerAngles.z;
            case PivotYawSpace.WorldZ:
                return Pivot.eulerAngles.z;
            default:
                return Pivot.eulerAngles.y;
        }
    }

    void OnEnable()
    {
        if (rotateCounterClockwise != null && rotateCounterClockwise.action != null)
            rotateCounterClockwise.action.Enable();
        if (rotateClockwise != null && rotateClockwise.action != null)
            rotateClockwise.action.Enable();
    }

    void OnDisable()
    {
        if (rotateCounterClockwise != null && rotateCounterClockwise.action != null)
            rotateCounterClockwise.action.Disable();
        if (rotateClockwise != null && rotateClockwise.action != null)
            rotateClockwise.action.Disable();
    }

    void Start()
    {
        if (!useDirectKeys &&
            (rotateCounterClockwise == null || rotateCounterClockwise.action == null ||
             rotateClockwise == null || rotateClockwise.action == null))
        {
            Debug.LogWarning(
                "[PlayerCameraOrbitPivot] 未指定两个 Input Action Reference，且未勾选 Use Direct Keys — 按下不会旋转。",
                this);
        }
    }

    bool IsCounterThisFrame()
    {
        if (useDirectKeys && Keyboard.current != null)
            return Keyboard.current[directCounterClockwise].wasPressedThisFrame;

        return rotateCounterClockwise != null && rotateCounterClockwise.action != null &&
               rotateCounterClockwise.action.WasPressedThisFrame();
    }

    bool IsClockwiseThisFrame()
    {
        if (useDirectKeys && Keyboard.current != null)
            return Keyboard.current[directClockwise].wasPressedThisFrame;

        return rotateClockwise != null && rotateClockwise.action != null &&
               rotateClockwise.action.WasPressedThisFrame();
    }

    void Update()
    {
        if (PlayerInputBlocker.IsBlocked)
            return;

        if (IsCounterThisFrame())
        {
            _targetYaw -= stepAngleDegrees;
            if (!smoothRotation)
                _visualYaw = _targetYaw;
        }

        if (IsClockwiseThisFrame())
        {
            _targetYaw += stepAngleDegrees;
            if (!smoothRotation)
                _visualYaw = _targetYaw;
        }
    }

    void LateUpdate()
    {
        if (Pivot == null)
        {
            if (!_warnedMissingPivot)
            {
                Debug.LogWarning("[PlayerCameraOrbitPivot] Pivot 丢失。", this);
                _warnedMissingPivot = true;
            }
            return;
        }

        if (smoothRotation)
        {
            _visualYaw = Mathf.MoveTowardsAngle(_visualYaw, _targetYaw, smoothRotateSpeed * Time.deltaTime);
            ApplyYaw(_visualYaw);
        }
        else
        {
            _visualYaw = _targetYaw;
            ApplyYaw(_visualYaw);
        }
    }

    void ApplyYaw(float yawDegrees)
    {
        switch (yawSpace)
        {
            case PivotYawSpace.LocalY:
            {
                Vector3 el = Pivot.localEulerAngles;
                el.y = yawDegrees;
                Pivot.localEulerAngles = el;
                break;
            }
            case PivotYawSpace.WorldY:
            {
                Vector3 e = Pivot.eulerAngles;
                e.y = yawDegrees;
                Pivot.eulerAngles = e;
                break;
            }
            case PivotYawSpace.LocalZ:
            {
                Vector3 el = Pivot.localEulerAngles;
                el.z = yawDegrees;
                Pivot.localEulerAngles = el;
                break;
            }
            case PivotYawSpace.WorldZ:
            {
                Vector3 e = Pivot.eulerAngles;
                e.z = yawDegrees;
                Pivot.eulerAngles = e;
                break;
            }
        }
    }
}
