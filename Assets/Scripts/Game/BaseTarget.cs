using UnityEngine;

/// <summary>
/// Place on the base / crystal root. Registers this transform as the global primary target for <see cref="EnemyBase"/>.
/// </summary>
public class BaseTarget : MonoBehaviour
{
    public static Transform Instance { get; private set; }

    private void Awake()
    {
        Instance = transform;
    }

    private void OnDestroy()
    {
        if (Instance == transform)
            Instance = null;
    }
}
