using Fusion;
using TMPro;
using UnityEngine;

public class SoldierDialogueController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioClip talkSound;
    [SerializeField, Range(0f, 1f)] private float talkSoundVolume = 1f;

    [Header("UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;

    private NetworkRunner runner;
    private NetworkGameManager manager;
    private AudioSource talkAudioSource;

    private void Awake()
    {
        if (talkSound == null)
            talkSound = Resources.Load<AudioClip>("SFX/talk");

        // Dialogue is UI feedback, so play it as 2D audio. The dialogue panel
        // can be far from the local player's AudioListener in world space.
        talkAudioSource = GetComponent<AudioSource>();
        if (talkAudioSource == null)
            talkAudioSource = gameObject.AddComponent<AudioSource>();

        talkAudioSource.playOnAwake = false;
        talkAudioSource.loop = false;
        talkAudioSource.spatialBlend = 0f;

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
        // The dialogue panel can be this component's own GameObject. Activate
        // it first so its AudioSource is active before PlayOneShot is called.
        if (dialoguePanel != null && !dialoguePanel.activeSelf)
            dialoguePanel.SetActive(true);

        if (talkSound != null)
        {
            talkAudioSource.enabled = true;
            talkAudioSource.PlayOneShot(talkSound, talkSoundVolume);
        }
        else
            Debug.LogWarning("[DIALOGUE] Talk SFX is missing. Assign a clip or add Resources/SFX/talk.", this);

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
