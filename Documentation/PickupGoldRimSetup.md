# Pulsing golden pickup rim

## What is already applied

Every one of the 59 item prefabs in `Assets/Prefabs/Items`, including all 48 world puzzle pieces, now has a **Pickup Highlight** component. `ViewFinderImage.prefab` is excluded.

The effect draws a transparent, additive gold Fresnel rim over the existing mesh. Original textures and materials stay assigned. The shader calculates the pulse on the GPU; no extra network properties, lights, or colliders are added. Opaque walls and other opaque geometry block the effect.

The assets are:

- Graph: `Assets/Shaders/Items/PickupGoldRim.shadergraph`
- Shared material: `Assets/Resources/PickupHighlight/PickupGoldRim.mat`
- Component: `Assets/Scripts/Interaction/DungeonItems/PickupHighlight.cs`

The existing `PlayerInventory.CleanHeldVisual` now removes highlighting immediately from local and remote held visuals. Items that use the same prefab for holding and dropping are supported.

## 1. Import the changes

1. Return to Unity and leave Play mode if it is running.
2. Wait for asset importing and script compilation to finish. If Unity has not noticed the new files, use **Assets > Refresh**.
3. Open **Window > General > Console** and check for red errors. Address compilation errors before testing components.
4. In the Project window, select `Assets/Resources/PickupHighlight/PickupGoldRim.mat`. Its shader should be **Fort1945/Items/PickupGoldRim**.

Do not replace the item's normal material with this material. The component adds the rim as a separate runtime overlay. Replacing the normal material would remove the normal surface appearance.

## 2. Test an existing pickup in the game

1. Open your usual gameplay flow and start a game so Fusion spawns the world items normally.
2. Find a spawned salt, cross, candle, camera, key, or world puzzle piece. The rim should pulse without aiming at the item or approaching within a special highlight distance.
3. Test it in a dark part of the scene. The unlit rim stays visible without scene lights; normal distance culling, fog, and occlusion can still affect visibility.
4. Move behind an opaque wall. The rim should be hidden.
5. Collect the item. Its world rim disappears with the pickup. The held model should have no gold rim.
6. Drop it. The new collectible world object should show the rim again.
7. With a full inventory, attempt collection: the item should remain in the world with its rim.
8. For chest loot, check that the rim appears when the item becomes collectible after the chest opens.
9. Repeat with a second multiplayer client: both clients should see world rims and neither should see rims on held visuals.

The 56 prefabs with `ItemPickup` use **Require Collectible Pickup = enabled**. A prefab simply dragged into an empty scene is not necessarily a spawned Fusion pickup, so this setting can correctly keep it dark until network initialization and a valid item ID are available.

## 3. Preview the appearance without multiplayer

1. Create a temporary test scene with a camera looking at the item.
2. Drag `Assets/Prefabs/Items/Flashlight.prefab` into the scene and frame it in front of the camera. This is a model-only prefab, so it can preview the rim without Fusion.
3. Enter Play mode. The effect is created at runtime; it does not draw in ordinary Edit mode or the prefab thumbnail.
4. To preview a network pickup instead, select that **scene instance**, locate **Pickup Highlight**, and temporarily uncheck **Require Collectible Pickup**. Do not apply that test override to the network pickup's prefab.
5. After testing, remove the instance or revert that checkbox before using the scene for gameplay.

`Flashlight`, `LargeCandle`, and `k01_lo2` currently have no `ItemPickup` or `KeyPickup`. Their highlight components have **Require Collectible Pickup = disabled**, so their world models can display the requested rim. This adds the visual effect only. If you later make them network pickups, enable that checkbox after adding the normal pickup setup.

## 4. Tune the shared material

Select `Assets/Resources/PickupHighlight/PickupGoldRim.mat` and adjust its exposed properties:

| Setting | Starting value | Effect |
| --- | --- | --- |
| Gold Color | Warm HDR gold | Changes the rim's color; the HDR picker can also raise its intensity. |
| Rim Power | 3 | Higher makes the rim tighter; lower spreads it farther over the surface. |
| Pulse Period (Seconds) | 1.5 | Duration of one pulse. Lower is faster; higher is slower. |
| Minimum Brightness | 0.6 | Brightness at the dimmest part of the pulse. Keep above zero to avoid fully fading away. |
| Maximum Brightness | 2.5 | Brightness at the strongest part of the pulse. Keep at least as high as the minimum. |

