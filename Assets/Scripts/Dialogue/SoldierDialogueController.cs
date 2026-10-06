using Fusion;
using TMPro;
using UnityEngine;

public class SoldierDialogueController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;

    private NetworkRunner runner;
    private NetworkGameManager manager;

    private void Awake()
    {
        HideDialogue();
    }

    private void Update()
    {
        if (manager == null)
            manager = NetworkGameManager.Instance;

        if (manager == null)
            return;

        // Hide dialogue when the soldier starts walking
        if (manager.SoldierStarted)
        {
            HideDialogue();
        }
    }

    public void ShowDialogue(string message, float duration = 0f)
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        // Get host name
        string hostName = GetHostName();

        if (speakerNameText != null)
            speakerNameText.text = hostName;

        if (dialogueText != null)
            dialogueText.text = message;

        Debug.Log(
            $"[DIALOGUE] {hostName}: {message}"
        );

        CancelInvoke(nameof(HideDialogue));

        if (duration > 0f)
            Invoke(nameof(HideDialogue), duration);
    }

    private string GetHostName()
    {
        if (runner == null)
            runner = FindAnyObjectByType<NetworkRunner>();

        NetworkGameManager manager =
            NetworkGameManager.Instance;

        if (runner == null || manager == null)
            return "Player";

        PlayerRef hostPlayer =
            manager.HostPlayer;

        if (!hostPlayer.IsValid)
            return "Player";

        if (!runner.TryGetPlayerObject(
            hostPlayer,
            out NetworkObject hostObject))
        {
            return "Player";
        }

        LobbyPlayerState hostState =
            hostObject.GetComponent<LobbyPlayerState>();

        if (hostState == null)
            return "Player";

        string hostName =
            hostState.PlayerName.ToString();

        if (string.IsNullOrWhiteSpace(hostName))
            hostName = "Player";

        return hostName;
    }

    public void HideDialogue()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }
}