using UnityEngine;

/// <summary>
/// Defines the different phases of a day.
/// </summary>
public enum TimePhase
{
    DeepNight, // 00:00 - 03:59
    Morning,   // 04:00 - 09:59
    Noon,      // 10:00 - 15:59
    Evening,   // 16:00 - 19:59
    Night      // 20:00 - 23:59
}

/// <summary>
/// A struct to hold time in a user-friendly hours and minutes format.
/// </summary>
[System.Serializable]
public struct TimeOfDay
{
    [Range(0, 23)]
    public int hours;
    [Range(0, 59)]
    public int minutes;
}

/// <summary>
/// Manages the in-game time of day, controls the sun's position, color, intensity, and ambient lighting.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    [SerializeField]
    [Tooltip("Length of a full day in real-world minutes. For example, 10 means a full 24-hour cycle completes in 10 minutes.")]
    private float dayDurationInMinutes = 10f;

    [SerializeField]
    [Tooltip("Set the initial start time of the day.")]
    private TimeOfDay startTime = new TimeOfDay { hours = 6, minutes = 0 };

    [Header("Sun & Lighting Settings")]
    [SerializeField]
    [Tooltip("The main directional light in the scene that acts as the sun.")]
    private Light sun;

    [SerializeField, Range(-180, 180)]
    [Tooltip("Corrects the base rotation of the sun. Set to -90 to make noon the highest point.")]
    private float sunRotationOffset = -90f;

    [SerializeField]
    [Tooltip("The color of the sun's light throughout the day.")]
    private Gradient sunColor;

    [SerializeField]
    [Tooltip("The color of the scene's ambient light throughout the day.")]
    private Gradient ambientLightColor;

    [SerializeField]
    [Tooltip("The intensity/brightness of the sun throughout the day.")]
    private AnimationCurve sunIntensity;

    [Header("Current State")]
    [SerializeField]
    [Tooltip("The current phase of the day. (Read-only)")]
    private TimePhase currentPhase;

    // Internal timekeeping value (0-1), calculated from startTime and updated at runtime.
    [SerializeField, HideInInspector, Range(0f, 1f)]
    private float currentTimeOfDay;

    /// <summary>
    /// The current time of day, represented as a value from 0 (midnight) to 1 (next midnight).
    /// </summary>
    public float CurrentTimeOfDay => currentTimeOfDay;

    /// <summary>
    /// The current phase of the day (e.g., Morning, Noon, Night).
    /// </summary>
    public TimePhase CurrentPhase => currentPhase;

    private void OnValidate()
    {
        currentTimeOfDay = (startTime.hours / 24f) + (startTime.minutes / (24f * 60f));
        UpdateTimePhase();
        UpdateSunAndLighting();
    }

    private void Update()
    {
        if (dayDurationInMinutes > 0)
        {
            currentTimeOfDay += Time.deltaTime / (dayDurationInMinutes * 60f);
            currentTimeOfDay %= 1f;
        }

        UpdateTimePhase();
        UpdateSunAndLighting();
    }

    private void UpdateTimePhase()
    {
        // These values correspond to the 0-1 time of day.
        // 4:00 = 4/24 = 0.167
        // 10:00 = 10/24 = 0.417
        // 16:00 = 16/24 = 0.667
        // 20:00 = 20/24 = 0.833
        if (currentTimeOfDay >= 0 && currentTimeOfDay < 0.167f)
        {
            currentPhase = TimePhase.DeepNight;
        }
        else if (currentTimeOfDay >= 0.167f && currentTimeOfDay < 0.417f)
        {
            currentPhase = TimePhase.Morning;
        }
        else if (currentTimeOfDay >= 0.417f && currentTimeOfDay < 0.667f)
        {
            currentPhase = TimePhase.Noon;
        }
        else if (currentTimeOfDay >= 0.667f && currentTimeOfDay < 0.833f)
        {
            currentPhase = TimePhase.Evening;
        }
        else // currentTimeOfDay >= 0.833f
        {
            currentPhase = TimePhase.Night;
        }
    }

    private void UpdateSunAndLighting()
    {
        if (sun == null) return;

        // Rotate the sun. An offset is applied to make noon (0.5) be at the top (90 degrees).
        sun.transform.localRotation = Quaternion.Euler((currentTimeOfDay * 360f) + sunRotationOffset, -30f, 0);
        sun.color = sunColor.Evaluate(currentTimeOfDay);
        sun.intensity = sunIntensity.Evaluate(currentTimeOfDay);
        RenderSettings.ambientLight = ambientLightColor.Evaluate(currentTimeOfDay);
    }
}
