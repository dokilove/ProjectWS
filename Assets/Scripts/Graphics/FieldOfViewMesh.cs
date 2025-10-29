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

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        fovMesh = new Mesh();
        fovMesh.name = "FOV Mesh";
        meshFilter.mesh = fovMesh;

        if (fovMaterial != null)
        {
            meshRenderer.sharedMaterial = fovMaterial;
        }
        else
        {
            // Create a default material if none is assigned
            var defaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            defaultMaterial.SetColor("_BaseColor", fovColor);
            defaultMaterial.SetFloat("_Surface", 1); // Set to transparent
            meshRenderer.material = defaultMaterial;
        }
        
        meshRenderer.enabled = false;
    }

    public void GenerateMesh(float angle, float radius, int segments = 20)
    {
        if (fovMesh == null) return;

        fovMesh.Clear();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Center vertex
        vertices.Add(Vector3.zero);

        // Arc vertices
        float currentAngle = -angle / 2;
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

    public void SetActive(bool active)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = active;
        }
    }
}