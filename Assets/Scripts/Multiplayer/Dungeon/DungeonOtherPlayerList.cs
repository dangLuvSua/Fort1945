using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class DungeonOtherPlayerList : MonoBehaviour
{
    [Header("Player List")]
    [SerializeField] private Transform content;

    [SerializeField] private DungeonOtherPlayerItem playerItemPrefab;


    [Header("Character Avatars")]
    [SerializeField] private Sprite[] characterAvatars;


    // =====================================================
    // REFERENCES
    // =====================================================

    private NetworkRunner runner;


    private readonly Dictionary<
        PlayerRef,
        DungeonOtherPlayerItem
    > playerItems =
        new Dictionary<
            PlayerRef,
            DungeonOtherPlayerItem
        >();


    // =====================================================
    // UPDATE
    // =====================================================

    private void Update()
    {
        FindRunner();

        if (runner == null)
            return;


        UpdatePlayerList();
    }


    // =====================================================
    // FIND RUNNER
    // =====================================================

    private void FindRunner()
    {
        if (runner != null)
            return;


        runner =
            FindAnyObjectByType<NetworkRunner>();
    }


    // =====================================================
    // UPDATE PLAYER LIST
    // =====================================================

    private void UpdatePlayerList()
    {
        if (content == null)
            return;


        if (playerItemPrefab == null)
        {
            Debug.LogError(
                "[DUNGEON OTHER PLAYER LIST] " +
                "Player Item Prefab is not assigned!"
            );

            return;
        }


        // =================================================
        // ACTIVE PLAYERS
        // =================================================

        HashSet<PlayerRef> activePlayers =
            new HashSet<PlayerRef>(
                runner.ActivePlayers
            );


        // =================================================
        // LOCAL PLAYER
        // =================================================

        PlayerRef localPlayer =
            runner.LocalPlayer;


        // =================================================
        // CREATE ITEMS
        // =================================================

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            // Do NOT display yourself.
            if (player == localPlayer)
                continue;


            // Already has UI.
            if (playerItems.ContainsKey(player))
                continue;


            // ---------------------------------------------
            // Find network player object
            // ---------------------------------------------

            if (!runner.TryGetPlayerObject(
                player,
                out NetworkObject playerObject))
            {
                continue;
            }


            if (playerObject == null)
                continue;


            // ---------------------------------------------
            // Find LobbyPlayerState
            // ---------------------------------------------

            LobbyPlayerState playerState =
                playerObject.GetComponent<LobbyPlayerState>();


            if (playerState == null)
            {
                Debug.LogWarning(
                    "[DUNGEON OTHER PLAYER LIST] " +
                    $"LobbyPlayerState missing on {player}."
                );

                continue;
            }


            // ---------------------------------------------
            // Create UI item
            // ---------------------------------------------

            DungeonOtherPlayerItem item =
                Instantiate(
                    playerItemPrefab,
                    content
                );


            // ---------------------------------------------
            // Character avatar
            // ---------------------------------------------

            Sprite avatar =
                GetCharacterAvatar(
                    playerState.SelectedCharacter.ToString()
                );


            // ---------------------------------------------
            // Setup
            // ---------------------------------------------

            item.Setup(
                playerState,
                avatar
            );


            playerItems.Add(
                player,
                item
            );


            Debug.Log(
                "[DUNGEON OTHER PLAYER LIST] " +
                $"Added: {playerState.PlayerName}"
            );
        }


        // =================================================
        // UPDATE EXISTING ITEMS
        // =================================================

        foreach (
            KeyValuePair<
                PlayerRef,
                DungeonOtherPlayerItem
            > entry
            in playerItems
        )
        {
            PlayerRef player =
                entry.Key;

            DungeonOtherPlayerItem item =
                entry.Value;


            if (item == null)
                continue;


            // The player may temporarily not have
            // a PlayerObject during scene transition.
            if (!runner.TryGetPlayerObject(
                player,
                out NetworkObject playerObject))
            {
                continue;
            }


            if (playerObject == null)
                continue;


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


        // =================================================
        // REMOVE PLAYERS WHO LEFT
        // =================================================

        List<PlayerRef> playersToRemove =
            new List<PlayerRef>();


        foreach (
            KeyValuePair<
                PlayerRef,
                DungeonOtherPlayerItem
            > entry
            in playerItems
        )
        {
            PlayerRef player =
                entry.Key;


            if (!activePlayers.Contains(player))
            {
                playersToRemove.Add(player);
            }
        }


        foreach (PlayerRef player in playersToRemove)
        {
            if (playerItems.TryGetValue(
                player,
                out DungeonOtherPlayerItem item))
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }


            playerItems.Remove(player);
        }
    }


    // =====================================================
    // GET CHARACTER AVATAR
    // =====================================================

    private Sprite GetCharacterAvatar(
        string characterId)
    {
        if (characterAvatars == null ||
            characterAvatars.Length == 0)
        {
            return null;
        }


        if (string.IsNullOrWhiteSpace(characterId))
            return null;


        if (!characterId.StartsWith("Character"))
            return null;


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