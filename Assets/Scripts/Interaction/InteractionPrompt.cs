using TMPro;
using UnityEngine;

public class InteractionPrompt : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;

    [Header("Default Text")]
    [SerializeField] private string defaultMessage = "[E] INTERACT";

    private void Awake()
    {
        Hide();
    }

    // =============================================================
    // SHOW DEFAULT MESSAGE
    // =============================================================

    public void Show()
    {
        Show(defaultMessage);
    }

    // =============================================================
    // SHOW CUSTOM MESSAGE
    // =============================================================

    public void Show(string message)
    {
        if (promptText != null)
        {
            promptText.text = message;
        }

        if (promptRoot != null)
        {
            promptRoot.SetActive(true);
        }
    }

    // =============================================================
    // HIDE
    // =============================================================

    public void Hide()
    {
        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }
    }
}