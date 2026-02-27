using UnityEngine;

/// <summary>
/// 让物体始终朝向摄像机（Billboard）。适用于 2.5D 场景中的 Sprite、3D 模型等。
/// 地面不需要挂此脚本。
/// </summary>
public class Billboard : MonoBehaviour
{
    [Header("轴向")]
    [Tooltip("勾选后只绕世界 Y 轴旋转（适合俯视，物体不倾斜）")]
    public bool lockVertical = true;

    private Transform _cam;
    private Vector3 _camForward;

    private void Start()
    {
        _cam = Camera.main != null ? Camera.main.transform : null;
    }

    private void LateUpdate()
    {
        if (_cam == null)
        {
            if (Camera.main != null) _cam = Camera.main.transform;
            return;
        }

        _camForward = _cam.forward;

        if (lockVertical)
        {
            _camForward.y = 0f;
            if (_camForward.sqrMagnitude < 0.01f) return;
            _camForward.Normalize();
        }

        transform.forward = _camForward;
    }
}
