using TMPro;
using UnityEngine;

public class LoadingDots : MonoBehaviour
{
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private float changeInterval = 0.5f;

    private float timer;
    private int dotCount;

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= changeInterval)
        {
            timer = 0f;

            dotCount++;

            if (dotCount > 3)
                dotCount = 0;

            loadingText.text = "Loading" + new string('.', dotCount);
        }
    }
}