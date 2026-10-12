using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class EnemyNavAuthority : NetworkBehaviour
{
    public override void Spawned()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();

        if (agent == null)
            return;

        if (Object.HasStateAuthority)
        {
            agent.enabled = true;
            agent.Warp(transform.position);
        }
        else
        {
            agent.enabled = false;
        }
    }
}