using Fusion;
using UnityEngine;

public class LobbyPlayerState : NetworkBehaviour
{
    [Header("Networked Player Data")]

    [Networked]
    public NetworkBool IsReady { get; private set; }

    [Networked]
    public NetworkBool IsHost { get; private set; }

    // Unlike IsHost, this remains true only for the player who created
    // the session, even if Shared Mode elects a different master later.
    [Networked]
    public NetworkBool IsLobbyCreator { get; private set; }

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

    public override void FixedUpdateNetwork()
    {
        // Shared Mode automatically elects a new Master Client on disconnect.
        // Replicate the new role into this player's host marker for the lobby UI.
        if (Object == null || !Object.HasStateAuthority || Runner == null)
            return;

        bool isCurrentMaster = Runner.IsSharedModeMasterClient;
        if ((bool)IsHost != isCurrentMaster)
        {
            IsHost = isCurrentMaster;
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

        IsLobbyCreator = IsHost;


        Debug.Log(
            $"[LOBBY PLAYER] Initialized:\n" +
            $"Player: {PlayerName}\n" +
            $"Character: {SelectedCharacter}\n" +
            $"Host: {IsHost}"
        );
    }

    public override void Render()
    {
        if (Object == null || !(bool)IsLobbyCreator || NetworkRunnerHandler.Instance == null)
            return;

        NetworkRunnerHandler.Instance.RegisterLobbyCreator(Object.InputAuthority);
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

    public void RequestResetReady()
    {
        if (Object == null || !Object.IsValid || Runner == null || !Runner.IsRunning)
            return;

        RPC_ResetReady();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ResetReady(RpcInfo info = default)
    {
        if (Object != null && Object.HasStateAuthority)
            IsReady = false;
    }
}
