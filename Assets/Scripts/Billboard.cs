using UnityEngine;

/// <summary>
/// 每帧将本物体世界旋转与 <b>Main Camera</b> 对齐：<c>transform.rotation = Camera.main.transform.rotation</c>。
/// </summary>
public class Billboard : MonoBehaviour
{
    Transform _cam;

    void Start()
    {
        _cam = Camera.main != null ? Camera.main.transform : null;
    }

    void LateUpdate()
    {
        if (_cam == null)
        {
            if (Camera.main != null)
                _cam = Camera.main.transform;
            return;
        }

        transform.rotation = _cam.rotation;
    }
}
