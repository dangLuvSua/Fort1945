using Fusion;
using UnityEngine;

public class ChestState : NetworkBehaviour
{
    public bool IsNetworkReady { get; private set; }

    [Networked]
    public NetworkBool IsOpen { get; private set; }

    public override void Spawned()
    {
        IsNetworkReady = true;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        IsNetworkReady = false;
    }

    public void ToggleOpen()
    {
        if (!IsNetworkReady || Object == null || !Object.IsValid)
            return;

        RPC_ToggleOpen();
    }

    public void SetOpen(bool isOpen)
    {
        if (!IsNetworkReady || Object == null || !Object.IsValid || IsOpen == isOpen)
            return;

        if (Object.HasStateAuthority)
            IsOpen = isOpen;
        else
            RPC_SetOpen(isOpen);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ToggleOpen(RpcInfo info = default)
    {
        IsOpen = !IsOpen;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetOpen(bool isOpen, RpcInfo info = default)
    {
        IsOpen = isOpen;
    }
}
