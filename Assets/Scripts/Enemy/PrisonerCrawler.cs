using UnityEngine;

public class PrisonerCrawler : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private string playerTag = "Player";

    [Header("Crawling")]
    [SerializeField] private float crawlSpeed = 1.2f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("References")]
    [SerializeField] private Animator animator;

    private Transform player;
    private bool isAwake;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Start in dead/prone state
        isAwake = false;

        if (animator != null)
            animator.SetBool("IsAwake", false);
    }

    private void Update()
    {
        FindPlayer();

        if (player == null)
            return;

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        // Player is close enough
        if (!isAwake && distance <= detectionRange)
        {
            WakeUp();
        }

        // Once awake, crawl toward player
        if (isAwake)
        {
            CrawlTowardPlayer();
        }
    }

    private void FindPlayer()
    {
        if (player != null)
            return;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    public void WakeUp()
    {
        isAwake = true;

        animator.SetBool("IsAwake", true);
    }

    private void CrawlTowardPlayer()
    {
        Vector3 direction = player.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        direction.Normalize();

        // Move
        transform.position +=
            direction * crawlSpeed * Time.deltaTime;

        // Face player
        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}