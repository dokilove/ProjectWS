using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class ChunkVariantGenerator : EditorWindow
{
    private GameObject templatePrefab;
    private string outputFolderPath = "Assets/Prefabs/Ground";
    private string namePrefix = "Chunk_0_";
    private int startIndex = 1;
    private int count = 10;

    private float minCoord = -26f;
    private float maxCoord = 26f;
    private float minDistanceBetweenWalls = 11f;

    [MenuItem("Tools/Chunk/Generate Chunk Variants (Chunk_0_1 ~ Chunk_0_10)")]
    public static void GenerateVariantsDirectly()
    {
        string templatePath = "Assets/Prefabs/Ground/Chunk_0_0.prefab";
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(templatePath);
        if (template == null)
        {
            Debug.LogError($"[ChunkVariantGenerator] Template prefab not found at {templatePath}!");
            return;
        }

        GenerateVariantsInternal(template, "Assets/Prefabs/Ground", "Chunk_0_", 1, 10, -26f, 26f, 11f);
    }

    [MenuItem("Tools/Chunk/Chunk Variant Generator Window")]
    public static void ShowWindow()
    {
        GetWindow<ChunkVariantGenerator>("Chunk Variant Generator");
    }

    private void OnEnable()
    {
        if (templatePrefab == null)
        {
            templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ground/Chunk_0_0.prefab");
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Chunk Variant Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        templatePrefab = (GameObject)EditorGUILayout.ObjectField("Template Prefab", templatePrefab, typeof(GameObject), false);
        outputFolderPath = EditorGUILayout.TextField("Output Folder", outputFolderPath);
        namePrefix = EditorGUILayout.TextField("Name Prefix", namePrefix);
        startIndex = EditorGUILayout.IntField("Start Index", startIndex);
        count = EditorGUILayout.IntField("Count", count);

        EditorGUILayout.Space();
        GUILayout.Label("Randomization Settings", EditorStyles.boldLabel);
        minCoord = EditorGUILayout.FloatField("Min X/Z Coord", minCoord);
        maxCoord = EditorGUILayout.FloatField("Max X/Z Coord", maxCoord);
        minDistanceBetweenWalls = EditorGUILayout.FloatField("Min Wall Distance", minDistanceBetweenWalls);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Chunk Prefabs", GUILayout.Height(30)))
        {
            if (templatePrefab == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a template prefab first!", "OK");
                return;
            }

            GenerateVariantsInternal(templatePrefab, outputFolderPath, namePrefix, startIndex, count, minCoord, maxCoord, minDistanceBetweenWalls);
            EditorUtility.DisplayDialog("Complete", $"Generated {count} chunk variants in {outputFolderPath}!", "OK");
        }
    }

    private static void GenerateVariantsInternal(GameObject template, string folder, string prefix, int start, int totalCount, float minC, float maxC, float minDist)
    {
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        int createdCount = 0;
        for (int i = start; i < start + totalCount; i++)
        {
            string prefabName = $"{prefix}{i}";
            string savePath = $"{folder}/{prefabName}.prefab";

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
            instance.name = prefabName;

            // Find Walls container under RenderGroup
            Transform renderGroup = instance.transform.Find("RenderGroup");
            Transform wallsContainer = renderGroup != null ? renderGroup.Find("Walls") : null;

            if (wallsContainer != null)
            {
                List<Vector2> placedPositions = new List<Vector2>();

                for (int w = 0; w < wallsContainer.childCount; w++)
                {
                    Transform wall = wallsContainer.GetChild(w);

                    // Randomly choose horizontal (10, 2, 2) or vertical (2, 2, 10)
                    bool isHorizontal = Random.value > 0.5f;
                    wall.localScale = isHorizontal ? new Vector3(10f, 2f, 2f) : new Vector3(2f, 2f, 10f);

                    // Find a valid non-overlapping position
                    Vector2 chosenPos = Vector2.zero;
                    bool foundValid = false;

                    for (int attempt = 0; attempt < 50; attempt++)
                    {
                        float rx = Mathf.Round(Random.Range(minC, maxC) * 2f) / 2f;
                        float rz = Mathf.Round(Random.Range(minC, maxC) * 2f) / 2f;
                        Vector2 candidate = new Vector2(rx, rz);

                        bool tooClose = false;
                        foreach (var placed in placedPositions)
                        {
                            if (Vector2.Distance(candidate, placed) < minDist)
                            {
                                tooClose = true;
                                break;
                            }
                        }

                        if (!tooClose)
                        {
                            chosenPos = candidate;
                            foundValid = true;
                            break;
                        }
                    }

                    if (!foundValid)
                    {
                        chosenPos = new Vector2(Random.Range(minC, maxC), Random.Range(minC, maxC));
                    }

                    placedPositions.Add(chosenPos);
                    wall.localPosition = new Vector3(chosenPos.x, 1f, chosenPos.y);
                    wall.localRotation = Quaternion.identity;
                }
            }
            else
            {
                Debug.LogWarning($"[ChunkVariantGenerator] 'Walls' container not found in {instance.name}!");
            }

            PrefabUtility.SaveAsPrefabAsset(instance, savePath);
            Object.DestroyImmediate(instance);
            createdCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ChunkVariantGenerator] Successfully generated {createdCount} chunk variants ({prefix}{start} to {prefix}{start + totalCount - 1})!");
    }
}