Start with **Rim Power = 2**, **Minimum = 1**, and **Maximum = 4** if small items are hard to notice. Try **Rim Power = 5** for a narrower rim.

Changes to this shared material affect all prefabs using it. Unity material-asset changes made during Play mode can persist after you stop, so record the original values before experimenting.

For one item to look different, duplicate the material into your own materials folder, edit the duplicate, and assign it to that prefab's **Pickup Highlight > Rim Material** field. A blank Rim Material field loads the default material from Resources.

## 5. Add optional Bloom

Bloom adds a halo around bright pixels. The unlit rim works without it.

1. Select the camera actually used by the local gameplay player and enable **Post Processing** in its URP camera settings. Enable HDR where the camera exposes that setting.
2. Use the scene's existing global Volume, or create one through **GameObject > Volume > Global Volume**. Assign a Volume Profile if needed. When modifying an existing profile shared by several scenes, duplicate it first if you want scene-specific changes.
3. In the Volume Profile, choose **Add Override > Post-processing > Bloom**.
4. Enable the override checkboxes for **Intensity** and **Threshold**. Start with **Intensity = 0.3** and **Threshold = 1**.
5. Ensure the camera's **Volume Mask** includes the Volume object's layer.
6. Test in Play mode. Raise Maximum Brightness or the HDR color intensity if the rim is visible but the halo is too weak.

The project's PC render pipeline already has HDR support enabled. Other quality levels can use a different pipeline asset: confirm HDR and post processing there too if the result changes between quality settings. Bloom also affects other bright scene objects.

## 6. Apply the effect to future items

1. Open the item's **world prefab** in Prefab Mode.
2. Add **Pickup Highlight** to the same GameObject as its `ItemPickup` or `KeyPickup` component.
3. Assign `PickupGoldRim.mat` to **Rim Material**, or leave it blank to use the default.
4. Keep **Require Collectible Pickup** and **Highlight Enabled** checked.
5. Leave **Target Renderers** empty to discover child mesh renderers automatically. For precise control, populate the array with only the item's MeshRenderers or SkinnedMeshRenderers. Particle systems and UI are ignored automatically.
6. Save the prefab and keep using it as the `ItemData.worldPrefab` in your existing spawners. The highlight component does not register prefabs with Fusion or assign item IDs.

For a purely visual object with no pickup logic, turn off **Require Collectible Pickup**. If another system creates held visuals outside `PlayerInventory`, call `SetHighlighted(false)` and disable its highlight component when attaching that model to a player.

## 7. Inspect the graph

Double-click `PickupGoldRim.shadergraph`. Its node flow is:

```text
Time / max(Pulse Period, 0.05)
    -> Multiply by 6.283185
    -> Sine
    -> Multiply by 0.5
    -> Add 0.5
    -> Lerp(Minimum Brightness, Maximum Brightness)

Fresnel(Rim Power) * pulse brightness * Gold Color
    -> Fragment Base Color

Fragment Alpha = 1
```

The graph uses **Universal / Unlit**, **Transparent**, **Additive**, **Front Faces**, **Depth Write Off**, and **Depth Test Less Equal**. Shadow casting is disabled. Preserve those settings for the intended overlay behavior.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| No rim in Edit mode | Expected: enter Play mode. |
| No rim on a network pickup | Confirm the component is enabled, Highlight Enabled is checked, the pickup was spawned by Fusion, its item ID is valid, and its chest is open if applicable. |
| Rim is too dim | Increase the minimum/maximum brightness or lower Rim Power. Check fog, exposure, and camera culling. |
| Rim is visible but there is no halo | Enable the camera's post processing and configure Bloom. |
| Only part of the model glows | Check Target Renderers and the source renderer's enabled state. Empty Target Renderers automatically includes all child meshes. |
| Flat puzzle piece barely glows head-on | Fresnel depends on viewing angle and surface normals. Lower Rim Power and inspect at an angle. A uniform border on a perfectly flat face needs a separate outline technique. |
| Item is pink | Check Console for shader import errors and verify that the active quality level uses URP. |
| Rim appears on a custom held model | Ensure the model is created through `PlayerInventory.CleanHeldVisual`, or explicitly suppress and disable its PickupHighlight. |
