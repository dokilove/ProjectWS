using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    public GameObject prefab;
    public int initialPoolSize = 10;

    private Queue<GameObject> pooledObjects = new Queue<GameObject>();

    void Awake()
    {
        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            pooledObjects.Enqueue(obj);
        }
    }

    public GameObject GetPooledObject()
    {
        if (pooledObjects.Count == 0)
        {
            // If pool is empty, create a new object
            GameObject obj = Instantiate(prefab);
            return obj;
        }

        GameObject objToGet = pooledObjects.Dequeue();
        objToGet.SetActive(true);
        return objToGet;
    }

    public void ReturnPooledObject(GameObject obj)
    {
        obj.SetActive(false);
        pooledObjects.Enqueue(obj);
    }
}
