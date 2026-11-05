using UnityEngine;

public class AverageFPSLogger : MonoBehaviour
{
    [SerializeField, Min(0.01f)]
    private float intervalSeconds = 1f;

    private double elapsedTime;
    private int frameCount;


    private void OnEnable()
    {
        elapsedTime = 0;
        frameCount = 0;
    }

    private void Update()
    {
        elapsedTime += Time.unscaledDeltaTime;
        frameCount++;

        if (elapsedTime >= Mathf.Max(0.01f, intervalSeconds))
        {
            double averageFPS = frameCount / elapsedTime;
            Debug.Log($"ЦђБе FPS: {averageFPS:F2}", this);

            elapsedTime = 0;
            frameCount = 0;
        }
    }
}