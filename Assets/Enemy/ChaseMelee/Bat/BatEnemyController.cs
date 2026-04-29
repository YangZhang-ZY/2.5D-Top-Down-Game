using UnityEngine;

/// <summary>Bat variant — uses <see cref="ChaseMeleeEnemyController"/>; extend for flight / swoop later.</summary>
public class BatEnemyController : ChaseMeleeEnemyController
{
#if UNITY_EDITOR
    void Reset()
    {
        logAIStateTransitions = true;
    }
#endif
}
