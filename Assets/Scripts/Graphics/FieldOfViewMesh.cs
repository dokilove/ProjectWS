using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FieldOfViewMesh : MonoBehaviour
{
    [SerializeField] private Material fovMaterial;
    [SerializeField] private Color fovColor = new Color(1f, 0f, 0f, 0.2f);

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh fovMesh;
    private Material originalMaterial;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        fovMesh = new Mesh();
        fovMesh.name = "FOV Mesh";
        meshFilter.mesh = fovMesh;

        if (fovMaterial != null)
        {
            meshRenderer.material = fovMaterial;
        }
        else
        {
            // Create a default material if none is assigned
            var defaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            defaultMaterial.SetColor("_BaseColor", fovColor);
            defaultMaterial.SetFloat("_Surface", 1); // Set to transparent
            meshRenderer.material = defaultMaterial;
        }
        originalMaterial = meshRenderer.material; // Store the initial material instance
        
        meshRenderer.enabled = false;
    }

    public void GenerateMesh(float angle, float radius, int segments = 20)
    {
        GenerateSectorMesh(angle, radius, segments);
    }

    /// <summary>
    /// 부채꼴(Sector) 형태의 메쉬를 생성합니다.
    /// </summary>
    public void GenerateSectorMesh(float angle, float radius, int segments = 20)
    {
        if (fovMesh == null) return;

        fovMesh.Clear();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Center vertex
        vertices.Add(Vector3.zero);

        // Arc vertices
        float currentAngle = -angle / 2f;
        float angleIncrement = angle / segments;

        for (int i = 0; i <= segments; i++)
        {
            Quaternion rotation = Quaternion.Euler(0, currentAngle, 0);
            Vector3 direction = rotation * Vector3.forward;
            vertices.Add(direction * radius);
            currentAngle += angleIncrement;
        }

        // Triangles
        for (int i = 0; i < segments; i++)
        {
            triangles.Add(0);       // Center vertex
            triangles.Add(i + 1);   // Current vertex on the arc
            triangles.Add(i + 2);   // Next vertex on the arc
        }

        fovMesh.vertices = vertices.ToArray();
        fovMesh.triangles = triangles.ToArray();
        fovMesh.RecalculateNormals();
    }

    /// <summary>
    /// 직사각형(Box) 형태의 메쉬를 생성합니다. (찌르기 / 돌진)
    /// </summary>
    public void GenerateBoxMesh(float width, float length, float forwardOffset = 0f)
    {
        if (fovMesh == null) return;

        fovMesh.Clear();

        float halfWidth = width * 0.5f;
        float zStart = forwardOffset;
        float zEnd = forwardOffset + length;

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-halfWidth, 0f, zStart), // 0: Bottom-Left
            new Vector3(halfWidth, 0f, zStart),  // 1: Bottom-Right
            new Vector3(-halfWidth, 0f, zEnd),   // 2: Top-Left
            new Vector3(halfWidth, 0f, zEnd)     // 3: Top-Right
        };

        int[] triangles = new int[]
        {
            0, 2, 1,
            1, 2, 3
        };

        fovMesh.vertices = vertices;
        fovMesh.triangles = triangles;
        fovMesh.RecalculateNormals();
    }

    /// <summary>
    /// 전방 원형(Circle) 형태의 메쉬를 생성합니다. (내려찍기 / 바닥 폭발)
    /// </summary>
    public void GenerateCircleMesh(float radius, float forwardOffset = 0f, int segments = 24)
    {
        if (fovMesh == null) return;

        fovMesh.Clear();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        Vector3 center = new Vector3(0f, 0f, forwardOffset);
        vertices.Add(center); // 0: Center

        float angleStep = 360f / segments;
        for (int i = 0; i <= segments; i++)
        {
            float rad = Mathf.Deg2Rad * (i * angleStep);
            float x = Mathf.Sin(rad) * radius;
            float z = forwardOffset + Mathf.Cos(rad) * radius;
            vertices.Add(new Vector3(x, 0f, z));
        }

        for (int i = 0; i < segments; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
        }

        fovMesh.vertices = vertices.ToArray();
        fovMesh.triangles = triangles.ToArray();
        fovMesh.RecalculateNormals();
    }

    public void SetActive(bool active)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = active;
        }
    }

    public void SetColor(Color color)
    {
        fovColor = color;
        // Only apply color if no explicit material is set and we are using the default one.
        if (fovMaterial == null && meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.SetColor("_BaseColor", fovColor);
        }
    }

    public void SetMaterial(Material newMaterial)
    {
        if (meshRenderer != null && newMaterial != null)
        {
            meshRenderer.material = newMaterial;
        }
    }

    public void RevertMaterial()
    {
        if (meshRenderer != null && originalMaterial != null)
        {
            meshRenderer.material = originalMaterial;
        }
    }
}
