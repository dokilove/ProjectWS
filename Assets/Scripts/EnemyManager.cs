using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }

    [SerializeField]
    private int maxActiveEnemies = 50; // Global limit for all spawners

    private HashSet<GameObject> activeEnemies = new HashSet<GameObject>();

    public int MaxActiveEnemies => maxActiveEnemies;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterEnemy(GameObject enemy)
    {
        activeEnemies.Add(enemy);
    }

    public void DeregisterEnemy(GameObject enemy)
    {
        activeEnemies.Remove(enemy);
    }

    public int GetActiveEnemyCount()
    {
        // Prune any destroyed enemies just in case
        activeEnemies.RemoveWhere(e => e == null);
        return activeEnemies.Count;
    }
}
