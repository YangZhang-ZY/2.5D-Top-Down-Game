using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Perspective camera: orbits the target (player) around a vertical axis (Q/E style).
/// Put on Main Camera; assign Target to the player transform.
/// Orbit input: assign Input Action References from InputSystem_Actions → Player → OrbitCameraLeft / OrbitCameraRight.
/// </summary>
public class CameraOrbitFollow : MonoBehaviour
{
    [Header("Follow target")]
    [Tooltip("Usually the player transform.")]
    public Transform target;

    [Tooltip("Added to target position for LookAt (e.g. chest height).")]
    public Vector3 lookAtOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Orbit")]
    [Tooltip("Degrees per second while orbit keys are held.")]
    public float rotateSpeed = 90f;

    [Tooltip("Default Q / E; change bindings in Input Actions.")]
    [SerializeField] private InputActionReference orbitLeftAction;

    [SerializeField] private InputActionReference orbitRightAction;

    [Tooltip("Orbit axis. Use (0,1,0) for XZ ground with Y up; (0,0,1) if gameplay is in the XY plane.")]
    public Vector3 orbitAxis = Vector3.up;

    [Header("Follow")]
    [Tooltip("When true, camera moves with the target while keeping relative orbit offset.")]
    public bool followTargetPosition = true;

    float _yawDegrees;
    Vector3 _offsetLocal;

    private void Awake()
    {
        if ((orbitLeftAction == null || orbitLeftAction.action == null) ||
            (orbitRightAction == null || orbitRightAction.action == null))
            Debug.LogWarning(
                "[CameraOrbitFollow] Assign Orbit Left and Orbit Right Input Action References (InputSystem_Actions → Player).",
                this);
    }

    private void OnEnable()
    {
        if (orbitLeftAction != null && orbitLeftAction.action != null)
            orbitLeftAction.action.Enable();
        if (orbitRightAction != null && orbitRightAction.action != null)
            orbitRightAction.action.Enable();
    }

    private void OnDisable()
    {
        if (orbitLeftAction != null && orbitLeftAction.action != null)
            orbitLeftAction.action.Disable();
        if (orbitRightAction != null && orbitRightAction.action != null)
            orbitRightAction.action.Disable();
    }

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("[CameraOrbitFollow] Target is not assigned.", this);
            return;
        }

        Vector3 axis = orbitAxis.sqrMagnitude > 0.01f ? orbitAxis.normalized : Vector3.up;
        orbitAxis = axis;

        Vector3 worldOffset = transform.position - target.position;
        _yawDegrees = ComputeYawDegrees(worldOffset, axis);
        _offsetLocal = Quaternion.Inverse(Quaternion.AngleAxis(_yawDegrees, axis)) * worldOffset;
    }

    void LateUpdate()
    {
        if (target == null) return;

        float input = 0f;
        if (orbitRightAction != null && orbitRightAction.action != null && orbitRightAction.action.IsPressed())
            input += 1f;
        if (orbitLeftAction != null && orbitLeftAction.action != null && orbitLeftAction.action.IsPressed())
            input -= 1f;

        _yawDegrees += input * rotateSpeed * Time.deltaTime;

        Quaternion orbit = Quaternion.AngleAxis(_yawDegrees, orbitAxis);
        Vector3 worldOffset = orbit * _offsetLocal;

        if (followTargetPosition)
            transform.position = target.position + worldOffset;
        else
            transform.position = worldOffset;

        Vector3 lookPoint = target.position + lookAtOffset;
        Vector3 forward = lookPoint - transform.position;
        if (forward.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(forward.normalized, orbitAxis);
    }

    static float ComputeYawDegrees(Vector3 offset, Vector3 axis)
    {
        if (Vector3.Dot(axis, Vector3.up) > 0.99f)
        {
            Vector3 h = new Vector3(offset.x, 0f, offset.z);
            if (h.sqrMagnitude < 1e-8f) return 0f;
            return Mathf.Atan2(h.x, h.z) * Mathf.Rad2Deg;
        }

        if (Vector3.Dot(axis, Vector3.forward) > 0.99f || Vector3.Dot(axis, Vector3.back) > 0.99f)
        {
            Vector3 h = new Vector3(offset.x, offset.y, 0f);
            if (h.sqrMagnitude < 1e-8f) return 0f;
            return Mathf.Atan2(h.x, h.y) * Mathf.Rad2Deg;
        }

        Vector3 u = axis.normalized;
        Vector3 projected = Vector3.ProjectOnPlane(offset, u);
        if (projected.sqrMagnitude < 1e-8f) return 0f;

        Vector3 refDir = Mathf.Abs(Vector3.Dot(Vector3.forward, u)) > 0.9f ? Vector3.right : Vector3.forward;
        Vector3 t1 = Vector3.Cross(u, refDir);
        if (t1.sqrMagnitude < 1e-8f) t1 = Vector3.Cross(u, Vector3.up);
        t1.Normalize();
        Vector3 t2 = Vector3.Cross(u, t1);

        float x = Vector3.Dot(projected, t1);
        float y = Vector3.Dot(projected, t2);
        return Mathf.Atan2(x, y) * Mathf.Rad2Deg;
    }
}
