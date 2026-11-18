using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프리팹별로 발사체 인스턴스를 재사용하는 공용 풀입니다.
/// 반환된 오브젝트는 비활성 상태이므로 호출자가 Init 후 SetActive(true)를 호출해야 합니다.
/// 발사체는 수명이 끝나면 SetActive(false)만 되므로, 비활성 오브젝트가 곧 재사용 가능한 오브젝트입니다.
/// </summary>
public static class ProjectilePool
{
    private static readonly Dictionary<GameObject, List<GameObject>> pools = new Dictionary<GameObject, List<GameObject>>();

    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!pools.TryGetValue(prefab, out List<GameObject> pool))
        {
            pool = new List<GameObject>();
            pools[prefab] = pool;
        }

        for (int i = pool.Count - 1; i >= 0; i--)
        {
            GameObject pooled = pool[i];
            if (pooled == null)
            {
                // 씬 전환 등으로 파괴된 인스턴스 정리
                pool.RemoveAt(i);
                continue;
            }

            if (!pooled.activeSelf)
            {
                pooled.transform.SetPositionAndRotation(position, rotation);
                return pooled;
            }
        }

        GameObject created = Object.Instantiate(prefab, position, rotation);
        created.SetActive(false);
        pool.Add(created);
        return created;
    }
}
