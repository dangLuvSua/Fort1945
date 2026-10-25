# Evidence board photograph matching

## What was inspected

This project uses Unity 6000.5.4f1 and Fusion Shared mode (`NetworkRunnerHandler`). `PlayerInteractor` already raycasts at a 3 metre default range and reads `IInteractable`. It normally uses E; only Polaroids now use left mouse click. The existing Gallery selection, preview, refresh, storage and captured-photo handlers are retained.

`ItemData.itemId` is an integer. Existing item assets use their current IDs; no new item IDs are allocated. Captured photos instead receive temporary negative IDs and an encoded-JPEG content key. `CameraController` substitutes the `CameraPhotoPoint.Photo` texture for a successful framed photograph, JPEG-encodes at quality 80, and shares the bytes through the existing camera album. The board maps those content keys to character IDs. Arbitrary screenshots and unmapped photographs cannot submit.

There is no PuzzleManager in the project. `PuzzleNetworkState` belongs to the separate map-piece puzzle and is left intact. `MissionManager` displays mission UI; `PhaseManager.ReportExternalPhaseComplete()` is an existing objective hook.

The existing `Integ 2.prefab` has all four display components and child renderers, but its configured IDs are `soldiers`, `vicente`, `josefa`, `liling`. Update the last three as below. Some existing parent names are misspelled `polariod` and contain spaces; names do not drive matching. Existing prefab edits were preserved. Scene/prefab wiring below must be completed in Unity before play.

## Hierarchy and components

Use the existing dungeon scene instance, with this structure (renaming parents is optional):

```text
Integ 2
  Polaroids                       EvidenceBoardManager
    polaroid_of_soldiers           existing SpriteRenderer + PolaroidPhotoDisplay
                                  + PolaroidInteractable + BoxCollider
      SoldiersPhoto               existing SpriteRenderer
    polaroid_of_vicente            same parent components
      VicentePhoto                existing SpriteRenderer
    polaroid_of_Josefa             same parent components
      JosefaPhoto                 existing SpriteRenderer
    polaroid_of_Liling             same parent components
      PhotoLiling                 existing SpriteRenderer

Existing Inventory UI controller  existing PlayerInventoryUI (keep active)
  existing inventory panel
    existing Gallery panel
      existing slots and preview
      SelectPhotograph            Button + child TMP text "SELECT"
      EvidenceFeedback            TMP_Text
```

Keep PlayerInventoryUI on its existing active controller, outside the panel that it hides. The evidence UI code is a partial class extending that same component; do **not** attach a second PlayerInventoryUI. No additional canvas or inventory is needed.

The dungeon creates Integ 2 using ordinary Instantiate. EvidenceBoardManager is therefore a MonoBehaviour and uses the existing runner-spawned PuzzleNetworkState for replicated evidence state. The Dungeon scene already has PuzzleNetworkBootstrap with its state prefab assigned. Do not add a NetworkObject to the board or individual photographs. If a NetworkObject was added to Polaroids during the earlier setup, it is no longer needed and can be removed. Keep PuzzleNetworkBootstrap and its existing network prefab configured. Save the scene and allow Unity/Fusion to recompile and bake the updated PuzzleNetworkState prefab.

## Four slots and photograph catalog

Assign the manager's Slots array in this exact order. For every display assign Character Photo Renderer to the corresponding child, never to the frame renderer. On every PolaroidInteractable assign Board to the one manager (it also finds a parent manager automatically).

| Slot | Existing parent / child | Correct Character ID |
|---|---|---|
| 0 | soldiers / SoldiersPhoto | soldiers |
| 1 | vicente / VicentePhoto | vicente_lim |
| 2 | Josefa / JosefaPhoto | josefa_escoda |
| 3 | Liling / PhotoLiling | rafael_roces |

Manager Photographs: size 4, same order on every client/build. Each entry needs:

- Character ID: matching the table.
- Captured Texture: the **exact same texture asset** used by that character's CameraPhotoPoint.Photo. The matching key is computed with the existing camera readable-copy and JPEG routines. Do not use a separately compressed copy or a screenshot of it.
- Board Sprite: a Sprite imported from that photograph, representing the image area without an extra Polaroid frame. It is resolved locally on every peer, independent of whether that peer owns a camera. This canonical sprite keeps board rendering independent of runtime image deletion and local runtime IDs.
- Inventory Item: optional existing Gallery-category ItemData asset for collectible asset photographs. Leave empty when using only camera captures. Keep its current numeric itemId; the explicit asset reference maps it to the entry's character ID. Ensure existing item database IDs remain unique.

Exactly one catalog entry per character is supported. Assign all four board sprites and renderer references on every build. No Sprite or Texture object references travel through Fusion: the request sends slot/catalog indices and the durable state is a four-bit completion mask. Since each slot has only one correct image, its bit uniquely determines its local photograph.

The submitting player's existing inventory must contain the camera, matching current preview behaviour. Captured-photo selection checks membership in CapturedPhotos; asset selection checks inventory.Has. No item is consumed. Gallery delete, equipment and quickbar methods are unchanged.

## Collider and rendering setup

Fit a non-trigger **3D BoxCollider** to each Polaroid's clickable region. The current interaction raycast ignores triggers and does not use Collider2D. Put it on a layer included in the existing PlayerInteractor Mask. Avoid overlapping the four colliders. Interaction continues to use the existing player's camera and Range; no extra raycaster is added.

