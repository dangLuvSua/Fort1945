
using UnityEngine;

public class CandleVisual : MonoBehaviour
{
    [SerializeField] private ParticleSystem flame;
    [SerializeField] private Light candleLight;

    private void Awake()
    {
        SetCandleState(false);
    }

    public void SetCandleState(bool isOn)
    {
        if (flame != null)
        {
            if (isOn)
            {
                if (!flame.isPlaying)
                    flame.Play();
            }
            else
            {
                flame.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }
        }

        if (candleLight != null)
            candleLight.enabled = isOn;
    }
}