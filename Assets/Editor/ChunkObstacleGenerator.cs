using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 템플릿 청크(바닥/NavMesh 바닥/RenderGroup 구조)를 기반으로,
/// 크기·회전이 랜덤인 큐브 장애물이 겹치지 않게 배치된 청크 프리팹을 여러 개 생성합니다.
///
/// - 큐브는 템플릿의 RenderGroup/Walls 첫 번째 자식(벽)을 복제해 만들므로 머티리얼, 레이어(Obstacle), 콜라이더, Static 설정이 동일합니다.
/// - 각 장애물은 회전을 고려한 바닥 외접원 + 최소 간격으로 겹침을 검사해 차량/적이 지나갈 통로를 보장합니다.
/// - 청크 가장자리 여백을 두어 이웃 청크와의 경계가 막히지 않게 합니다.
/// - 같은 시드로 다시 생성하면 같은 결과가 나옵니다.
/// 생성 후에는 씬의 NavMeshSurface를 다시 Bake 해야 합니다.
/// </summary>
public class ChunkObstacleGenerator : EditorWindow
{
    private const string DefaultTemplatePath = "Assets/Prefabs/Ground/Chunk_0_0.prefab";

    [SerializeField] private GameObject templatePrefab;
    [SerializeField] private string outputFolder = "Assets/Prefabs/Ground/Generated";
    [SerializeField] private string namePrefix = "Chunk_Cube_";
    [SerializeField] private int startIndex = 1;
    [SerializeField] private int count = 10;
    [SerializeField] private int seed = 12345;

    [SerializeField] private float chunkSize = 80f;
    [SerializeField] private Vector2Int obstacleCountRange = new Vector2Int(6, 12);
    [SerializeField] private Vector2 widthRange = new Vector2(2f, 10f);   // 로컬 X 크기
    [SerializeField] private Vector2 depthRange = new Vector2(2f, 10f);   // 로컬 Z 크기
    [SerializeField] private Vector2 heightRange = new Vector2(1.5f, 4f);
    [SerializeField] private float rotationStep = 0f;                     // 0 = 자유 회전, 예: 15, 45, 90
    [SerializeField] private float edgeMargin = 6f;                       // 청크 가장자리에서 비워 둘 거리
    [SerializeField] private float minGap = 4f;                           // 장애물 사이 최소 통로 폭
    [SerializeField] private float centerClearRadius = 0f;                // 청크 중앙에 비워 둘 반경
    [SerializeField] private int maxAttemptsPerObstacle = 100;

    private Vector2 scroll;

    [MenuItem("Tools/Chunk/Random Cube Chunk Generator")]
    public static void ShowWindow()
    {
        GetWindow<ChunkObstacleGenerator>("Cube Chunk Generator");
    }

