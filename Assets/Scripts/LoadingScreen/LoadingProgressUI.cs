using System.Collections;
using TMPro;
using UnityEngine;

public class LoadingProgressUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text percentageText;

    [Header("Testing")]
    [SerializeField] private bool testProgress = true;
    [SerializeField] private float testDuration = 5f;

    private float currentProgress;

    private void Start()
    {
        SetProgress(0f);

        // TEMPORARY:
        // Simulates loading from 0% to 100%.
        if (testProgress)
        {
            StartCoroutine(TestLoadingProgress());
        }
    }

    public void SetProgress(float progress)
    {
        currentProgress = Mathf.Clamp01(progress);

        int percentage =
            Mathf.RoundToInt(currentProgress * 100f);

        if (percentageText != null)
        {
            percentageText.text = percentage + "%";
        }
    }

    private IEnumerator TestLoadingProgress()
    {
        float elapsedTime = 0f;

        while (elapsedTime < testDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                elapsedTime / testDuration;

            SetProgress(progress);

            yield return null;
        }

        SetProgress(1f);
    }
}