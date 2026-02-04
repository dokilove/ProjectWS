using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

public class DamageIndicatorManager : MonoBehaviour
{
    public static DamageIndicatorManager Instance { get; private set; }

    [SerializeField] private VisualTreeAsset damageNumberAsset;
    [SerializeField] private UIDocument worldSpaceUIDocument;
    [SerializeField] private float lifeTime = 1f;
    [SerializeField] private float floatSpeed = 50f;
    [SerializeField] private int poolSize = 20;

    private Camera mainCamera;
    private VisualElement root;
    private Queue<Label> damageNumberPool = new Queue<Label>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        mainCamera = Camera.main;
        if (worldSpaceUIDocument == null)
        {
            worldSpaceUIDocument = GetComponent<UIDocument>();
        }
        root = worldSpaceUIDocument.rootVisualElement;

        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            Label label = damageNumberAsset.Instantiate().Q<Label>();
            label.style.display = DisplayStyle.None;
            root.Add(label);
            damageNumberPool.Enqueue(label);
        }
    }

    public void ShowDamage(Vector3 worldPosition, int damage)
    {
        if (damageNumberPool.Count == 0)
        {
            Debug.LogWarning("DamageIndicator pool is empty. Consider increasing pool size.");
            return;
        }

        Label damageLabel = damageNumberPool.Dequeue();
        
        damageLabel.text = damage.ToString();

        StartCoroutine(AnimateDamageNumber(damageLabel, worldPosition));
    }

    private IEnumerator AnimateDamageNumber(Label label, Vector3 worldPosition)
    {
        float timer = 0f;
        Vector2 initialScreenPos = WorldToScreen(worldPosition);

        label.style.display = DisplayStyle.Flex;
        label.style.opacity = 1f;

        while (timer < lifeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / lifeTime;

            // Move up from the initial position
            float yOffset = progress * floatSpeed;
            
            label.style.left = initialScreenPos.x;
            label.style.bottom = initialScreenPos.y + yOffset;

            // Fade out in the last half of its lifetime
            if (progress > 0.5f)
            {
                label.style.opacity = 1 - ((progress - 0.5f) * 2);
            }

            yield return null;
        }

        // Reset and return to pool
        label.style.display = DisplayStyle.None;
        damageNumberPool.Enqueue(label);
    }

    private Vector2 WorldToScreen(Vector3 worldPosition)
    {
        // WorldToScreenPoint's Y coordinate is from the bottom, which matches 'style.bottom'.
        return mainCamera.WorldToScreenPoint(worldPosition);
    }
}
