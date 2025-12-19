using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PlayerHUDController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("The DayNightCycle manager that holds the current time.")]
    [SerializeField] private DayNightCycle dayNightCycleManager;

    [Header("FPS Counter Settings")]
    [SerializeField] private float fpsUpdateInterval = 0.5f;

    // Labels from UXML
    private Label timeLabel;
    private Label enemyCountLabel;
    private Label fpsLabel;

    // Variables for FPS calculation
    private float fpsAccumulator = 0;
    private int frameCount = 0;
    private float timeSinceLastUpdate;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // Query for all the labels by name
        timeLabel = root.Q<Label>("time-label");
        enemyCountLabel = root.Q<Label>("enemy-count-label");
        fpsLabel = root.Q<Label>("fps-label");

        // Initialize FPS counter
        timeSinceLastUpdate = fpsUpdateInterval;

        // Basic validation
        if (dayNightCycleManager == null)
        {
            Debug.LogError("PlayerHUDController: DayNightCycle manager is not assigned! Time will not be displayed.", this);
        }
        if (timeLabel == null || enemyCountLabel == null || fpsLabel == null)
        {
            Debug.LogError("PlayerHUDController: One or more UI labels could not be found in the UXML. Check the names ('time-label', 'enemy-count-label', 'fps-label').", this);
        }
    }

    private void Update()
    {
        UpdateTime();
        UpdateEnemyCount();
        UpdateFPS();
    }

    private void UpdateTime()
    {
        if (timeLabel == null || dayNightCycleManager == null) return;

        float time01 = dayNightCycleManager.CurrentTimeOfDay;
        float timeInHours = time01 * 24f;
        int hours = Mathf.FloorToInt(timeInHours);
        int minutes = Mathf.FloorToInt((timeInHours - hours) * 60f);

        timeLabel.text = $"{hours:D2}:{minutes:D2}";
    }

    private void UpdateEnemyCount()
    {
        if (enemyCountLabel == null || EnemyManager.Instance == null) return;
        
        int enemyCount = EnemyManager.Instance.GetActiveEnemyCount();
        enemyCountLabel.text = enemyCount.ToString();
    }

    private void UpdateFPS()
    {
        if (fpsLabel == null) return;

        timeSinceLastUpdate -= Time.unscaledDeltaTime;
        fpsAccumulator += Time.unscaledDeltaTime;
        frameCount++;

        if (timeSinceLastUpdate <= 0.0f)
        {
            float fps = frameCount / fpsAccumulator;
            fpsLabel.text = $"{fps:F1}";

            // Reset for next interval
            timeSinceLastUpdate = fpsUpdateInterval;
            fpsAccumulator = 0.0f;
            frameCount = 0;
        }
    }
}
