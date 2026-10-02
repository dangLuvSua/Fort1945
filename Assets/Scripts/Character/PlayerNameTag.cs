using TMPro;
using UnityEngine;
using Fusion;

public class PlayerNameTag : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text nameText;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    private PlayerNetwork playerNetwork;

    private void Awake()
    {
        // PlayerNameTag is on NameTag,
        // so search upward for PlayerNetwork.
        playerNetwork =
            GetComponentInParent<PlayerNetwork>();

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
    }

    private void UpdateName()
    {
        if (nameText == null)
        {
            return;
        }

        if (playerNetwork == null)
        {
            return;
        }

        string playerName =
            playerNetwork.PlayerName.ToString();
        Debug.Log(
            $"[NAME TAG] Network Name: {playerNetwork.PlayerName}"
        );
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

        // Keep the text facing the camera.
        transform.rotation =
            Quaternion.LookRotation(
                transform.position -
                targetCamera.transform.position
            );
    }
}