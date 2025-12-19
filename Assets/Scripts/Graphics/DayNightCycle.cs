using UnityEngine;

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

    [SerializeField]
    [Tooltip("The color of the sun's light throughout the day.")]
    private Gradient sunColor;

    [SerializeField]
    [Tooltip("The color of the scene's ambient light throughout the day.")]
    private Gradient ambientLightColor;

    [SerializeField]
    [Tooltip("The intensity/brightness of the sun throughout the day.")]
    private AnimationCurve sunIntensity;

    // Internal timekeeping value (0-1), calculated from startTime and updated at runtime.
    [SerializeField, HideInInspector, Range(0f, 1f)]
    private float currentTimeOfDay;

    /// <summary>
    /// The current time of day, represented as a value from 0 (midnight) to 1 (next midnight).
    /// </summary>
    public float CurrentTimeOfDay => currentTimeOfDay;

    private void OnValidate()
    {
        // Convert the user-friendly time to the internal 0-1 format
        currentTimeOfDay = (startTime.hours / 24f) + (startTime.minutes / (24f * 60f));
        UpdateSunAndLighting(); // Update lighting in editor for real-time feedback
    }

    private void Update()
    {
        // Advance the time of day
        if (dayDurationInMinutes > 0)
        {
            currentTimeOfDay += Time.deltaTime / (dayDurationInMinutes * 60f);
            currentTimeOfDay %= 1f; // Loop back to 0 after reaching 1
        }

        UpdateSunAndLighting();
    }

    /// <summary>
    /// Updates the sun's rotation, color, intensity, and the scene's ambient light based on the current time of day.
    /// </summary>
    private void UpdateSunAndLighting()
    {
        if (sun == null) return;

        // Rotate the sun
        sun.transform.localRotation = Quaternion.Euler(currentTimeOfDay * 360f, -30f, 0);

        // Set color and intensity from the gradient and curve
        sun.color = sunColor.Evaluate(currentTimeOfDay);
        sun.intensity = sunIntensity.Evaluate(currentTimeOfDay);

        // Set ambient light
        RenderSettings.ambientLight = ambientLightColor.Evaluate(currentTimeOfDay);
    }
}
