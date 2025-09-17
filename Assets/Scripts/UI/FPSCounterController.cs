
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class FPSCounterController : MonoBehaviour
{
    [SerializeField]
    private float updateInterval = 0.5f;

    private Label fpsLabel;
    private Label msLabel;

    private float accum = 0;
    private int frames = 0;
    private float timeleft;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        fpsLabel = root.Q<Label>("fps-label");
        msLabel = root.Q<Label>("ms-label");
        timeleft = updateInterval;
    }

    void Update()
    {
        timeleft -= Time.unscaledDeltaTime;
        accum += Time.unscaledDeltaTime;
        frames++;

        if (timeleft <= 0.0)
        {
            float fps = frames / accum;
            float ms = (accum / frames) * 1000.0f;

            if (fpsLabel != null)
            {
                fpsLabel.text = $"{fps:F1} FPS";
            }
            if (msLabel != null)
            {
                msLabel.text = $"{ms:F2} ms";
            }

            timeleft = updateInterval;
            accum = 0.0F;
            frames = 0;
        }
    }
}
