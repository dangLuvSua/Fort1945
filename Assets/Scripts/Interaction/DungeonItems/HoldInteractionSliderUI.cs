using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple runtime-generated "hold to interact" progress bar that appears
/// near the bottom of the screen while the player holds the interact key.
///
/// Built entirely in code so no prefab or scene wiring is required.
/// </summary>
public class HoldInteractionSliderUI : MonoBehaviour
{
    [Header("Runtime References")]
    [SerializeField] private GameObject root;

    [SerializeField] private Image fillImage;

    private static Sprite whiteSprite;

    // =========================================================
    // PUBLIC API
    // =========================================================

    public void Show()
    {
        if (root != null)
        {
            root.SetActive(true);
        }
    }

    public void Hide()
    {
        if (root != null)
        {
            root.SetActive(false);
        }
    }

    public void SetProgress(float progress)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount =
                Mathf.Clamp01(progress);
        }
    }

    // =========================================================
    // FACTORY
    // =========================================================

    public static HoldInteractionSliderUI Create(
        Transform parent)
    {
        // -------------------------------------------------
        // Root container
        // -------------------------------------------------

        GameObject rootGo =
            new GameObject(
                "HoldInteractionSlider",
                typeof(RectTransform)
            );

        rootGo.transform.SetParent(parent, false);

        RectTransform rootRt =
            rootGo.GetComponent<RectTransform>();

        rootRt.anchorMin =
            new Vector2(0.5f, 0f);

        rootRt.anchorMax =
            new Vector2(0.5f, 0f);

        rootRt.pivot =
            new Vector2(0.5f, 0.5f);

        rootRt.anchoredPosition =
            new Vector2(0f, 40f);

        rootRt.sizeDelta =
            new Vector2(280f, 16f);

        // -------------------------------------------------
        // Background bar
        // -------------------------------------------------

        GameObject bgGo =
            new GameObject(
                "Background",
                typeof(RectTransform)
            );

        bgGo.transform.SetParent(
            rootGo.transform,
            false
        );

        RectTransform bgRt =
            bgGo.GetComponent<RectTransform>();

        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        Image bgImage =
            bgGo.AddComponent<Image>();

        bgImage.sprite = GetWhiteSprite();
        bgImage.color =
            new Color(0f, 0f, 0f, 0.65f);
        bgImage.type = Image.Type.Simple;

        // -------------------------------------------------
        // Fill bar
        // -------------------------------------------------

        GameObject fillGo =
            new GameObject(
                "Fill",
                typeof(RectTransform)
            );

        fillGo.transform.SetParent(
            bgGo.transform,
            false
        );

        RectTransform fillRt =
            fillGo.GetComponent<RectTransform>();

        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        Image fillImage =
            fillGo.AddComponent<Image>();

        fillImage.sprite = GetWhiteSprite();
        fillImage.color =
            new Color(0.3f, 1f, 0.35f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin =
            (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;

        // -------------------------------------------------
        // Component
        // -------------------------------------------------

        HoldInteractionSliderUI slider =
            rootGo.AddComponent<HoldInteractionSliderUI>();

        slider.root = rootGo;
        slider.fillImage = fillImage;

        slider.Hide();

        return slider;
    }

    // =========================================================
    // SPRITE
    // =========================================================

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D texture =
                new Texture2D(1, 1);

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            whiteSprite =
                Sprite.Create(
                    texture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f)
                );
        }

        return whiteSprite;
    }
}
