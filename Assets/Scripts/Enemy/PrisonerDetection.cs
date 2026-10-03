using UnityEngine;

public class PrisonerDetection : MonoBehaviour
{
    [SerializeField] private PrisonerCrawler prisoner;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        prisoner.WakeUp();
    }
}