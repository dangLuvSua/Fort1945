using TMPro;
using UnityEngine;

/// <summary>
/// World-space text tag (billboarded toward the local camera).
///
/// Used by the intro flow for:
/// - "FOLLOW THE SOLDIER" objective above the soldier guide
/// - "ENTER THE GATE (2/4)" above the gate
/// - the red -> green "DUNGEON ENTRY" zone label
/// - the pop-up ping text ("ALL PLAYERS ENTERED!")
///
/// Place the WorldTag.prefab in the world (or Instantiate it from
/// NetworkRunnerHandler.worldTagPrefab) then call Configure().
/// </summary>
public class WorldTag : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text text;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Life")]
    [Tooltip("Seconds before the tag fades and destroys itself. Zero = permanent.")]
    [SerializeField] private float lifetime = 0f;

    [Tooltip("Seconds spent fading out before destruction.")]
    [SerializeField] private float fadeOutSeconds = 0.5f;

    [Tooltip("How fast the tag eases toward the camera-facing orientation.")]
    [SerializeField] private float faceSpeed = 12f;

    // =========================================================
    // INTERNAL
    // =========================================================

    private Color baseColor =
        new Color(1f, 1f, 1f, 1f);

    private float timeAlive;

    private bool initialized;

    private bool fadingOut;


    // =========================================================
    // CONFIGURE
    // =========================================================

    public void Configure(
        string message,
        Color color,
        float fontSize,
        float lifeSeconds)
    {
        baseColor = color;

        if (text != null)
        {
            text.text = message;
            text.color = color;
            text.fontSize = fontSize;
        }

        lifetime = lifeSeconds;
        timeAlive = 0f;
        fadingOut = false;

        initialized = true;
    }


    /// <summary>
    /// Updates only the text (keeps color / size / lifetime).
    /// </summary>
    public void SetMessage(
        string message)
    {
        if (text != null)
        {
            text.text = message;
        }
    }
    public void SetScale(float scale)
    {
        transform.localScale = Vector3.one * scale;
    }


    /// <summary>
    /// Updates the color (used by the dungeon zone: red -> green).
    /// </summary>
    public void SetColor(
        Color color)
    {
        baseColor = color;

        if (text != null)
        {
            text.color = color;
        }
    }


    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (text == null)
        {
            text = GetComponent<TMP_Text>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Start()
    {
        if (text != null &&
            !initialized)
        {
            text.color = baseColor;
        }
    }

    private void LateUpdate()
    {
        FaceCamera();

        UpdateLifetime();
    }


    // =========================================================
    // CAMERA
    // =========================================================

    private void FaceCamera()
    {
        if (targetCamera == null &&
            Camera.main != null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(
                targetCamera.transform.forward
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                faceSpeed * Time.deltaTime
            );
    }


    // =========================================================
    // LIFETIME
    // =========================================================

    private void UpdateLifetime()
    {
        if (lifetime <= 0f)
            return;

        if (!fadingOut)
        {
            timeAlive += Time.deltaTime;

            if (timeAlive >= lifetime)
            {
                fadingOut = true;
                timeAlive = 0f;
            }

            return;
        }

        // ------------------------------------------------
        // Fading out
        // ------------------------------------------------

        timeAlive += Time.deltaTime;

        float alpha =
            Mathf.Clamp01(
                1f -
                timeAlive /
                fadeOutSeconds
            );

        if (text != null)
        {
            text.color =
                new Color(
                    baseColor.r,
                    baseColor.g,
                    baseColor.b,
                    alpha
                );
        }

        // Preserve billboard while fading.
        FaceCamera();

        if (timeAlive >= fadeOutSeconds)
        {
            Destroy(gameObject);
        }
    }
}