using UnityEngine;
using System.Collections.Generic;

public class ChunkManager : MonoBehaviour
{
    [Header("Culling Settings")]
    [SerializeField] private float checkInterval = 0.4f; // 거리 체크 주기 (0.3초 ~ 0.5초)
    [SerializeField] private float cullingDistance = 120f; // 컬링 한계 거리 (플레이어 ~ 청크 중심, XZ 평면)
    [SerializeField] private string chunkRootTag = "Chunk_Root"; // 씬 내 청크 루트 GameObject를 찾기 위한 태그
    [SerializeField] private string renderGroupName = "RenderGroup"; // 청크 내 그래픽 요소 그룹의 이름

    // 캐싱된 청크 정보
    private class CachedChunk
    {
        public Transform chunkRootTransform;
        public Renderer[] renderers;   // RenderGroup 하위 렌더러 (컬링 대상)
        public Vector3 chunkCenterXZ;  // 청크의 XZ 평면 중심 좌표
        public bool isVisible = true;
    }

    private readonly List<CachedChunk> allCachedChunks = new List<CachedChunk>();

    void Start()
    {
        if (PlayerPawnManager.ActivePlayerTransform == null)
        {
            Debug.LogError("ChunkManager: PlayerPawnManager.ActivePlayerTransform is null. Ensure PlayerPawnManager is initialized and a pawn is possessed. Disabling ChunkManager.");
            enabled = false;
            return;
        }

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
            if (renderGroup == null)
            {
                Debug.LogWarning($"ChunkManager: Chunk '{rootGO.name}' does not have a child GameObject named '{renderGroupName}'. Culling will not work for this chunk.");
                continue;
            }

            Renderer[] renderers = renderGroup.GetComponentsInChildren<Renderer>(true);
            allCachedChunks.Add(new CachedChunk
            {
                chunkRootTransform = rootGO.transform,
                renderers = renderers,
                chunkCenterXZ = GetChunkCenterXZ(rootGO.transform, renderers)
            });
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

    /// <summary>
    /// 청크의 XZ 평면 중심 좌표를 계산합니다.
    /// 피벗 위치(중심/모서리)나 청크 크기를 가정하지 않고, RenderGroup 렌더러들의 월드 바운드 중심을 사용합니다.
    /// (이전에는 "피벗 = 모서리, 크기 80"을 가정해 +40을 더했으나, 실제 프리팹은 피벗이 중심이라 북동쪽 모서리를 중심으로 계산하고 있었음)
    /// </summary>
    private static Vector3 GetChunkCenterXZ(Transform chunkRoot, Renderer[] renderers)
    {
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Vector3 center = hasBounds ? bounds.center : chunkRoot.position;
        return new Vector3(center.x, 0f, center.z);
    }

    private void CheckCullingStatus()
    {
        if (PlayerPawnManager.ActivePlayerTransform == null)
        {
            return;
        }

        Vector3 playerPosition = PlayerPawnManager.ActivePlayerTransform.position;
        Vector3 playerPosXZ = new Vector3(playerPosition.x, 0f, playerPosition.z);
        float cullingDistanceSqr = cullingDistance * cullingDistance;

        foreach (CachedChunk chunk in allCachedChunks)
        {
            bool shouldBeVisible = (playerPosXZ - chunk.chunkCenterXZ).sqrMagnitude <= cullingDistanceSqr;
            if (shouldBeVisible == chunk.isVisible) continue;

            chunk.isVisible = shouldBeVisible;
            SetRenderersEnabled(chunk, shouldBeVisible);
        }
    }

    /// <summary>
    /// 렌더러만 켜고 끕니다. (GameObject를 SetActive(false)하면 벽 콜라이더까지 꺼져
    /// 멀리 있는 발사체가 벽을 통과하고, 경계를 지날 때마다 콜라이더 추가/제거 비용이 발생했음)
    /// </summary>
    private static void SetRenderersEnabled(CachedChunk chunk, bool enabledState)
    {
        Renderer[] renderers = chunk.renderers;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = enabledState;
            }
        }
    }

    // 방어적 설계: ChunkManager는 청크의 루트를 끄지 않으며, 콜라이더(벽, Ground_Navmesh)도 항상 유지합니다.
    // NavMesh는 Ground_Navmesh에 사전 베이킹되어 있으므로, Ground_Navmesh는 항상 활성화되어 있어야 합니다.
    // 이 스크립트는 오직 RenderGroup 하위 렌더러의 가시성만 제어합니다.
}
