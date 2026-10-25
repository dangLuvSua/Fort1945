using UnityEngine;

/// <summary>
/// Handles the visual state of a single Polaroid slot on the evidence board.
/// Attach to each polaroid parent GameObject alongside PolaroidInteractable.
///
/// The child SpriteRenderer (characterPhotoRenderer) sits in front of the
/// parent question-mark sprite and is hidden until a correct photograph is placed.
/// </summary>
public class PolaroidPhotoDisplay : MonoBehaviour
{
    // =========================================================
    // INSPECTOR
    // =========================================================

    [Header("Photograph")]
    [Tooltip("SpriteRenderer on the child GameObject that displays the placed photo " +
             "on top of the question-mark polaroid frame.")]
    [SerializeField] private SpriteRenderer characterPhotoRenderer;

    [Header("Matching")]
    [Tooltip(
        "String that must match the characterID on the photograph the player selects. " +
        "Use soldiers, vicente_lim, josefa_escoda, or rafael_roces."
    )]
    [SerializeField] private string correctCharacterID;

    // =========================================================
    // STATE
    // =========================================================

    private bool isCompleted;

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (characterPhotoRenderer != null)
            characterPhotoRenderer.enabled = false;
    }

    // =========================================================
    // PHOTO PLACEMENT
    // =========================================================

    /// <summary>
    /// Validates the given characterID against this slot's expected value and,
    /// if correct, displays the photograph and marks the slot as completed.
    /// </summary>
    /// <returns>True if the photo was correct and the slot is now completed.</returns>
    public bool TryPlacePhoto(string characterID, Sprite photo)
    {
        if (isCompleted || photo == null || characterPhotoRenderer == null)
            return false;

        if (characterID != correctCharacterID)
        {
            Debug.Log(
                $"[POLAROID] Wrong photograph for '{gameObject.name}'. " +
                $"Expected '{correctCharacterID}', got '{characterID}'."
            );
            return false;
        }

        ApplyPhoto(photo);
        return true;
    }

    /// <summary>
    /// Applies a confirmed photo placement received from the network.
    /// Bypasses character-ID validation. Safe to call multiple times (idempotent:
    /// does nothing if the slot is already completed).
    /// </summary>
    public void RestorePlacement(Sprite photo)
    {
        if (isCompleted || photo == null || characterPhotoRenderer == null)
            return;

        ApplyPhoto(photo);
    }

    // =========================================================
    // INTERNAL
    // =========================================================

    private void ApplyPhoto(Sprite photo)
    {
        if (characterPhotoRenderer != null)
        {
            characterPhotoRenderer.sprite  = photo;
            characterPhotoRenderer.enabled = true;
        }

        isCompleted = true;

        Debug.Log($"[POLAROID] Photograph placed on '{gameObject.name}'.");
    }

    // =========================================================
    // PROPERTIES
    // =========================================================

    /// <summary>True once a correct photograph has been placed on this slot.</summary>
    public bool IsCompleted => isCompleted;
    public bool CanDisplayPhoto => characterPhotoRenderer != null;

    /// <summary>
    /// The character ID this slot expects. Read by EvidenceBoardManager for
    /// authority-side validation in RPC_RequestPlacement.
    /// </summary>
    public string CorrectCharacterID => correctCharacterID;
}
