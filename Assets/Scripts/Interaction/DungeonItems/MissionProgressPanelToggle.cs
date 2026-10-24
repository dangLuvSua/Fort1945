using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Local Caps Lock toggle for the mission progress panel.</summary>
public sealed class MissionProgressPanelToggle : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.capsLockKey.wasPressedThisFrame)
            MissionProgressPanelUI.ToggleVisibility();
    }
}
