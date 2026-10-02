using UnityEngine;

public static class PlayerProfile
{
    private const string PlayerNameKey = "PlayerName";
    private const string SelectedCharacterKey = "SelectedCharacter";


    // =====================================================
    // GET PLAYER NAME
    // =====================================================

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
            return PlayerPrefs.GetString(
                SelectedCharacterKey,
                "Character1"
            );
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