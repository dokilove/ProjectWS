using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class EnemyCountController : MonoBehaviour
{
    private Label enemyCountLabel;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        enemyCountLabel = root.Q<Label>("enemy-count");
    }

    private void Update()
    {
        if (enemyCountLabel != null && EnemyManager.Instance != null)
        {
            int enemyCount = EnemyManager.Instance.GetActiveEnemyCount();
            enemyCountLabel.text = enemyCount.ToString();
        }
    }
}
