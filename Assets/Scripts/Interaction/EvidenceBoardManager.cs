using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Local generated-board presentation backed by the shared PuzzleNetworkState.</summary>
public sealed class EvidenceBoardManager : MonoBehaviour
{
    [Serializable]
    public sealed class Photograph
    {
        public string characterID;
        [Tooltip("Optional existing Gallery ItemData. Its numeric ID is preserved.")]
        public ItemData inventoryItem;
        [Tooltip("Same texture assigned to the corresponding CameraPhotoPoint.")]
        public Texture2D capturedTexture;
        public Sprite boardSprite;
        [NonSerialized] public string contentKey;
    }

    [SerializeField] private PolaroidPhotoDisplay[] slots = new PolaroidPhotoDisplay[4];
    [SerializeField] private Photograph[] photographs = new Photograph[4];
    [SerializeField] private AudioSource placementAudio;
    [SerializeField] private MissionData completionMission;
    [SerializeField] private UnityEvent onProgressChanged = new UnityEvent();
    [SerializeField] private UnityEvent onCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onAuthorityCompleted = new UnityEvent();

    public int CompletedMask => Ready ? PuzzleNetworkState.Instance.EvidenceCompletedMask : 0;
    public bool IsCompleted => Ready && CompletedMask == 15;
    public bool Ready => PuzzleNetworkState.Instance != null &&
        PuzzleNetworkState.Instance.Object != null &&
        PuzzleNetworkState.Instance.Object.IsValid &&
        PuzzleNetworkState.Instance.Object.IsInSimulation;
    private int renderedMask;
    private bool completionRaised;

    private void Awake()
    {
        foreach (Photograph photo in photographs)
        {
            if (photo == null || photo.capturedTexture == null) continue;
            Texture2D copy = CameraController.CreateReadableCopy(photo.capturedTexture);
            photo.contentKey = PlayerInventory.GetPhotoContentKey(copy.EncodeToJPG(80));
            Destroy(copy);
        }
    }

    public int IndexOf(PolaroidPhotoDisplay slot) => Array.IndexOf(slots, slot);
    public bool IsPlaced(int index) => Ready && index >= 0 && index < 4 &&
        (CompletedMask & (1 << index)) != 0;

    // Captured IDs are runtime-local; match the existing encoded content key instead.
    public int ResolveCollectedPhoto(PlayerInventory inventory, ItemData item)
    {
        if (inventory == null || item == null || item.category != ItemCategory.Gallery ||
            item.icon == null || !inventory.HasCameraInInventory) return -1;
        for (int i = 0; i < photographs.Length; i++)
        {
            Photograph photo = photographs[i];
            if (photo == null || photo.boardSprite == null) continue;
            if (photo.inventoryItem == item && inventory.Has(item)) return i;
            foreach (PlayerInventory.CapturedPhoto captured in inventory.CapturedPhotos)
                if (captured.Item == item && !string.IsNullOrEmpty(photo.contentKey) &&
                    captured.ContentKey == photo.contentKey) return i;
        }
        return -1;
    }

    public bool Matches(int slot, int photo) => slot >= 0 && slot < 4 &&
        slots.Length == 4 && slots[slot] != null && slots[slot].CanDisplayPhoto && photo >= 0 &&
        photo < photographs.Length && photographs[photo] != null &&
        photographs[photo].boardSprite != null &&
        slots[slot].CorrectCharacterID == photographs[photo].characterID;

    public void Submit(int slot, int photo)
    {
        if (Ready && Matches(slot, photo) && !IsPlaced(slot))
            PuzzleNetworkState.Instance.RPC_PlaceEvidencePhoto(slot, photo);
    }

    public void NotifyAuthorityCompleted()
    {
        onAuthorityCompleted.Invoke();
    }

    private bool receivedInitialState;
    private void Update()
    {
        if (!Ready) return;
        ApplyState(receivedInitialState);
        receivedInitialState = true;
    }

    private void ApplyState(bool playAudio)
    {
        if (!Ready) return;
        bool changed = renderedMask != CompletedMask;
        for (int i = 0; i < slots.Length && i < 4; i++)
        {
            if (!IsPlaced(i) || slots[i] == null || slots[i].IsCompleted) continue;
            foreach (Photograph photo in photographs)
                if (photo != null && photo.characterID == slots[i].CorrectCharacterID &&
                    photo.boardSprite != null)
                { slots[i].RestorePlacement(photo.boardSprite); break; }
        }
        renderedMask = CompletedMask;
        if (changed)
        {
            onProgressChanged.Invoke();
            if (playAudio && placementAudio != null) placementAudio.Play();
        }
        if (IsCompleted && !completionRaised)
        {
            completionRaised = true;
            onCompleted.Invoke();
            if (completionMission != null) MissionManager.Show(completionMission);
        }
    }
}
