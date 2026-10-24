using UnityEngine;
using UnityEngine.InputSystem;

public class InGameOptionsController : MonoBehaviour
{
    public static InGameOptionsController Instance { get; private set; }

    [Header("Options Canvas Reference")]
    [SerializeField] private GameObject optionsCanvas;

    [Header("Optional Settings Button on HUD")]
    [SerializeField] private GameObject hudSettingsButton;

    public bool IsOpen => optionsCanvas != null && optionsCanvas.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (optionsCanvas != null)
        {
            optionsCanvas.SetActive(false);
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleOptions();
        }
    }

    public void ToggleOptions()
    {
        if (IsOpen)
        {
            CloseOptions();
        }
        else
        {
            OpenOptions();
        }
    }

    public void OpenOptions()
    {
        if (optionsCanvas != null)
        {
            optionsCanvas.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (hudSettingsButton != null)
        {
            hudSettingsButton.SetActive(false);
        }
    }

    public void CloseOptions()
    {
        if (optionsCanvas != null)
        {
            optionsCanvas.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (hudSettingsButton != null)
        {
            hudSettingsButton.SetActive(true);
        }
    }
}
