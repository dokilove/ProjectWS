using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class EnemyCountController : MonoBehaviour
{
    private Label enemyCountLabel;
    private int lastDisplayedCount = -1;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        enemyCountLabel = root.Q<Label>("enemy-count");
        lastDisplayedCount = -1;
    }

    private void Update()
    {
        if (enemyCountLabel != null && EnemyManager.Instance != null)
        {
            int enemyCount = EnemyManager.Instance.GetActiveEnemyCount();
            // 값이 바뀔 때만 문자열 생성 및 UI 갱신
            if (enemyCount != lastDisplayedCount)
            {
                lastDisplayedCount = enemyCount;
                enemyCountLabel.text = enemyCount.ToString();
            }
        }
    }
}