Keep each parent SpriteRenderer and its question-mark frame sprite intact. Set parent and child to the same sorting layer (Default is fine); give each child a higher Sorting Order than its parent, for example parent 0, child 1. Keep the child active; the display hides its renderer during Awake. Fit its local position/scale to the inner question-mark area so it covers the mark without covering the frame. If the material uses depth testing, offset the child very slightly toward the viewing side of the board to avoid depth conflict. Verify from both player camera angles. No automatic scale changes are made because the board is already artist-authored.

## Existing Gallery references and SELECT

On the existing PlayerInventoryUI assign the three new Inspector fields:

- Evidence Tabs: existing InventoryTabController associated with this inventory.
- Select Photograph Button: the new SELECT Button inside the existing Gallery.
- Evidence Feedback: the new TMP_Text inside the Gallery.

Leave the Button's persistent OnClick list empty. The component registers/removes its own listener. Make its GameObject initially inactive; runtime also hides it outside matching mode. Keep it outside Description Panel Root, which the Gallery normally hides. Keep the preview image, content transforms and other inventory references as they are.

The interactor finds the UI bound to the interacting local inventory, stores its target board and slot, calls OpenInventoryPanel(), and calls the existing OpenGalleryTab(). Existing slot handlers select and preview the photo; SELECT resolves that selected ItemData against collected photos. An incorrect mapped character leaves the Gallery open and displays the requested error. Unmapped photos leave SELECT disabled. A correct match sends an authority request; the Gallery closes only after replicated state confirms the slot is filled. A competing correct placement also closes the stale selection.

Close buttons should continue calling CloseInventoryPanel(). Escape also cancels matching. Switching away from Gallery, externally hiding the inventory panel, disabling the UI, or losing the board cancels its pending reference. Cursor state is saved/restored for matching and kept unlocked while choosing. Normal inventory cursor handling is unchanged. Cancellation clears the local selection operation; it cannot undo a correct placement already committed by authority.

## Completion hooks

Manager Placement Audio: optional AudioSource with a placement clip. Late join restoration is silent.

Manager Completion Mission: optional MissionData to show using the existing MissionManager on each client when completion is first observed (including a late join).

On Progress Changed: local UnityEvent for updating existing progress UI. Read CompletedMask or IsCompleted from the manager.

On Completed: local UnityEvent fired once per manager instance after observing all four bits; also useful for local objective presentation. Late joiners receive this once to restore their presentation.

On Authority Completed: authority-only UnityEvent fired only when the fourth slot is first committed, not during late join restoration. For an existing PhaseManager phase configured for ExternalSignal, connect this event to that scene PhaseManager's **ReportExternalPhaseComplete()**. Only do this for the intended phase; other phase rules should keep their existing handlers. Do not connect the same objective advancement to both completion events.

Shared mode trusts the submitting client's collection check, consistent with local captured-photo storage. Authority validates catalog/character IDs, active RPC sender, slot bounds and duplicate state. This is not a server-verified inventory proof: a modified client could forge a valid catalog request. Authoritative anti-cheat ownership would require moving the existing camera album/collection ledger to an authority-readable network model. This implementation does not change that collection architecture.

## Validation checklist

- Let Unity finish importing all new scripts; verify no Console compiler or Fusion weaving errors. Save the configured scene.
- Approach each empty Polaroid: prompt appears only inside the existing interaction range; left-click opens the existing Gallery with SELECT visible and initially disabled.
- Browse Gallery normally: SELECT stays hidden; preview, deletion, tabs and quickbar continue working.
- Select each wrong mapped character: SELECT enables; click shows "Incorrect photograph. Try another."; board and inventory remain unchanged.
- Select an uncollected/unmapped/black screenshot: it cannot submit. Delete a selected collected photo: selection becomes invalid.
- Match each correct character: Gallery closes after authority confirmation, only that child's renderer appears, original frame stays visible, inventory photo remains.
- Hover completed slots: "Photograph Already Placed"; clicking does not reopen selection.
- Cancel with Close, Escape, switching tabs and externally hiding the panel; reopen normally and confirm SELECT is hidden and no stale slot remains.
- Complete four slots: progress updates, local completion fires once, authority objective hook fires once, repeated clicks cannot advance it again.
- Two clients submit for one slot simultaneously: one persistent placement; neither can replace it. Have one client delete its inventory photo afterward: both board images remain.
- Join after one, then four placements: all existing images and completion presentation restore without requiring inventory photos on the joining client.
- Disconnect the shared master: confirm master authority transfer preserves the scene board state and the remaining client can complete it.
- Test the hash mapping in actual target builds, including non-readable source textures and shared-album restore; encoding/texture import settings must produce the same JPEG key as capture.

## Complete source

The complete updated source is saved directly in the project:

- Assets/Scripts/Interaction/EvidenceBoardManager.cs (new)
- Assets/Scripts/Interaction/PolaroidInteractable.cs (new)
- Assets/Scripts/Interaction/DungeonItems/Inventory/PlayerInventoryUI.Evidence.cs (new partial implementation)
- Assets/Scripts/Interaction/DungeonItems/Inventory/PlayerInventoryUI.cs
- Assets/Scripts/Interaction/PolaroidPhotoDisplay.cs
- Assets/Scripts/Interaction/DungeonItems/PlayerInteraction.cs (defines PlayerInteractor)
- Assets/Scripts/Interaction/DungeonItems/PlayerInventory.cs (existing hash utility exposed)
- Assets/Scripts/Interaction/DungeonItems/Camera/CameraController.cs (existing readable-copy utility exposed)

PuzzleNetworkState.cs also contains the evidence completion mask and placement RPC. Map-puzzle resets do not reset the evidence mask. The procedural room's board renders that shared state, including for late joiners.
