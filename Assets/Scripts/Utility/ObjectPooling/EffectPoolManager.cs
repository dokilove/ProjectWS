using System.Collections.Generic;
using UnityEngine;

public class EffectPoolManager : MonoBehaviour
{
    public static EffectPoolManager Instance { get; private set; }

    [System.Serializable]
    public class EffectPoolSetup
    {
        public string tag;
        public GameObject prefab;
        public int initialSize;
    }

    public List<EffectPoolSetup> effectPoolSetups;

    private Dictionary<string, ObjectPool> pools = new Dictionary<string, ObjectPool>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep manager alive across scenes
            InitializePools();
        }
    }

    private void InitializePools()
    {
        foreach (var setup in effectPoolSetups)
        {
            GameObject poolGO = new GameObject($"Pool_{setup.tag}");
            poolGO.transform.SetParent(this.transform);
            ObjectPool pool = poolGO.AddComponent<ObjectPool>();
            pool.prefab = setup.prefab;
            pool.initialPoolSize = setup.initialSize;
            pools.Add(setup.tag, pool);
        }
    }

    public GameObject GetPooledObject(string tag, Vector3 position, Quaternion rotation)
    {
        if (pools.ContainsKey(tag))
        {
            GameObject obj = pools[tag].GetPooledObject();
            obj.transform.position = position;
            obj.transform.rotation = rotation;

            PooledEffect pooledEffect = obj.GetComponent<PooledEffect>();
            if (pooledEffect != null)
            {
                pooledEffect.poolTag = tag; // Set the tag for the pooled effect
            }
            return obj;
        }
        Debug.LogWarning($"Pool with tag {tag} does not exist.");
        return null;
    }

    public void ReturnPooledObject(string tag, GameObject obj)
    {
        if (pools.ContainsKey(tag))
        {
            pools[tag].ReturnPooledObject(obj);
        }
        else
        {
            Debug.LogWarning($"Pool with tag {tag} does not exist. Destroying object instead.");
            Destroy(obj);
        }
    }
}
