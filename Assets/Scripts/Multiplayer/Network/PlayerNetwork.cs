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

    [Networked, Capacity(32)]
    public string PlayerName { get; set; }

    private GameObject currentCharacter;

    private void Awake()
    {
        if (player == null)
        {
            player = transform;
        }
    }

    public override void Spawned()
    {
        Debug.Log(
            $"[PLAYER NETWORK] Spawned for {Object.InputAuthority}"
        );

        // Only the local player reads their local PlayerPrefs.
        if (Object.HasInputAuthority)
        {
            SetLocalPlayerData();
        }

        SetupCamera();

        UpdateCharacter();
    }

    private void SetLocalPlayerData()
    {
        // -------------------------
        // PLAYER NAME
        // -------------------------

        string localPlayerName =
            PlayerProfile.PlayerName;

        if (string.IsNullOrWhiteSpace(localPlayerName))
        {
            localPlayerName = "Player";
        }

        // Host can directly set its own data.
        if (Object.HasStateAuthority)
        {
            PlayerName = localPlayerName;
        }
        else
        {
            RPC_SetPlayerName(localPlayerName);
        }


        // -------------------------
        // SELECTED CHARACTER
        // -------------------------

        int selectedCharacterIndex =
            GetSavedCharacterIndex();

        Debug.Log(
            $"[PLAYER NETWORK] " +
            $"Local selected character index: {selectedCharacterIndex}"
        );

        // Host can directly set its own data.
        if (Object.HasStateAuthority)
        {
            CharacterIndex =
                selectedCharacterIndex;
        }
        else
        {
            RPC_SetCharacter(
                selectedCharacterIndex
            );
        }
    }

    private int GetSavedCharacterIndex()
    {
        string selectedCharacter =
            PlayerProfile.SelectedCharacter;

        Debug.Log(
            $"[PLAYER NETWORK] " +
            $"Saved Character: {selectedCharacter}"
        );

        if (selectedCharacter.StartsWith("Character"))
        {
            string numberPart =
                selectedCharacter.Replace(
                    "Character",
                    ""
                );

            if (int.TryParse(
                    numberPart,
                    out int characterNumber))
            {
                int index =
                    characterNumber - 1;

                if (index >= 0 &&
                    index < characterPrefabs.Length)
                {
                    return index;
                }
            }
        }

        // Default to Character 1
        return 0;
    }


    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    private void RPC_SetCharacter(
        int characterIndex
    )
    {
        if (characterIndex < 0 ||
            characterIndex >= characterPrefabs.Length)
        {
            Debug.LogWarning(
                $"[PLAYER NETWORK] Invalid character index: " +
                $"{characterIndex}"
            );

            return;
        }

        CharacterIndex =
            characterIndex;

        Debug.Log(
            $"[PLAYER NETWORK] " +
            $"Character set to index {characterIndex}"
        );
    }


    [Rpc(
        RpcSources.InputAuthority,
        RpcTargets.StateAuthority
    )]
    private void RPC_SetPlayerName(
        string playerName
    )
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Player";
        }

        PlayerName =
            playerName;

        Debug.Log(
            $"[PLAYER NETWORK] " +
            $"Player name set to: {PlayerName}"
        );
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

        GameObject selectedPrefab =
            characterPrefabs[CharacterIndex];

        if (selectedPrefab == null)
        {
            Debug.LogWarning(
                $"[PLAYER NETWORK] " +
                $"Character prefab at index " +
                $"{CharacterIndex} is missing!"
            );

            return;
        }

        // Already showing correct character
        if (currentCharacter != null &&
            currentCharacter.name.StartsWith(
                selectedPrefab.name
            ))
        {
            return;
        }

        // Remove previous character
        if (currentCharacter != null)
        {
            Destroy(currentCharacter);
            currentCharacter = null;
        }

        // Create selected character
        currentCharacter =
            Instantiate(
                selectedPrefab,
                player
            );

        currentCharacter.transform.localPosition =
            Vector3.zero;

        currentCharacter.transform.localRotation =
            Quaternion.identity;

        currentCharacter.transform.localScale =
            Vector3.one;

        bool hideCharacter =
            Object.HasInputAuthority;

        foreach (Renderer characterRenderer in
                 currentCharacter.GetComponentsInChildren<Renderer>(true))
        {
            characterRenderer.enabled =
                !hideCharacter;
        }

        Debug.Log(
            $"[PLAYER NETWORK] " +
            $"Displaying character: " +
            $"{selectedPrefab.name}"
        );
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

        if (characterPrefabs == null ||
            index < 0 ||
            index >= characterPrefabs.Length)
        {
            Debug.LogWarning(
                $"[PLAYER NETWORK] " +
                $"Invalid character index: {index}"
            );

            return;
        }

        CharacterIndex =
            index;
    }
}