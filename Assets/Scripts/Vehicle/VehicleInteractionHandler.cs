using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class VehicleInteractionHandler : MonoBehaviour
{
    [Tooltip("체크하면 홀드 없이 탭으로 탑승합니다.")]
    public bool useTapToEnter = false;

    private UIDocument _uiDocument;
    private VisualElement _promptContainer;
    private float _currentProgress = 0f;

    // This will be drawn by the custom painter
    private VisualElement _progressElement;
    private Camera _mainCamera;

    void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        _mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (_mainCamera == null) return;

        // Billboard effect: Make the UI always face the camera
        transform.rotation = Quaternion.LookRotation(_mainCamera.transform.forward);
    }

    void OnEnable()
    {
        var root = _uiDocument.rootVisualElement;
        // Center the container within the UIDocument's rect
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;

        _promptContainer = root.Q<VisualElement>("prompt-container");
        
        _progressElement = root.Q<VisualElement>("hold-progress");
        if (_progressElement != null)
        {
            _progressElement.generateVisualContent += OnGenerateVisualContent;
        }

        Hide(); // Start hidden
    }

    private void OnDisable()
    {
        if (_progressElement != null)
        {
            _progressElement.generateVisualContent -= OnGenerateVisualContent;
        }
    }

    private void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        var painter = mgc.painter2D;
        var rect = mgc.visualElement.contentRect;
        
        float width = rect.width;
        float height = rect.height;
        
        if (width <= 0 || height <= 0) return;

        // Draw background
        painter.fillColor = new Color(0, 0, 0, 0.5f);
        painter.BeginPath();
        painter.Arc(rect.center, width / 2, 0.0f, 360.0f);
        painter.Fill();

        // Draw progress arc
        if (_currentProgress > 0)
        {
            painter.fillColor = Color.white;
            painter.BeginPath();
            painter.MoveTo(rect.center);
            painter.Arc(rect.center, width / 2, -90.0f, 360.0f * _currentProgress); // -90 to start from the top
            painter.Fill();
        }
    }

    public void Show()
    {
        if (_promptContainer == null) return;
        _promptContainer.style.display = DisplayStyle.Flex;
        UpdateProgress(0);
    }

    public void Hide()
    {
        if (_promptContainer == null) return;
        _promptContainer.style.display = DisplayStyle.None;
    }

    public void UpdateProgress(float progress)
    {
        _currentProgress = Mathf.Clamp01(progress);
        _progressElement?.MarkDirtyRepaint(); // Force the custom painter to redraw
    }
}
