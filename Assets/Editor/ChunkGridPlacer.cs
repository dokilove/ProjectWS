using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ChunkGridPlacer : EditorWindow
{
    private GameObject chunkPrefab;
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
        GUILayout.Label("Chunk Grid Placement Settings", EditorStyles.boldLabel);

        chunkPrefab = (GameObject)EditorGUILayout.ObjectField("Chunk Prefab", chunkPrefab, typeof(GameObject), false);
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
    }

    private void PlaceChunks()
    {
        if (chunkPrefab == null)
        {
            Debug.LogError("Chunk Prefab is not assigned!");
            return;
        }

        if (clearExistingChunks)
        {
            ClearChunksByTag();
        }

        // 그리드의 시작점 계산 (중앙 정렬)
        float startX = -(gridSizeN / 2f) * chunkSize;
        float startZ = -(gridSizeN / 2f) * chunkSize;

        // 홀수 그리드 크기일 경우 중앙 정렬을 위해 오프셋 조정
        if (gridSizeN % 2 == 0)
        {
            startX += chunkSize / 2f;
            startZ += chunkSize / 2f;
        }

        for (int x = 0; x < gridSizeN; x++)
        {
            for (int z = 0; z < gridSizeN; z++)
            {
                Vector3 position = new Vector3(startX + x * chunkSize, 0, startZ + z * chunkSize);
                
                // PrefabUtility.InstantiatePrefab을 사용하여 프리팹을 올바르게 인스턴스화
                GameObject newChunk = (GameObject)PrefabUtility.InstantiatePrefab(chunkPrefab);
                
                // Undo 시스템에 등록하여 실행 취소 가능하게 함
                Undo.RegisterCreatedObjectUndo(newChunk, "Place Chunk");

                newChunk.transform.position = position;
                newChunk.transform.rotation = Quaternion.identity;

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
