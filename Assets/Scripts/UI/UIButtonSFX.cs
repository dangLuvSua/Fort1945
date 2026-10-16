using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonSFX : MonoBehaviour,
    IPointerEnterHandler,
    IPointerClickHandler
{
    [Header("Audio Source")]
    [SerializeField]
    private AudioSource audioSource;

    [Header("Sound Clips")]
    [SerializeField]
    private AudioClip hoverClip;

    [SerializeField]
    private AudioClip clickClip;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField]
    private float hoverVolume = 0.6f;

    [Range(0f, 1f)]
    [SerializeField]
    private float clickVolume = 0.8f;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (audioSource != null &&
            hoverClip != null)
        {
            audioSource.PlayOneShot(
                hoverClip,
                hoverVolume
            );
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (audioSource != null &&
            clickClip != null)
        {
            audioSource.PlayOneShot(
                clickClip,
                clickVolume
            );
        }
    }
}