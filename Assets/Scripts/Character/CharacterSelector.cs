using UnityEngine;
using TMPro;

public class CharacterSelector : MonoBehaviour
{
    [Header("Characters")]
    [SerializeField] private GameObject[] characters;

    [Header("UI")]
    [SerializeField] private TMP_Text characterNameText;

    [Header("Character Names")]
    [SerializeField] private string[] characterNames;

    [Header("Equip Button")]
    [SerializeField] private GameObject equipButton;

    private int currentIndex = 0;

    private void Start()
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogWarning(
                "CharacterSelector: No characters assigned."
            );
            return;
        }

        LoadSelectedCharacter();
        ShowCharacter(currentIndex);
    }

    private void LoadSelectedCharacter()
    {
        string selectedCharacter =
            PlayerProfile.SelectedCharacter;

        if (selectedCharacter.StartsWith("Character"))
        {
            string numberPart =
                selectedCharacter.Replace("Character", "");

            if (int.TryParse(numberPart, out int characterNumber))
            {
                int index = characterNumber - 1;

                if (index >= 0 &&
                    index < characters.Length)
                {
                    currentIndex = index;
                }
            }
        }
    }

    public void NextCharacter()
    {
        if (characters == null || characters.Length == 0)
            return;

        currentIndex++;

        if (currentIndex >= characters.Length)
            currentIndex = 0;

        ShowCharacter(currentIndex);
    }

    public void PreviousCharacter()
    {
        if (characters == null || characters.Length == 0)
            return;

        currentIndex--;

        if (currentIndex < 0)
            currentIndex = characters.Length - 1;

        ShowCharacter(currentIndex);
    }

    private void ShowCharacter(int index)
    {
        if (characters == null || characters.Length == 0)
            return;

        if (index < 0 || index >= characters.Length)
            return;

        // Show only the current character
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null)
                characters[i].SetActive(i == index);
        }

        // Update character name
        if (characterNameText != null &&
            characterNames != null &&
            index < characterNames.Length)
        {
            characterNameText.text =
                characterNames[index];
        }

        // Update Select Character button
        UpdateEquipButton();
    }

    public void EquipCharacter()
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogWarning(
                "CharacterSelector: No characters available."
            );
            return;
        }

        string characterId =
            "Character" + (currentIndex + 1);

        PlayerProfile.SetSelectedCharacter(
            characterId
        );

        UpdateEquipButton();

        Debug.Log(
            $"Character equipped: {characterId}"
        );
    }

    private void UpdateEquipButton()
    {
        if (equipButton == null)
            return;

        string currentCharacterId =
            "Character" + (currentIndex + 1);

        bool isEquipped =
            PlayerProfile.SelectedCharacter ==
            currentCharacterId;

        // Hide button if this character is already equipped
        equipButton.SetActive(!isEquipped);
    }

    public int GetCurrentCharacterIndex()
    {
        return currentIndex;
    }
}