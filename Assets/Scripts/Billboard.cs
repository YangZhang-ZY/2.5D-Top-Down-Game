using UnityEngine;

/// <summary>
/// Faces this transform toward the camera (billboard). Typical for 2.5D sprites or meshes.
/// Do not use on ground tiles.
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
