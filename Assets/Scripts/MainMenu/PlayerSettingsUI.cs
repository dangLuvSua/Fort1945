using TMPro;
using UnityEngine;

public class PlayerSettingsUI : MonoBehaviour
{
    [Header("Player Name")]
    [SerializeField] private TMP_InputField playerNameInput;

    [Header("Optional")]
    [SerializeField] private TMP_Text statusText;

    private void OnEnable()
    {
        LoadPlayerName();
    }

    private void LoadPlayerName()
    {
        if (playerNameInput == null)
        {
            Debug.LogError(
                "PlayerSettingsUI: Player Name Input is not assigned."
            );
            return;
        }

        playerNameInput.text = PlayerProfile.PlayerName;
    }

    public void SavePlayerName()
    {
        if (playerNameInput == null)
        {
            return;
        }

        string name = playerNameInput.text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            name = "Player";
            playerNameInput.text = name;
        }

        PlayerProfile.SetPlayerName(name);

        if (statusText != null)
        {
            statusText.text = "Player name saved!";
        }

        Debug.Log($"Player name saved: {name}");
    }
}