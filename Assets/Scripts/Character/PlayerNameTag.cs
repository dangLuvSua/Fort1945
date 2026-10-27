using TMPro;
using UnityEngine;
using Fusion;

public class PlayerNameTag : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text nameText;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Placement")]
    [SerializeField, Min(0f)] private float headClearance = 0.25f;

    private PlayerNetwork playerNetwork;
    private CharacterController characterController;
    private Transform playerRoot;
    private Animator characterAnimator;
    private Transform head;
    private SkinnedMeshRenderer[] bodyRenderers;
    private float nextHeadSearch;

    private void Awake()
    {
        // PlayerNameTag is on NameTag,
        // so search upward for PlayerNetwork.
        playerNetwork =
            GetComponentInParent<PlayerNetwork>();

        characterController =
            GetComponentInParent<CharacterController>();

        if (characterController != null)
        {
            playerRoot = characterController.transform;
        }
        else if (playerNetwork != null)
        {
            playerRoot = playerNetwork.transform;
        }

        if (playerNetwork == null)
        {
            Debug.LogError(
                "[NAME TAG] PlayerNetwork not found in parent!"
            );
        }

        if (nameText == null)
        {
            Debug.LogError(
                "[NAME TAG] Name Text is not assigned!"
            );
        }
    }

    private void Start()
    {
        // Find the local player's camera.
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        UpdateName();
        FaceCamera();
        PlaceAboveHead();
    }

    private void PlaceAboveHead()
    {
        if (playerRoot == null)
        {
            return;
        }

        if (head == null && Time.unscaledTime >= nextHeadSearch)
        {
            nextHeadSearch = Time.unscaledTime + 0.25f;
            characterAnimator = playerRoot.GetComponentInChildren<Animator>();
            if (characterAnimator != null && characterAnimator.isHuman)
            {
                head = characterAnimator.GetBoneTransform(HumanBodyBones.Head);
                bodyRenderers = characterAnimator.GetComponentsInChildren<SkinnedMeshRenderer>();
            }
        }

        float topY = characterController != null
            ? characterController.center.y + characterController.height * 0.5f
            : 1.8f;

        Vector3 anchor = playerRoot.TransformPoint(new Vector3(0f, topY, 0f));
        if (head != null)
        {
            anchor = head.position;
            // Include the crown/hair above the head bone, which is inside the skull.
            foreach (SkinnedMeshRenderer bodyRenderer in bodyRenderers)
            {
                if (bodyRenderer != null && bodyRenderer.gameObject.activeInHierarchy)
                    anchor.y = Mathf.Max(anchor.y, bodyRenderer.bounds.max.y);
            }
        }

        transform.position = anchor + Vector3.up * headClearance;
        if (nameText != null && !string.IsNullOrWhiteSpace(nameText.text))
        {
            // Place the visible glyphs, regardless of the text rectangle's pivot.
            nameText.ForceMeshUpdate();
            Bounds glyphs = nameText.textBounds;
            Vector3 bottomCenter = new Vector3(glyphs.center.x, glyphs.min.y, glyphs.center.z);
            transform.position += anchor + Vector3.up * headClearance
                - nameText.transform.TransformPoint(bottomCenter);
        }
    }

    private void UpdateName()
    {
        if (nameText == null)
        {
            return;
        }

        if (playerNetwork == null || playerNetwork.Object == null || !playerNetwork.Object.IsValid)
        {
            return;
        }

        string playerName =
            playerNetwork.PlayerName.ToString();
        // Debug.Log(
        //     $"[NAME TAG] Network Name: {playerNetwork.PlayerName}"
        // );
        if (string.IsNullOrWhiteSpace(playerName))
        {
            nameText.text = "Player";
        }
        else
        {
            nameText.text = playerName;
        }
    }

    private void FaceCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        Vector3 directionToCamera = Vector3.ProjectOnPlane(
            targetCamera.transform.position - transform.position,
            Vector3.up
        );

        if (directionToCamera.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(
                directionToCamera,
                Vector3.up
            ) * Quaternion.Euler(0f, 180f, 0f);
        }
    }
}
