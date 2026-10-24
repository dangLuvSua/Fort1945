using UnityEngine;

// Ilagay sa trigger collider sa pintuan ng Prison room.
// Kapag natamaan ito ng PrisonerCrawler, babalik siya sa spawn point niya.
public class PrisonDoorTrigger : MonoBehaviour
{
    private void Reset()
    {
        Collider c = GetComponent<Collider>();

        if (c != null)
            c.isTrigger = true;
    }
}