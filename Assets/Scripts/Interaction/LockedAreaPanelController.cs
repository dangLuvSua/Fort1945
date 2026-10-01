using UnityEngine;
using TMPro;

public class LockedAreaPanelController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Panel Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    private void Awake()
    {
        if (panel == null)
        {
            Debug.LogError(
                "LockedAreaPanelController: Panel is not assigned!",
                this
            );

            return;
        }

        panel.SetActive(false);
    }

    public void ShowPanel(string title, string description)
    {
        if (panel == null)
            return;

        // Change the text based on which trigger was entered
        if (titleText != null)
            titleText.text = title;

        if (descriptionText != null)
            descriptionText.text = description;

        panel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log($"LOCKED AREA PANEL SHOWN: {title}");
    }

    public void HidePanel()
    {
        if (panel == null)
            return;

        panel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("LOCKED AREA PANEL HIDDEN");
    }
}