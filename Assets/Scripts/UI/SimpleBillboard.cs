// SimpleBillboard.cs
using UnityEngine;

public class SimpleBillboard : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCamera == null) return;
        transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
    }
}