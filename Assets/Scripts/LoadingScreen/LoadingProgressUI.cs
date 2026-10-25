using TMPro;
using UnityEngine;

public class LoadingProgressUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text statusText;

    private void Awake()
    {
        SetStatus("LOADING...");
    }

    public void BeginLoading(string message)
    {
        // The MainMenu keeps this screen disabled by setting its scale to zero.
        // Restore it before showing, then put its canvas above the menu UI.
        transform.SetParent(null, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        DontDestroyOnLoad(gameObject);
        gameObject.SetActive(true);

        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in canvases)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;
        }

        SetStatus(message);
    }

    public void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    public void CompleteLoading()
    {
        SetStatus("READY");
    }
}
