using Fusion;
using UnityEngine;

public class NetworkPlayerData : NetworkBehaviour
{
    public enum CharacterID
    {
        Character1 = 0,
        Character2 = 1,
        Character3 = 2,
        Character4 = 3,
    }

    [Networked]
    public CharacterID SelectedCharacter { get; private set; } = CharacterID.Character1;

    [Networked]
    public NetworkBool HasSelectedCharacter { get; private set; }

    public bool IsCharacterSelected()
    {
        return HasSelectedCharacter;
    }

    public void SelectCharacter(CharacterID characterID)
    {
        if (!Object.HasInputAuthority)
            return;

        RPC_SelectCharacter(characterID);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SelectCharacter(CharacterID characterID)
    {
        SelectedCharacter = characterID;
        HasSelectedCharacter = true;
    }

    public override void Spawned()
    {
        Debug.Log(
            $"[CHARACTER] Player {Object.InputAuthority} spawned. " +
            $"Selected Character: {SelectedCharacter}"
        );
    }
}