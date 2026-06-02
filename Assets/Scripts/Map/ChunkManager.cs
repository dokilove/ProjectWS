using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For LINQ operations like ToArray()

public class ChunkManager : MonoBehaviour
{
    [Header("References")]

    [Header("Culling Settings")]
    [SerializeField] private float checkInterval = 0.4f; // 거리 체크 주기 (0.3초 ~ 0.5초)
    [SerializeField] private float cullingDistance = 120f; // 컬링 한계 거리
    [SerializeField] private string chunkRootTag = "Chunk_Root"; // 씬 내 청크 루트 GameObject를 찾기 위한 태그
    [SerializeField] private string renderGroupName = "RenderGroup"; // 청크 내 그래픽 요소 그룹의 이름

    // 캐싱된 청크 정보 구조체
    private struct CachedChunk
    {
        public Transform chunkRootTransform;
        public GameObject renderGroupGameObject;
        public Vector3 chunkCenterXZ; // 청크의 XZ 평면 중심 좌표
    }

    private List<CachedChunk> allCachedChunks = new List<CachedChunk>();

    void Start()
    {
        // PlayerPawnManager에서 플레이어 트랜스폼을 가져옵니다.
        // PlayerPawnManager가 Awake에서 초기화되므로 Start에서는 접근 가능합니다.
        if (PlayerPawnManager.ActivePlayerTransform == null)
        {
            Debug.LogError("ChunkManager: PlayerPawnManager.ActivePlayerTransform is null. Ensure PlayerPawnManager is initialized and a pawn is possessed. Disabling ChunkManager.");
            enabled = false;
            return;
        }
        // playerTransform 멤버 변수는 더 이상 사용하지 않습니다.
        // CheckCullingStatus에서 PlayerPawnManager.ActivePlayerTransform을 직접 사용합니다.

        // 씬에 배치된 모든 청크 루트 GameObject를 찾아서 캐싱
        GameObject[] chunkRoots = GameObject.FindGameObjectsWithTag(chunkRootTag);
        if (chunkRoots.Length == 0)
        {
            Debug.LogWarning($"ChunkManager: No GameObjects found with tag '{chunkRootTag}'. Ensure your pre-placed chunks are tagged correctly.");
            enabled = false;
            return;
        }

        foreach (GameObject rootGO in chunkRoots)
        {
            Transform renderGroup = rootGO.transform.Find(renderGroupName);
            if (renderGroup != null)
            {
                allCachedChunks.Add(new CachedChunk
                {
                    chunkRootTransform = rootGO.transform,
                    renderGroupGameObject = renderGroup.gameObject,
                    chunkCenterXZ = GetChunkCenterXZ(rootGO.transform)
                });
            }
            else
            {
                Debug.LogWarning($"ChunkManager: Chunk '{rootGO.name}' does not have a child GameObject named '{renderGroupName}'. Culling will not work for this chunk.");
            }
        }

        if (allCachedChunks.Count == 0)
        {
            Debug.LogWarning("ChunkManager: No valid chunks with RenderGroup found for culling. Disabling ChunkManager.");
            enabled = false;
            return;
        }

        // 주기적으로 컬링 상태를 체크하는 루틴 시작
        InvokeRepeating(nameof(CheckCullingStatus), 0f, checkInterval);
        Debug.Log($"ChunkManager initialized with {allCachedChunks.Count} chunks for culling.");
    }

    // 청크의 XZ 평면 중심 좌표 계산
    private Vector3 GetChunkCenterXZ(Transform chunkRoot)
    {
        // 청크의 위치는 80의 배수이므로, 중심은 위치 + 40f
        return new Vector3(chunkRoot.position.x + 40f, 0f, chunkRoot.position.z + 40f);
    }

    private void CheckCullingStatus()
    {
        if (PlayerPawnManager.ActivePlayerTransform == null)
        {
            Debug.LogWarning("ChunkManager: PlayerPawnManager.ActivePlayerTransform is null during culling check. Skipping.");
            return;
        }

        Vector3 playerPosXZ = new Vector3(PlayerPawnManager.ActivePlayerTransform.position.x, 0f, PlayerPawnManager.ActivePlayerTransform.position.z);

        foreach (CachedChunk chunk in allCachedChunks)
        {
            float distance = Vector3.Distance(playerPosXZ, chunk.chunkCenterXZ);

            if (distance > cullingDistance)
            {
                // 방어적 설계: Ground_Navmesh가 꺼지지 않도록 RenderGroup만 제어
                if (chunk.renderGroupGameObject.activeSelf)
                {
                    chunk.renderGroupGameObject.SetActive(false);
                    // Debug.Log($"Culling: {chunk.chunkRootTransform.name} RenderGroup set to false (Distance: {distance:F2})");
                }
            }
            else
            {
                if (!chunk.renderGroupGameObject.activeSelf)
                {
                    chunk.renderGroupGameObject.SetActive(true);
                    // Debug.Log($"Culling: {chunk.chunkRootTransform.name} RenderGroup set to true (Distance: {distance:F2})");
                }
            }
        }
    }

    // 방어적 설계: ChunkManager는 청크의 루트를 끄지 않습니다.
    // NavMesh는 Ground_Navmesh에 사전 베이킹되어 있으므로, Ground_Navmesh는 항상 활성화되어 있어야 합니다.
    // 이 스크립트는 오직 RenderGroup의 가시성만 제어합니다.
}