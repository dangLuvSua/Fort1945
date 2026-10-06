using UnityEngine;

/// <summary>
/// Shared spawn-point utility methods used by both
/// PlayerSpawner (Fort 2) and DungeonPlayerSpawner (Dungeon scene).
///
/// Eliminates the near-identical TryGetGroundPosition implementations
/// that existed independently in each spawner.
/// </summary>
public static class SpawnUtils
{
    // =========================================================
    // GROUND DETECTION
    // =========================================================

    /// <summary>
    /// Raycasts downward from <paramref name="origin"/> + upward height offset
    /// to find the nearest ground surface. Falls back to
    /// <paramref name="origin"/> + height offset when no hit is found.
    /// </summary>
    /// <param name="origin">World-space spawn point centre.</param>
    /// <param name="checkHeight">
    ///     How far above <paramref name="origin"/> the ray starts.
    /// </param>
    /// <param name="checkDistance">Maximum downward ray length.</param>
    /// <param name="heightOffset">
    ///     Added to the hit point so the player stands on the surface
    ///     rather than clipping through it.
    /// </param>
    /// <returns>
    ///     The best world-space position with the player above the ground.
    /// </returns>
    public static Vector3 GetGroundPosition(
        Vector3 origin,
        float checkHeight    = 10f,
        float checkDistance  = 50f,
        float heightOffset   = 1.1f)
    {
        Vector3 rayOrigin =
            origin +
            Vector3.up * checkHeight;


        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                checkDistance,
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            return
                hit.point +
                Vector3.up * heightOffset;
        }


        // Fallback: no geometry below spawn point.
        return
            origin +
            Vector3.up * heightOffset;
    }
}

