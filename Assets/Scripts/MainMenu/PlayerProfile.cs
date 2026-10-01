using UnityEngine;

public static class PlayerProfile
{
    private const string PlayerNameKey = "PlayerName";

    public static string PlayerName
    {
        get
        {
            return PlayerPrefs.GetString(
                PlayerNameKey,
                "Player"
            );
        }
    }

    public static void SetPlayerName(string name)
    {
        name = name.Trim();

        if (string.IsNullOrEmpty(name))
        {
            name = "Player";
        }

        PlayerPrefs.SetString(
            PlayerNameKey,
            name
        );

        PlayerPrefs.Save();

        Debug.Log($"Player name saved: {name}");
    }
}