using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ChunkGridPlacer : EditorWindow
{
    private GameObject chunkPrefab;

    // 여러 청크 프리팹 중 랜덤으로 골라 배치 (비어 있으면 chunkPrefab만 사용)
    [SerializeField] private List<GameObject> randomChunkPrefabs = new List<GameObject>();
    [SerializeField] private string randomPrefabFolder = "Assets/Prefabs/Ground/Generated";
    [SerializeField] private bool randomRotate90 = false;
    [SerializeField] private int placementSeed = 12345;
    private SerializedObject serializedWindow;
    private Vector2 scroll;

    private int gridSizeN = 3;
    private float chunkSize = 80f; // 청크의 월드 유닛 크기 (80x80)
    private Transform parentTransform;
    private bool clearExistingChunks = true;
    private string chunkRootTag = "Chunk_Root"; // 씬 내 청크 루트 GameObject를 찾기 위한 태그

    [MenuItem("Tools/Chunk/Chunk Grid Placer")]
    public static void ShowWindow()
    {
        GetWindow<ChunkGridPlacer>("Chunk Grid Placer");
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("Chunk Grid Placement Settings", EditorStyles.boldLabel);

        chunkPrefab = (GameObject)EditorGUILayout.ObjectField("Chunk Prefab", chunkPrefab, typeof(GameObject), false);

        // --- 랜덤 프리팹 목록 ---
        EditorGUILayout.Space();
        GUILayout.Label("Random Chunk Prefabs (목록이 비어 있으면 위 Chunk Prefab만 사용)", EditorStyles.boldLabel);
        if (serializedWindow == null) serializedWindow = new SerializedObject(this);
        serializedWindow.Update();
        EditorGUILayout.PropertyField(serializedWindow.FindProperty(nameof(randomChunkPrefabs)), true);
        serializedWindow.ApplyModifiedProperties();

        using (new EditorGUILayout.HorizontalScope())
        {
            randomPrefabFolder = EditorGUILayout.TextField("Folder", randomPrefabFolder);
            if (GUILayout.Button("Load Folder", GUILayout.Width(90)))
            {
                LoadPrefabsFromFolder();
            }
        }
        placementSeed = EditorGUILayout.IntField("Placement Seed", placementSeed);
        randomRotate90 = EditorGUILayout.Toggle(new GUIContent("Random 90° Rotation", "청크를 0/90/180/270도로 랜덤 회전해 같은 프리팹도 다르게 보이게 함"), randomRotate90);
        EditorGUILayout.Space();

        gridSizeN = EditorGUILayout.IntField("Grid Size (N)", gridSizeN);
        chunkSize = EditorGUILayout.FloatField("Chunk Size", chunkSize);
        parentTransform = (Transform)EditorGUILayout.ObjectField("Parent Transform", parentTransform, typeof(Transform), true);
        chunkRootTag = EditorGUILayout.TextField("Chunk Root Tag", chunkRootTag);
        clearExistingChunks = EditorGUILayout.Toggle("Clear Existing Chunks", clearExistingChunks);

        if (GUILayout.Button("Place Chunks"))
        {
            PlaceChunks();
        }

        if (GUILayout.Button("Clear All Chunks (by Tag)"))
        {
            ClearChunksByTag();
        }

        EditorGUILayout.EndScrollView();
    }

    private void LoadPrefabsFromFolder()
    {
        if (!AssetDatabase.IsValidFolder(randomPrefabFolder))
        {
            EditorUtility.DisplayDialog("Chunk Grid Placer", $"폴더를 찾을 수 없습니다: {randomPrefabFolder}", "OK");
            return;
        }

        randomChunkPrefabs.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { randomPrefabFolder }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null) randomChunkPrefabs.Add(prefab);
        }
        serializedWindow?.Update();
        Debug.Log($"[ChunkGridPlacer] {randomChunkPrefabs.Count}개 프리팹을 불러왔습니다: {randomPrefabFolder}");
    }

    private List<GameObject> GetPlacementCandidates()
    {
        var candidates = new List<GameObject>();
        foreach (GameObject prefab in randomChunkPrefabs)
        {
            if (prefab != null) candidates.Add(prefab);
        }
        if (candidates.Count == 0 && chunkPrefab != null)
        {
            candidates.Add(chunkPrefab);
        }
        return candidates;
    }

    private void PlaceChunks()
    {
        List<GameObject> candidates = GetPlacementCandidates();
        if (candidates.Count == 0)
        {
            Debug.LogError("Chunk Prefab is not assigned!");
            return;
        }
        var rng = new System.Random(placementSeed);

        if (clearExistingChunks)
        {
            ClearChunksByTag();
        }

        // 그리드의 시작점 계산 (중앙 정렬)
        // 청크 프리팹의 피벗은 청크 중심이므로, 첫 청크 중심 = -(N-1)/2 * 크기 (홀수/짝수 모두 동일)
        // (이전 코드는 홀수 N에서 한 칸의 절반만큼 치우쳐 배치됨: N=3 → -120, -40, 40)
        float startX = -(gridSizeN - 1) * 0.5f * chunkSize;
        float startZ = -(gridSizeN - 1) * 0.5f * chunkSize;

        for (int x = 0; x < gridSizeN; x++)
        {
            for (int z = 0; z < gridSizeN; z++)
            {
                Vector3 position = new Vector3(startX + x * chunkSize, 0, startZ + z * chunkSize);
                
                // 후보 중 랜덤 선택 (시드 고정 시 같은 배치 재현)
                GameObject prefabToPlace = candidates[rng.Next(candidates.Count)];

                // PrefabUtility.InstantiatePrefab을 사용하여 프리팹을 올바르게 인스턴스화
                GameObject newChunk = (GameObject)PrefabUtility.InstantiatePrefab(prefabToPlace);

                // Undo 시스템에 등록하여 실행 취소 가능하게 함
                Undo.RegisterCreatedObjectUndo(newChunk, "Place Chunk");

                newChunk.transform.position = position;
                // 정사각형 청크이므로 90도 단위 회전은 경계가 그대로 맞물림
                newChunk.transform.rotation = randomRotate90 ? Quaternion.Euler(0f, 90f * rng.Next(4), 0f) : Quaternion.identity;

                if (parentTransform != null)
                {
                    newChunk.transform.SetParent(parentTransform);
                }

                // 청크 루트 태그 설정
                newChunk.tag = chunkRootTag;
            }
        }
        Debug.Log($"Successfully placed {gridSizeN * gridSizeN} chunks.");
    }

    private void ClearChunksByTag()
    {
        GameObject[] existingChunks = GameObject.FindGameObjectsWithTag(chunkRootTag);
        if (existingChunks.Length == 0)
        {
            Debug.Log($"No chunks with tag '{chunkRootTag}' found to clear.");
            return;
        }

        foreach (GameObject chunk in existingChunks)
        {
            Undo.DestroyObjectImmediate(chunk); // Undo 시스템에 등록하여 실행 취소 가능하게 함
        }
        Debug.Log($"Cleared {existingChunks.Length} chunks with tag '{chunkRootTag}'.");
    }
}
