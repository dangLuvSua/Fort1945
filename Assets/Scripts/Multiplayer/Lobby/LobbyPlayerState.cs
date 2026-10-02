using Fusion;
using UnityEngine;

public class LobbyPlayerState : NetworkBehaviour
{
    [Header("Networked Player Data")]

    [Networked]
    public NetworkBool IsReady { get; private set; }

    [Networked]
    public NetworkBool IsHost { get; private set; }

    [Networked]
    public NetworkString<_32> PlayerName { get; private set; }

    [Networked]
    public NetworkString<_32> SelectedCharacter { get; private set; }


    public override void Spawned()
    {
        Debug.Log(
            $"[LOBBY PLAYER] Spawned: " +
            $"{Object.InputAuthority}"
        );

        Debug.Log(
            $"[LOBBY PLAYER] " +
            $"StateAuthority: {Object.HasStateAuthority}"
        );


        // In Shared Mode, this player's own object
        // normally has State Authority on this client.
        if (Object.HasStateAuthority)
        {
            InitializePlayerData();
        }
    }


    private void InitializePlayerData()
    {
        string playerName =
            PlayerProfile.PlayerName;

        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = "Player";
        }


        string selectedCharacter =
            PlayerProfile.SelectedCharacter;

        if (string.IsNullOrWhiteSpace(selectedCharacter))
        {
            selectedCharacter = "Character1";
        }


        PlayerName = playerName;

        SelectedCharacter =
            selectedCharacter;

        IsReady = false;

        IsHost =
            Runner != null &&
            Runner.IsSharedModeMasterClient;


        Debug.Log(
            $"[LOBBY PLAYER] Initialized:\n" +
            $"Player: {PlayerName}\n" +
            $"Character: {SelectedCharacter}\n" +
            $"Host: {IsHost}"
        );
    }


    // =====================================================
    // READY
    // =====================================================

    public void SetReady(bool ready)
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning(
                "[LOBBY PLAYER] " +
                "Cannot change Ready state because " +
                "this client does not have State Authority."
            );

            return;
        }


        IsReady = ready;


        Debug.Log(
            $"[LOBBY] {PlayerName} " +
            $"Ready = {IsReady}"
        );
    }
}