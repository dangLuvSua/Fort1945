using UnityEngine;

public class DungeonConnector : MonoBehaviour
{
    public enum ConnectorType
    {
        Entrance,
        Exit
    }

    [Header("Connector")]
    public ConnectorType connectorType;

    [HideInInspector]
    public bool used = false;
}