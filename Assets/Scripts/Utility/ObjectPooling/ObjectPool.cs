using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    public GameObject prefab;
    public int initialPoolSize = 10;

    private Queue<GameObject> pooledObjects = new Queue<GameObject>();

    /// <summary>
    /// 프리팹과 초기 개수를 지정해 풀을 미리 채웁니다.
    /// AddComponent 직후 Awake가 즉시 실행되어 prefab이 할당되기 전이므로, Awake 대신 이 메서드에서 초기화합니다.
    /// </summary>
    public void Initialize(GameObject poolPrefab, int size)
    {
        prefab = poolPrefab;
        initialPoolSize = size;

        if (prefab == null)
        {
            Debug.LogWarning($"[ObjectPool] '{name}'에 프리팹이 지정되지 않았습니다.", this);
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            pooledObjects.Enqueue(obj);
        }
    }

    public GameObject GetPooledObject()
    {
        // 씬 전환 등으로 파괴된 인스턴스는 건너뜀
        while (pooledObjects.Count > 0)
        {
            GameObject objToGet = pooledObjects.Dequeue();
            if (objToGet == null) continue;

            objToGet.SetActive(true);
            return objToGet;
        }

        if (prefab == null) return null;

        // If pool is empty, create a new object
        return Instantiate(prefab, transform);
    }

    public void ReturnPooledObject(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        pooledObjects.Enqueue(obj);
    }
}
