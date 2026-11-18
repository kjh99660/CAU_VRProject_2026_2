using UnityEngine;
using VRWebSwing;

public class FragileAnchor : MonoBehaviour
{
    [Header("Fragile Settings")]
    [Tooltip("로프가 처음 연결된 뒤 블록이 부서지기까지 걸리는 시간(초). 이 동안 색이 빨갛게 변한다.")]
    [Min(0)] public float destroyDelay = 1.5f;

    private MeshRenderer meshRenderer;
    private Collider anchorCollider;
    private Color originalColor;

    // 중복 실행을 막기 위한 상태 플래그
    private bool isTriggered = false;
    private bool isBroken = false;
    private float elapsed;
    private WebRope rope;
    private SwingMotor motor;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        anchorCollider = GetComponent<Collider>();
        originalColor = meshRenderer.material.color;
    }

    // 무한 맵 풀에서 재사용될 때 부서지기 전 상태로 복구한다.
    void OnEnable()
    {
        isTriggered = false;
        isBroken = false;
        elapsed = 0f;
        rope = null;
        motor = null;
        meshRenderer.enabled = true;
        meshRenderer.material.color = originalColor;
        if (anchorCollider) anchorCollider.enabled = true;
    }

    // WebRope가 이 블록에 연결에 성공했을 때 호출한다. 첫 연결부터 타이머가 시작된다.
    public void OnRopeAttached(WebRope attachedRope)
    {
        rope = attachedRope;
        if (rope) motor = rope.GetComponent<SwingMotor>();
        if (isTriggered || isBroken) return;
        isTriggered = true;
        elapsed = 0f;
    }

    // 메뉴/중단으로 일시정지(motor.Paused)된 동안에는 타이머가 흐르지 않도록 물리 주기에서 센다.
    void FixedUpdate()
    {
        if (!isTriggered || isBroken) return;
        if (motor && motor.Paused) return;

        elapsed += Time.fixedDeltaTime;
        float t = destroyDelay > 0f ? Mathf.Clamp01(elapsed / destroyDelay) : 1f;
        meshRenderer.material.color = Color.Lerp(originalColor, Color.red, t);
        if (elapsed >= destroyDelay) Break();
    }

    private void Break()
    {
        isBroken = true;

        // 아직 이 블록에 줄이 걸려 있으면 끊는다. 다른 곳으로 옮겨 건 줄은 유지한다.
        if (rope && rope.Attached && rope.AnchorSurface == transform)
            rope.Detach("Fragile anchor broke");

        // 오브젝트 풀에서 재사용하므로 Destroy 대신 렌더러와 충돌체만 끈다.
        meshRenderer.enabled = false;
        if (anchorCollider) anchorCollider.enabled = false;
    }
}
