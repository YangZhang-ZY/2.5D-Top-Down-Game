using UnityEngine;

/// <summary>
/// 让物体始终朝向摄像机（Billboard）。适用于 2.5D 场景中的 Sprite、3D 模型等。
/// 地面不需要挂此脚本。
/// </summary>
public class Billboard : MonoBehaviour
{
    private Transform _cam;

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

        transform.forward = _cam.forward;
    }
}
