using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 화면 밖에 있는 차량의 방향을 HUD(UI Toolkit) 가장자리에 화살표로 표시합니다.
/// PlayerHUD와 같은 UIDocument에 붙여서 사용합니다.
/// UXML의 "vehicle-indicator-layer" 요소를 사용하며, 없으면 코드에서 생성합니다.
/// </summary>
[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(10000)] // 카메라(CinemachineBrain, LateUpdate) 이동 후에 위치를 계산
public class OffscreenVehicleIndicator : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("화면 가장자리에서 인디케이터 중심까지의 여백 (px)")]
    [SerializeField] private float edgePadding = 40f;

    [Tooltip("인디케이터 크기 (px)")]
    [SerializeField] private float indicatorSize = 52f;

    [Tooltip("화면 안쪽으로 이 비율만큼 들어와야 '화면 안'으로 판정 (0 = 화면 경계 그대로)")]
    [Range(0f, 0.2f)]
    [SerializeField] private float onScreenMargin = 0.02f;

    [Tooltip("차량 위치에서 위로 올려서 판정할 높이 (차량 중심 기준)")]
    [SerializeField] private float targetHeightOffset = 1f;

    [Header("Display")]
    [Tooltip("플레이어와의 거리를 표시할지 여부")]
    [SerializeField] private bool showDistance = true;

    [SerializeField] private Color arrowColor = new Color(0.35f, 0.85f, 1f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);

    private const string LayerName = "vehicle-indicator-layer";
    private const string IndicatorClass = "vehicle-indicator";
    private const string ArrowClass = "vehicle-indicator__arrow";
    private const string LabelClass = "vehicle-indicator__label";

    private class Indicator
    {
        public VisualElement root;
        public VisualElement arrow;
        public Label label;
        public int lastDistance = -1;
    }

    private UIDocument uiDocument;
    private VisualElement layer;
    private Camera mainCamera;
    private readonly List<Indicator> indicators = new List<Indicator>();

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        layer = root.Q<VisualElement>(LayerName);
        if (layer == null)
        {
            layer = new VisualElement { name = LayerName };
            layer.AddToClassList(LayerName);
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.top = 0;
            layer.style.right = 0;
            layer.style.bottom = 0;
            root.Add(layer);
        }
        layer.pickingMode = PickingMode.Ignore;

        // UXML이 다시 로드되면 기존 요소는 사라지므로 새로 만든다
        indicators.Clear();
    }

    private void OnDisable()
    {
        foreach (Indicator indicator in indicators)
        {
            indicator.root.RemoveFromHierarchy();
        }
        indicators.Clear();
    }

    private void LateUpdate()
    {
        if (layer == null) return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                HideFrom(0);
                return;
            }
        }

        float panelWidth = layer.layout.width;
        float panelHeight = layer.layout.height;
        if (float.IsNaN(panelWidth) || float.IsNaN(panelHeight) || panelWidth <= 0f || panelHeight <= 0f)
        {
            return; // 첫 레이아웃 전
        }

        Object drivenVehicle = PlayerPawnManager.ActiveVehicle as Object;
        Transform player = PlayerPawnManager.ActivePlayerTransform;

        int used = 0;
        List<Vehicle> vehicles = Vehicle.ActiveVehicles;
        for (int i = 0; i < vehicles.Count; i++)
        {
            Vehicle vehicle = vehicles[i];
            if (vehicle == null || vehicle.IsDead || !vehicle.gameObject.activeInHierarchy) continue;
            if (drivenVehicle != null && vehicle == drivenVehicle) continue; // 운전 중인 차량은 화면 중앙에 있으므로 제외

            Vector3 targetPosition = vehicle.transform.position + Vector3.up * targetHeightOffset;
            if (!TryGetEdgePosition(targetPosition, panelWidth, panelHeight, out Vector2 panelPosition, out float angle))
            {
                continue; // 화면 안에 보이는 차량
            }

            Indicator indicator = GetIndicator(used++);
            indicator.root.style.display = DisplayStyle.Flex;
            indicator.root.style.left = panelPosition.x - indicatorSize * 0.5f;
            indicator.root.style.top = panelPosition.y - indicatorSize * 0.5f;
            indicator.arrow.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));

            if (showDistance && player != null)
            {
                int distance = Mathf.RoundToInt(Vector3.Distance(player.position, vehicle.transform.position));
                if (distance != indicator.lastDistance)
                {
                    indicator.lastDistance = distance;
                    indicator.label.text = distance + "m";
                }
                indicator.label.style.display = DisplayStyle.Flex;
            }
            else
            {
                indicator.label.style.display = DisplayStyle.None;
            }
        }

        HideFrom(used);
    }

    /// <summary>
    /// 대상이 화면 밖이면 화면 가장자리의 패널 좌표와 화살표 각도(도, 패널 기준 +X에서 시계 방향)를 반환합니다.
    /// </summary>
    private bool TryGetEdgePosition(Vector3 worldPosition, float panelWidth, float panelHeight, out Vector2 panelPosition, out float angle)
    {
        panelPosition = Vector2.zero;
        angle = 0f;

        Vector3 viewport = mainCamera.WorldToViewportPoint(worldPosition);
        bool isBehind = viewport.z < 0f;

        if (!isBehind &&
            viewport.x >= onScreenMargin && viewport.x <= 1f - onScreenMargin &&
            viewport.y >= onScreenMargin && viewport.y <= 1f - onScreenMargin)
        {
            return false;
        }

        // 화면 중심 기준 방향 (카메라 뒤에 있으면 투영이 반전되므로 뒤집음)
        Vector2 direction = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
        if (isBehind)
        {
            direction = -direction;
        }

        // 뷰포트(아래→위가 +Y)를 패널 좌표(위→아래가 +Y) 픽셀 방향으로 변환
        Vector2 panelDirection = new Vector2(direction.x * panelWidth, -direction.y * panelHeight);
        if (panelDirection.sqrMagnitude < 0.0001f)
        {
            panelDirection = Vector2.down; // 정확히 정면 뒤인 경우 화면 아래쪽으로 표시
        }

        // 여백을 둔 사각형 경계와 방향 벡터의 교점
        Vector2 center = new Vector2(panelWidth * 0.5f, panelHeight * 0.5f);
        float halfWidth = Mathf.Max(1f, center.x - edgePadding);
        float halfHeight = Mathf.Max(1f, center.y - edgePadding);
        float scaleX = Mathf.Abs(panelDirection.x) > 0.0001f ? halfWidth / Mathf.Abs(panelDirection.x) : float.MaxValue;
        float scaleY = Mathf.Abs(panelDirection.y) > 0.0001f ? halfHeight / Mathf.Abs(panelDirection.y) : float.MaxValue;
        float scale = Mathf.Min(scaleX, scaleY);

        panelPosition = center + panelDirection * scale;
        angle = Mathf.Atan2(panelDirection.y, panelDirection.x) * Mathf.Rad2Deg;
        return true;
    }

    private Indicator GetIndicator(int index)
    {
        while (indicators.Count <= index)
        {
            indicators.Add(CreateIndicator());
        }
        return indicators[index];
    }

    private void HideFrom(int index)
    {
        for (int i = index; i < indicators.Count; i++)
        {
            indicators[i].root.style.display = DisplayStyle.None;
        }
    }

    private Indicator CreateIndicator()
    {
        var indicator = new Indicator();

        indicator.root = new VisualElement();
        indicator.root.AddToClassList(IndicatorClass);
        indicator.root.pickingMode = PickingMode.Ignore;
        indicator.root.style.position = Position.Absolute;
        indicator.root.style.width = indicatorSize;
        indicator.root.style.height = indicatorSize;

        // 화살표: +X 방향(오른쪽)을 가리키도록 그리고, 요소 자체를 회전
        indicator.arrow = new VisualElement();
        indicator.arrow.AddToClassList(ArrowClass);
        indicator.arrow.pickingMode = PickingMode.Ignore;
        indicator.arrow.style.position = Position.Absolute;
        indicator.arrow.style.left = 0;
        indicator.arrow.style.top = 0;
        indicator.arrow.style.right = 0;
        indicator.arrow.style.bottom = 0;
        indicator.arrow.generateVisualContent += DrawArrow;
        indicator.root.Add(indicator.arrow);

        indicator.label = new Label();
        indicator.label.AddToClassList(LabelClass);
        indicator.label.pickingMode = PickingMode.Ignore;
        indicator.root.Add(indicator.label);

        layer.Add(indicator.root);
        return indicator;
    }

    private void DrawArrow(MeshGenerationContext context)
    {
        Rect rect = context.visualElement.contentRect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        Painter2D painter = context.painter2D;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        float bodyRadius = radius * 0.62f;

        // 원형 배경
        painter.fillColor = backgroundColor;
        painter.BeginPath();
        painter.Arc(center, bodyRadius, 0f, 360f);
        painter.Fill();

        // 원 테두리
        painter.strokeColor = arrowColor;
        painter.lineWidth = 2f;
        painter.BeginPath();
        painter.Arc(center, bodyRadius, 0f, 360f);
        painter.Stroke();

        // 바깥쪽(+X)을 가리키는 삼각형
        float tipX = center.x + radius;
        float baseX = center.x + bodyRadius - 1f;
        float halfBase = radius * 0.32f;
        painter.fillColor = arrowColor;
        painter.BeginPath();
        painter.MoveTo(new Vector2(tipX, center.y));
        painter.LineTo(new Vector2(baseX, center.y - halfBase));
        painter.LineTo(new Vector2(baseX, center.y + halfBase));
        painter.ClosePath();
        painter.Fill();
    }
}
