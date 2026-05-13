using UnityEngine;
using System.Collections;

public class PooledEffect : MonoBehaviour
{
    private ParticleSystem ps;
    public string poolTag; // Added to store the tag of the pool this effect belongs to

    void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogWarning("PooledEffect script requires a ParticleSystem component on the same GameObject or a child.");
        }
    }

    void OnEnable()
    {
        if (ps != null)
        {
            ps.Play();
            // Use the actual duration of the particle system
            float totalDuration = ps.main.duration + ps.main.startLifetime.constantMax;
            StartCoroutine(ReturnToPoolAfterDelay(totalDuration));
        }
        else
        {
            // If no particle system, return immediately or after a short default delay
            StartCoroutine(ReturnToPoolAfterDelay(1f)); 
        }
    }

    IEnumerator ReturnToPoolAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (EffectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
        {
            EffectPoolManager.Instance.ReturnPooledObject(poolTag, gameObject);
        }
        else
        {
            // Fallback if manager is gone or tag is not set
            gameObject.SetActive(false);
        }
    }
}
