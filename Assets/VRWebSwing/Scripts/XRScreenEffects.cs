using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class XRScreenEffects : MonoBehaviour
{
    [Header("Distance Fog")]
    public bool fogEnabled = true;

    [Tooltip("안개가 시작되는 거리. Unity 월드 단위.")]
    [Min(0f)]
    public float fogStartDistance = 2f;

    [Tooltip("설정한 최대 농도에 도달하는 거리.")]
    [Min(0.01f)]
    public float fogEndDistance = 15f;

    [Tooltip("최대 불투명도. 0: 없음, 1: 완전히 가림.")]
    [Range(0f, 1f)]
    public float fogDensity = 0.85f;

    public Color fogColor = new Color(0.65f, 0.7f, 0.75f);

    [Tooltip("깊이가 없는 배경에도 최대 농도를 적용.")]
    public bool fogSkybox = false;

    [Header("Independent Vignette")]
    public bool vignetteEnabled = false;

    [Range(0f, 1f)]
    public float vignetteIntensity = 0.7f;

    [Tooltip("중앙에서 효과가 시작되는 반경. 작을수록 많이 가림.")]
    [Range(0f, 1.5f)]
    public float vignetteRadius = 0.35f;

    [Tooltip("경계가 부드럽게 변하는 폭.")]
    [Range(0.01f, 1.5f)]
    public float vignetteSoftness = 0.6f;

    public Vector2 vignetteCenter = new Vector2(0.5f, 0.5f);
    public Color vignetteColor = Color.black;

    // 다른 스크립트 또는 UnityEvent에서 호출.
    public void SetFogEnabled(bool value) => fogEnabled = value;
    public void SetVignetteEnabled(bool value) => vignetteEnabled = value;

    public void ShowVignette() => vignetteEnabled = true;
    public void HideVignette() => vignetteEnabled = false;

    public void SetFogDensity(float value)
    {
        fogDensity = Mathf.Clamp01(value);
    }

    public void SetFogRange(float start, float end)
    {
        fogStartDistance = Mathf.Max(0f, start);
        fogEndDistance = Mathf.Max(fogStartDistance + 0.01f, end);
    }

    public void SetVignetteIntensity(float value)
    {
        vignetteIntensity = Mathf.Clamp01(value);
    }

    private void OnValidate()
    {
        SetFogRange(fogStartDistance, fogEndDistance);
        SetFogDensity(fogDensity);
        SetVignetteIntensity(vignetteIntensity);

        vignetteRadius = Mathf.Max(0f, vignetteRadius);
        vignetteSoftness = Mathf.Max(0.01f, vignetteSoftness);

        vignetteCenter.x = Mathf.Clamp01(vignetteCenter.x);
        vignetteCenter.y = Mathf.Clamp01(vignetteCenter.y);
    }
}