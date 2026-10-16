using UnityEngine;
using TMPro;

public class LockedAreaTrigger : MonoBehaviour
{
    [Header("Shared Panel Controller")]
    [SerializeField] private LockedAreaPanelController panelController;

    [Header("This Area's Message")]
    [SerializeField] private string titleText = "AREA LOCKED";
    [TextArea(2, 5)]
    [SerializeField]
    private string descriptionText =
        "This area is currently unavailable.";

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = GetLocalPlayer(other);
        if (player == null)
            return;

        Debug.Log($"[Locked Area] Player entered: {gameObject.name}");

        if (panelController != null)
        {
            panelController.ShowPanel(titleText, descriptionText);
        }
        else
        {
            Debug.LogError(
                $"[Locked Area] Panel Controller is not assigned on {gameObject.name}!",
                this
            );
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = GetLocalPlayer(other);
        if (player == null)
            return;

        Debug.Log($"[Locked Area] Player left: {gameObject.name}");

        if (panelController != null)
        {
            panelController.HidePanel();
        }
    }

    private static PlayerController GetLocalPlayer(Collider other)
    {
        PlayerController player =
            other.GetComponentInParent<PlayerController>();

        if (player == null || player.Object == null ||
            !player.Object.IsValid || !player.Object.HasInputAuthority)
        {
            return null;
        }

        return player;
    }
}
