using TMPro;
using UnityEngine;

public class SoldierDialogueController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Speaker")]
    [SerializeField] private string speakerName = "Soldier";

    private void Awake()
    {
        HideDialogue();
    }

    public void ShowDialogue(string message, float duration = 0f)
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        if (speakerNameText != null)
            speakerNameText.text = speakerName;

        if (dialogueText != null)
            dialogueText.text = message;

        Debug.Log("[DIALOGUE] " + speakerName + ": " + message);

        CancelInvoke(nameof(HideDialogue));

        if (duration > 0f)
            Invoke(nameof(HideDialogue), duration);
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }
}