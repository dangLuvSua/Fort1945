using UnityEngine;

public class CameraViewfinderUI : MonoBehaviour
{
    [SerializeField] private GameObject viewfinderPanel;

    private PlayerInventory localInventory;

    private void Awake()
    {
        if (viewfinderPanel != null)
            viewfinderPanel.SetActive(false);
    }

    private void Update()
    {
        if (viewfinderPanel == null)
            return;

        // Find the locally controlled player's inventory after Fusion spawns it.
        if (localInventory == null)
        {
            PlayerInventory[] inventories =
                FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);

            foreach (PlayerInventory inventory in inventories)
            {
                if (inventory != null &&
                    inventory.Object != null &&
                    inventory.Object.HasInputAuthority)
                {
                    localInventory = inventory;
                    break;
                }
            }
        }

        if (localInventory != null)
            viewfinderPanel.SetActive(localInventory.IsViewfinderOpen);
    }
}