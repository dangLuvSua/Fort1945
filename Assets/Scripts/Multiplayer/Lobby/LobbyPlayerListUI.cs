using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class LobbyPlayerListUI : MonoBehaviour
{
    [Header("Player List")]
    [SerializeField] private Transform content;

    [SerializeField] private LobbyPlayerItem playerItemPrefab;

    [Header("Character Avatars")]
    [SerializeField] private Sprite[] characterAvatars;


    private NetworkRunner runner;

    private readonly Dictionary<
        PlayerRef,
        LobbyPlayerItem
    > playerItems =
        new Dictionary<PlayerRef, LobbyPlayerItem>();


    private void Update()
    {
        FindRunner();

        if (runner == null)
            return;

        UpdatePlayerList();
    }


    private void FindRunner()
    {
        if (runner != null)
            return;

        runner =
            FindAnyObjectByType<NetworkRunner>();
    }


    private void UpdatePlayerList()
    {
        if (content == null)
            return;

        if (playerItemPrefab == null)
        {
            Debug.LogError(
                "[LOBBY PLAYER LIST] " +
                "Player Item Prefab is not assigned!"
            );

            return;
        }


        HashSet<PlayerRef> activePlayers =
            new HashSet<PlayerRef>(
                runner.ActivePlayers
            );


        // ==========================================
        // CREATE PLAYER ITEMS
        // ==========================================

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (playerItems.ContainsKey(player))
                continue;


            if (!runner.TryGetPlayerObject(
                player,
                out NetworkObject playerObject))
            {
                // Player exists, but its PlayerObject
                // is not available yet.
                continue;
            }


            LobbyPlayerState playerState =
                playerObject.GetComponent<LobbyPlayerState>();


            if (playerState == null)
            {
                Debug.LogWarning(
                    $"[LOBBY PLAYER LIST] " +
                    $"LobbyPlayerState missing on {player}."
                );

                continue;
            }


            LobbyPlayerItem item =
                Instantiate(
                    playerItemPrefab,
                    content
                );


            Sprite avatar =
                GetCharacterAvatar(
                    playerState.SelectedCharacter.ToString()
                );


            item.Setup(
                playerState,
                avatar
            );


            playerItems.Add(
                player,
                item
            );


            Debug.Log(
                $"[LOBBY PLAYER LIST] " +
                $"Added: {playerState.PlayerName}"
            );
        }


        // ==========================================
        // UPDATE EXISTING ITEMS
        // ==========================================

        foreach (
            KeyValuePair<PlayerRef, LobbyPlayerItem> entry
            in playerItems
        )
        {
            PlayerRef player =
                entry.Key;

            LobbyPlayerItem item =
                entry.Value;


            if (!runner.TryGetPlayerObject(
                player,
                out NetworkObject playerObject))
            {
                continue;
            }


            LobbyPlayerState playerState =
                playerObject.GetComponent<LobbyPlayerState>();


            if (playerState == null)
                continue;


            Sprite avatar =
                GetCharacterAvatar(
                    playerState.SelectedCharacter.ToString()
                );


            item.UpdateDisplay(
                avatar
            );
        }


        // ==========================================
        // REMOVE PLAYERS WHO LEFT
        // ==========================================

        List<PlayerRef> playersToRemove =
            new List<PlayerRef>();


        foreach (
            KeyValuePair<PlayerRef, LobbyPlayerItem> entry
            in playerItems
        )
        {
            if (!activePlayers.Contains(entry.Key))
            {
                playersToRemove.Add(
                    entry.Key
                );
            }
        }


        foreach (PlayerRef player in playersToRemove)
        {
            if (playerItems.TryGetValue(
                player,
                out LobbyPlayerItem item))
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }


            playerItems.Remove(player);
        }
    }


    private Sprite GetCharacterAvatar(
        string characterId)
    {
        if (characterAvatars == null ||
            characterAvatars.Length == 0)
        {
            return null;
        }


        if (!characterId.StartsWith("Character"))
        {
            return null;
        }


        string numberPart =
            characterId.Replace(
                "Character",
                ""
            );


        if (!int.TryParse(
            numberPart,
            out int characterNumber))
        {
            return null;
        }


        int index =
            characterNumber - 1;


        if (index < 0 ||
            index >= characterAvatars.Length)
        {
            return null;
        }


        return characterAvatars[index];
    }
}