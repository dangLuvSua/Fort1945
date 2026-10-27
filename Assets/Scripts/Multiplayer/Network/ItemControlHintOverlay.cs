using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows local control hints for the item selected in the player's quick bar.
/// </summary>
public sealed class ItemControlHintOverlay : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    private PlayerInventory localInventory;
    private PlayerInventoryUI localInventoryUI;
    private CameraController localCameraController;
    private CandleController localCandleController;
    private SaltController localSaltController;
    private float nextRefreshTime;
    private bool visible;
    private string primaryHint;
    private string secondaryHint;
    private GUIStyle panelStyle;
    private GUIStyle keyStyle;
    private GUIStyle actionStyle;

    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            RefreshLocalPlayer();
        }
        RefreshHint();
    }

    private void RefreshLocalPlayer()
    {
        NetworkRunner runner = NetworkRunnerHandler.Instance != null
            ? NetworkRunnerHandler.Instance.GameRunner
            : null;

        if (runner == null || !runner.IsRunning || !runner.LocalPlayer.IsValid ||
            !runner.TryGetPlayerObject(runner.LocalPlayer, out NetworkObject playerObject) ||
            playerObject == null)
        {
            localInventory = null;
            localInventoryUI = null;
            localCameraController = null;
            localCandleController = null;
            localSaltController = null;
            return;
        }

        if (localInventory == null || localInventory.gameObject != playerObject.gameObject)
            localInventory = playerObject.GetComponent<PlayerInventory>();

        if (localCameraController == null || localCameraController.gameObject != playerObject.gameObject)
            localCameraController = playerObject.GetComponent<CameraController>();

        if (localCandleController == null || localCandleController.gameObject != playerObject.gameObject)
            localCandleController = playerObject.GetComponent<CandleController>();

        if (localSaltController == null || localSaltController.gameObject != playerObject.gameObject)
            localSaltController = playerObject.GetComponent<SaltController>();

        if (localInventoryUI == null || !localInventoryUI.IsForInventory(localInventory))
        {
            localInventoryUI = null;
            PlayerInventoryUI[] inventoryUIs = FindObjectsByType<PlayerInventoryUI>(FindObjectsSortMode.None);
            foreach (PlayerInventoryUI inventoryUI in inventoryUIs)
            {
                if (inventoryUI != null && inventoryUI.IsForInventory(localInventory))
                {
                    localInventoryUI = inventoryUI;
                    break;
                }
            }
        }
    }

    private void RefreshHint()
    {
        visible = false;
        primaryHint = null;
        secondaryHint = null;

        if (localInventory == null || localInventory.SelectedSlot < 0 ||
            (localInventoryUI != null && localInventoryUI.IsInventoryOpen()) ||
            (InGameOptionsController.Instance != null &&
             InGameOptionsController.Instance.IsAnyPanelOpen))
        {
            return;
        }

        ItemData item = localInventory.GetQuickBarItemAt(localInventory.SelectedSlot);
        if (item == null)
            return;

        switch (item.itemId)
        {
            case 201: // Cross
                primaryHint = "R";
                secondaryHint = "Hold to heal or revive teammate";
                break;

            case 102: // Prison Key 1
            case 103: // Dungeon Key
            case 104: // Prison Key 2
            case 105: // Interrogation Room Key
                primaryHint = "E";
                secondaryHint = "Hold to unlock the matching gate";
                break;

            case 202: // Salt
                if (localSaltController != null && localSaltController.IsSpraying)
                    return;
                primaryHint = "F";
                secondaryHint = "Spray Salt";
                break;

            case 203: // Candle
                if (localCandleController != null && localCandleController.Object != null &&
                    localCandleController.Object.IsValid && localCandleController.Object.IsInSimulation &&
                    localCandleController.IsCandleLit)
                    return;
                primaryHint = "F";
                secondaryHint = "Light or extinguish Candle";
                break;

            case 204: // Camera
                if (localCameraController != null && localCameraController.IsCameraOpen)
                    return;
                primaryHint = "X";
                secondaryHint = "Open Camera";
                break;

            default:
                if (item.category == ItemCategory.PuzzlePiece)
                {
                    primaryHint = "E";
                    secondaryHint = "Assemble map at the puzzle table";
                }
                break;
        }

        visible = !string.IsNullOrEmpty(primaryHint) &&
            !IsActionHeld(item);
    }

    private bool IsActionHeld(ItemData item)
    {
        Keyboard keyboard = Keyboard.current;
        // Hide immediately on input, before the item controller updates its state.
        if (item.itemId == 204 && keyboard != null && keyboard.xKey.isPressed)
            return true;

        switch (primaryHint)
        {
            case "R": return keyboard != null && keyboard.rKey.isPressed;
            case "E": return keyboard != null && keyboard.eKey.isPressed;
            case "F": return keyboard != null && keyboard.fKey.isPressed;
            case "X": return keyboard != null && keyboard.xKey.isPressed;
            case "Right-click":
                return Mouse.current != null && Mouse.current.rightButton.isPressed;
            default: return false;
        }
    }

    private void OnGUI()
    {
        if (!visible)
            return;

        EnsureStyles();

        const float width = 340f;
        const float height = 52f;
        float inventoryTop = Screen.height * 0.16f;
        if (localInventoryUI != null && localInventoryUI.TryGetQuickBarScreenTop(out float screenTop))
            inventoryTop = screenTop;
        Rect panelRect = new Rect(
            (Screen.width - width) * 0.5f,
            Mathf.Max(0f, Screen.height - inventoryTop - height - 12f),
            width,
            height);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.035f, 0.045f, 0.06f, 0.88f);
        GUI.Box(panelRect, GUIContent.none, panelStyle);
        GUI.color = previousColor;

        float keyWidth = primaryHint.Length > 5 ? 110f : 52f;
        GUI.Box(
            new Rect(panelRect.x + 8f, panelRect.y + 8f, keyWidth, 36f),
            primaryHint,
            keyStyle);
        GUI.Label(
            new Rect(panelRect.x + keyWidth + 18f, panelRect.y + 4f,
                width - keyWidth - 26f, height - 8f),
            secondaryHint,
            actionStyle);
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
            return;

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter
        };
        keyStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        actionStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
    }
}
