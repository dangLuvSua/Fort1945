using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerListPanelController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject playerListPanel;

    [Header("Settings")]
    [SerializeField] private bool startsOpen = false;

    private bool isOpen;


    // =====================================================
    // UNITY
    // =====================================================

    private void Awake()
    {
        isOpen = startsOpen;

        if (playerListPanel != null)
        {
            playerListPanel.SetActive(isOpen);
        }
        else
        {
            Debug.LogWarning(
                "[PLAYER LIST] Player List Panel is not assigned."
            );
        }
    }


    private void Update()
    {
        // New Unity Input System
        if (Keyboard.current != null &&
            Keyboard.current.tabKey.wasPressedThisFrame)
        {
            TogglePlayerList();
        }
    }


    // =====================================================
    // TOGGLE
    // =====================================================

    private void TogglePlayerList()
    {
        isOpen = !isOpen;

        if (playerListPanel != null)
        {
            playerListPanel.SetActive(isOpen);
        }

        Debug.Log(
            $"[PLAYER LIST] " +
            $"Panel {(isOpen ? "OPENED" : "CLOSED")}"
        );
    }


    // =====================================================
    // PUBLIC
    // =====================================================

    public void Open()
    {
        isOpen = true;

        if (playerListPanel != null)
        {
            playerListPanel.SetActive(true);
        }
    }


    public void Close()
    {
        isOpen = false;

        if (playerListPanel != null)
        {
            playerListPanel.SetActive(false);
        }
    }


    public void Toggle()
    {
        TogglePlayerList();
    }


    public bool IsOpen()
    {
        return isOpen;
    }
}