    private void OnEnable()
    {
        if (templatePrefab == null)
        {
            templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTemplatePath);
        }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        templatePrefab = (GameObject)EditorGUILayout.ObjectField("Template Prefab", templatePrefab, typeof(GameObject), false);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        namePrefix = EditorGUILayout.TextField("Name Prefix", namePrefix);
        startIndex = EditorGUILayout.IntField("Start Index", startIndex);
        count = Mathf.Max(1, EditorGUILayout.IntField("Count", count));
        using (new EditorGUILayout.HorizontalScope())
        {
            seed = EditorGUILayout.IntField("Seed", seed);
            if (GUILayout.Button("Random", GUILayout.Width(70)))
            {
                seed = System.Environment.TickCount;
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Obstacles", EditorStyles.boldLabel);
        chunkSize = EditorGUILayout.FloatField("Chunk Size", chunkSize);
        obstacleCountRange = EditorGUILayout.Vector2IntField("Count (Min / Max)", obstacleCountRange);
        widthRange = EditorGUILayout.Vector2Field("Width X (Min / Max)", widthRange);
        depthRange = EditorGUILayout.Vector2Field("Depth Z (Min / Max)", depthRange);
        heightRange = EditorGUILayout.Vector2Field("Height (Min / Max)", heightRange);
        rotationStep = EditorGUILayout.FloatField(new GUIContent("Rotation Step", "0이면 자유 회전, 예: 15 / 45 / 90도 단위로 스냅"), rotationStep);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spacing", EditorStyles.boldLabel);
        edgeMargin = EditorGUILayout.FloatField(new GUIContent("Edge Margin", "청크 가장자리에서 비워 둘 거리 (이웃 청크와의 통로)"), edgeMargin);
        minGap = EditorGUILayout.FloatField(new GUIContent("Min Gap", "장애물 사이 최소 통로 폭 (차량 폭보다 넓게)"), minGap);
        centerClearRadius = EditorGUILayout.FloatField(new GUIContent("Center Clear Radius", "청크 중앙에 비워 둘 반경 (0이면 사용 안 함)"), centerClearRadius);
        maxAttemptsPerObstacle = Mathf.Max(1, EditorGUILayout.IntField("Max Attempts / Obstacle", maxAttemptsPerObstacle));

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("생성 후 씬에 배치한 다음, NavMeshSurface를 다시 Bake 해야 적이 새 장애물을 피해 다닙니다.", MessageType.Info);

        if (GUILayout.Button("Generate Chunk Prefabs", GUILayout.Height(30)))
        {
            Generate();
        }

        EditorGUILayout.EndScrollView();
    }

    private void Generate()
    {
        if (templatePrefab == null)
        {
            EditorUtility.DisplayDialog("Cube Chunk Generator", "Template Prefab을 지정해 주세요.", "OK");
            return;
        }

        if (!outputFolder.StartsWith("Assets"))
        {
            EditorUtility.DisplayDialog("Cube Chunk Generator", "Output Folder는 Assets/ 아래여야 합니다.", "OK");
            return;
        }

        // 덮어쓸 파일이 있으면 확인
        int existing = 0;
        for (int i = startIndex; i < startIndex + count; i++)
        {
            if (File.Exists(GetPrefabPath(i))) existing++;
        }
        if (existing > 0 &&
            !EditorUtility.DisplayDialog("Cube Chunk Generator", $"이미 존재하는 프리팹 {existing}개를 덮어씁니다. 계속할까요?", "덮어쓰기", "취소"))
        {
            return;
        }

        EnsureFolder(outputFolder);

        int created = 0;
        int totalPlaced = 0;
        try
        {
            for (int i = startIndex; i < startIndex + count; i++)
            {
                EditorUtility.DisplayProgressBar("Cube Chunk Generator", $"{namePrefix}{i}", (float)(i - startIndex) / count);
                totalPlaced += GenerateOne(i, seed + i);
                created++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[ChunkObstacleGenerator] {created}개 청크 생성 완료 (장애물 총 {totalPlaced}개) → {outputFolder}");
    }

    private string GetPrefabPath(int index) => $"{outputFolder}/{namePrefix}{index}.prefab";

    private int GenerateOne(int index, int chunkSeed)
    {
        // 프리팹 연결 없는 복사본으로 만들어야 템플릿의 기존 벽을 삭제할 수 있음
        GameObject instance = Instantiate(templatePrefab);
        instance.name = $"{namePrefix}{index}";
        int placed = 0;

        try
        {
            Transform renderGroup = instance.transform.Find("RenderGroup");
            if (renderGroup == null)
            {
                Debug.LogError($"[ChunkObstacleGenerator] 템플릿에 'RenderGroup'이 없습니다: {templatePrefab.name}");
                return 0;
            }

            Transform walls = renderGroup.Find("Walls");
            if (walls == null)
            {
                walls = new GameObject("Walls").transform;
                walls.SetParent(renderGroup, false);
                walls.gameObject.layer = renderGroup.gameObject.layer;
            }

            // 첫 번째 기존 벽을 큐브 원본으로 사용 (머티리얼/레이어/콜라이더/Static 유지)
            GameObject obstacleSource = walls.childCount > 0 ? walls.GetChild(0).gameObject : null;
            GameObject fallbackSource = null;
            if (obstacleSource == null)
            {
                fallbackSource = CreateFallbackCube(walls.gameObject.layer);
                obstacleSource = fallbackSource;
            }
            obstacleSource.SetActive(false); // 원본은 복제 후 삭제

            // 기존 벽 제거 (원본은 마지막에 삭제)
            for (int c = walls.childCount - 1; c >= 0; c--)
            {
                GameObject child = walls.GetChild(c).gameObject;
                if (child != obstacleSource)
                {
                    DestroyImmediate(child);
                }
            }

            placed = PlaceObstacles(walls, obstacleSource, new System.Random(chunkSeed));

            DestroyImmediate(obstacleSource);

            TrySetTag(instance, "Chunk_Root");
            PrefabUtility.SaveAsPrefabAsset(instance, GetPrefabPath(index));
        }
        finally
        {
            DestroyImmediate(instance);
        }

        return placed;
    }

    private struct PlacedObstacle
    {
        public Vector2 center;
        public float radius; // 회전을 고려한 바닥 외접원 반지름
    }

    private int PlaceObstacles(Transform parent, GameObject source, System.Random rng)
    {
        float half = chunkSize * 0.5f;
        int minCount = Mathf.Max(0, Mathf.Min(obstacleCountRange.x, obstacleCountRange.y));
        int maxCount = Mathf.Max(obstacleCountRange.x, obstacleCountRange.y);
        int targetCount = rng.Next(minCount, maxCount + 1);

        var placed = new List<PlacedObstacle>(targetCount);

        for (int n = 0; n < targetCount; n++)
        {
            for (int attempt = 0; attempt < maxAttemptsPerObstacle; attempt++)
            {
                float width = Range(rng, widthRange);
                float depth = Range(rng, depthRange);
                float height = Range(rng, heightRange);
                float yaw = Range(rng, 0f, 360f);
                if (rotationStep > 0f)
                {
                    yaw = Mathf.Round(yaw / rotationStep) * rotationStep;
                }

                // 회전과 무관하게 안전한 외접원 반지름
                float radius = 0.5f * Mathf.Sqrt(width * width + depth * depth);
                float limit = half - edgeMargin - radius;
                if (limit <= 0f) continue; // 이 크기로는 청크 안에 들어가지 않음

                Vector2 center = new Vector2(Range(rng, -limit, limit), Range(rng, -limit, limit));

                if (centerClearRadius > 0f && center.magnitude < centerClearRadius + radius) continue;
                if (Overlaps(placed, center, radius)) continue;

                placed.Add(new PlacedObstacle { center = center, radius = radius });
                CreateObstacle(parent, source, n, center, width, depth, height, yaw);
                break;
            }
        }

        return placed.Count;
    }

    private bool Overlaps(List<PlacedObstacle> placed, Vector2 center, float radius)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            float required = placed[i].radius + radius + minGap;
            if ((placed[i].center - center).sqrMagnitude < required * required)
            {
                return true;
            }
        }
        return false;
    }

    private static void CreateObstacle(Transform parent, GameObject source, int index, Vector2 center, float width, float depth, float height, float yaw)
    {
        GameObject obstacle = Instantiate(source, parent);
        obstacle.name = $"Obstacle ({index})";
        obstacle.SetActive(true);

        // 바닥(y=0) 위에 올려 놓기: 큐브 메시 중심 기준이므로 높이의 절반만큼 올림
        obstacle.transform.localPosition = new Vector3(center.x, height * 0.5f, center.y);
        obstacle.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        obstacle.transform.localScale = new Vector3(width, height, depth);
    }

    private static GameObject CreateFallbackCube(int layer)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.layer = LayerMask.NameToLayer("Obstacle") >= 0 ? LayerMask.NameToLayer("Obstacle") : layer;
        GameObjectUtility.SetStaticEditorFlags(cube, (StaticEditorFlags)~0);
        return cube;
    }

    private static void TrySetTag(GameObject go, string tag)
    {
        try
        {
            go.tag = tag;
        }
        catch (UnityException)
        {
            // 태그가 정의되지 않은 프로젝트에서는 무시 (ChunkGridPlacer가 배치 시 태그를 지정)
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static float Range(System.Random rng, Vector2 range) => Range(rng, Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));

    private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
}
