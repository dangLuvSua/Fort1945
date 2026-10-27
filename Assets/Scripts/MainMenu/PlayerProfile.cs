using UnityEngine;

public static class PlayerProfile
{
    private const string PlayerNameKey = "PlayerName";
    private const string SelectedCharacterKey = "SelectedCharacter";

    // Keep this process's profile across scene changes. ParrelSync editors can
    // share PlayerPrefs, so rereading them on spawn can pick up another player.
    private static string cachedPlayerName;
    private static string cachedSelectedCharacter;


    // =====================================================
    // GET PLAYER NAME
    // =====================================================

    public static string PlayerName
    {
        get
        {
            return cachedPlayerName ?? (cachedPlayerName = PlayerPrefs.GetString(
                PlayerNameKey,
                "Player"
            ));
        }
    }


    // =====================================================
    // SAVE PLAYER NAME
    // =====================================================

    public static void SetPlayerName(string name)
    {
        name = name.Trim();

        if (string.IsNullOrEmpty(name))
        {
            name = "Player";
        }

        cachedPlayerName = name;

        PlayerPrefs.SetString(
            PlayerNameKey,
            name
        );

        PlayerPrefs.Save();

        Debug.Log(
            $"Player name saved: {name}"
        );
    }


    // =====================================================
    // GET SELECTED CHARACTER
    // =====================================================

    public static string SelectedCharacter
    {
        get
        {
            return cachedSelectedCharacter ?? (cachedSelectedCharacter = PlayerPrefs.GetString(
                SelectedCharacterKey,
                "Character1"
            ));
        }
    }


    // =====================================================
    // SAVE SELECTED CHARACTER
    // =====================================================

    public static void SetSelectedCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            characterId = "Character1";
        }

        cachedSelectedCharacter = characterId;

        PlayerPrefs.SetString(
            SelectedCharacterKey,
            characterId
        );

        PlayerPrefs.Save();

        Debug.Log(
            $"Character equipped: {characterId}"
        );
    }
}
