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
        gameObject.SetActive(true);
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