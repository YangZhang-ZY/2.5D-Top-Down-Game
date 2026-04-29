using System.Collections.Generic;

/// <summary>
/// Global player input lock registry.
/// Any UI can Request/Release; player input stays blocked while at least one requester is active.
/// </summary>
public static class PlayerInputBlocker
{
    static readonly HashSet<object> Requesters = new HashSet<object>();

    public static bool IsBlocked => Requesters.Count > 0;

    public static void Request(object requester)
    {
        if (requester == null) return;
        Requesters.Add(requester);
    }

    public static void Release(object requester)
    {
        if (requester == null) return;
        Requesters.Remove(requester);
    }
}
