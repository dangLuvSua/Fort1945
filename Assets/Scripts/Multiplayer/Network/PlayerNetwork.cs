using UnityEngine;
using Fusion;

public class PlayerNetwork : NetworkBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Available Characters")]
    [SerializeField] private GameObject[] characterPrefabs;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;

    [SerializeField] private AudioListener audioListener;

    [Networked]
    public int CharacterIndex { get; set; }

    private GameObject currentCharacter;

    public override void Spawned()
    {
        UpdateCharacter();
        SetupCamera();
    }

    public override void Render()
    {
        UpdateCharacter();
    }

    private void UpdateCharacter()
    {
        if (characterPrefabs == null ||
            characterPrefabs.Length == 0)
        {
            return;
        }

        if (CharacterIndex < 0 ||
            CharacterIndex >= characterPrefabs.Length)
        {
            return;
        }

        // Already showing the correct character
        if (currentCharacter != null &&
            currentCharacter.name.StartsWith(
                characterPrefabs[CharacterIndex].name))
        {
            return;
        }

        // Remove previous visual
        if (currentCharacter != null)
        {
            Destroy(currentCharacter);
            currentCharacter = null;
        }

        // Create selected character
        currentCharacter = Instantiate(
            characterPrefabs[CharacterIndex],
            player
        );

        currentCharacter.transform.localPosition =
            Vector3.zero;

        currentCharacter.transform.localRotation =
            Quaternion.identity;

        currentCharacter.transform.localScale =
            Vector3.one;
    }

    private void SetupCamera()
    {
        bool isLocalPlayer =
            Object.HasInputAuthority;

        if (playerCamera != null)
        {
            playerCamera.enabled =
                isLocalPlayer;
        }

        if (audioListener != null)
        {
            audioListener.enabled =
                isLocalPlayer;
        }
    }

    public void SetCharacter(int index)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (index < 0 ||
            index >= characterPrefabs.Length)
        {
            Debug.LogWarning(
                $"Invalid character index: {index}"
            );

            return;
        }

        CharacterIndex = index;
    }
}