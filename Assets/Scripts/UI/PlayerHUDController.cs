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

    [Header("Speed Settings")]
    [Tooltip("속도 표시 포맷 (기본: {0:F1})")]
    [SerializeField] private string speedFormat = "{0:F1}";
    [Tooltip("체크 시 차량에 탑승 중일 때만 속도를 표시합니다.")]
    [SerializeField] private bool showOnlyInVehicle = false;
    [Tooltip("박치기 최소 충돌 속도 기준치(Threshold)와 도달 여부를 표시합니다.")]
    [SerializeField] private bool showRamThreshold = true;

    // Labels from UXML
    private Label timeLabel;
    private Label enemyCountLabel;
    private Label fpsLabel;
    private Label speedLabel;
    private VisualElement speedContainer;

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
        speedLabel = root.Q<Label>("speed-label");
        speedContainer = root.Q<VisualElement>("VehicleSpeedUI");

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
        UpdateSpeed();
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

    private void UpdateSpeed()
    {
        if (speedLabel == null) return;

        Vehicle vehicle = null;
        if (PlayerPawnManager.ActiveVehicle is Vehicle playerVehicle)
        {
            vehicle = playerVehicle;
        }
        else if (!showOnlyInVehicle && Vehicle.ActiveVehicles.Count > 0)
        {
            vehicle = Vehicle.ActiveVehicles[0];
        }

        if (vehicle != null && vehicle.VehicleMove != null)
        {
            if (speedContainer != null) speedContainer.style.display = DisplayStyle.Flex;

            float currentSpeed = Mathf.Abs(vehicle.VehicleMove.CurrentSpeed);
            float minSpeed = vehicle.VehicleRamSystem != null ? vehicle.VehicleRamSystem.MinRamSpeed : 4f;
            bool isRamReady = currentSpeed >= minSpeed;

            if (showRamThreshold)
            {
                if (isRamReady)
                {
                    speedLabel.text = $"{currentSpeed:F1} [RAM]";
                    speedLabel.style.color = new StyleColor(new Color(0.2f, 1f, 0.3f)); // 녹색: 박치기 발동 가능
                }
                else
                {
                    speedLabel.text = $"{currentSpeed:F1} / {minSpeed:F1}";
                    speedLabel.style.color = new StyleColor(Color.white); // 흰색: 속도 부족
                }
            }
            else
            {
                speedLabel.text = string.Format(speedFormat, currentSpeed);
                speedLabel.style.color = new StyleColor(isRamReady ? new Color(0.2f, 1f, 0.3f) : Color.white);
            }
        }
        else
        {
            if (speedContainer != null)
            {
                speedContainer.style.display = showOnlyInVehicle ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (!showOnlyInVehicle)
            {
                speedLabel.text = "0.0";
                speedLabel.style.color = new StyleColor(Color.white);
            }
        }
    }
}
