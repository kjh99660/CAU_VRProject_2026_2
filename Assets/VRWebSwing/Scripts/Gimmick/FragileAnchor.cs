using System.Collections;
using UnityEngine;

public class FragileAnchor : MonoBehaviour
{
    [Header("Fragile Settings")]
    [Tooltip("로프가 부착된 후 완전히 파괴되기까지 걸리는 시간(초)")]
    public float destroyDelay = 1.5f;

    private MeshRenderer meshRenderer;
    private Color originalColor;

    // 중복 실행을 막기 위한 상태 플래그
    private bool isTriggered = false;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        originalColor = meshRenderer.material.color;
    }


    // ==========================================
    // 방법 1: 외부(플레이어/로프 매니저)에서 Raycast 등으로 타겟을 잡고 직접 호출할 때 (추천)
    // ==========================================
    public void OnRopeAttached()
    {
        if (!isTriggered)
        {
            isTriggered = true;
            StartCoroutine(BreakAnchorRoutine());
        }
    }

    // ==========================================
    // 방법 2: 물리적 트리거로 감지할 때 (갈고리 발사체 오브젝트가 직접 날아와 닿을 때)
    // ==========================================
    private void OnTriggerEnter(Collider other)
    {
        // TODO: 태그나 레이어를 통해 플레이어의 로프/갈고리가 맞는지 확인하는 조건문 필요
        if (other.CompareTag("PlayerHook") && !isTriggered)
        {
            isTriggered = true;
            StartCoroutine(BreakAnchorRoutine());
        }
    }

    // 파괴 타이머 코루틴
    private IEnumerator BreakAnchorRoutine()
    {
        float elapsed = 0f;
        while (elapsed < destroyDelay)
        {
            elapsed += Time.deltaTime;
            meshRenderer.material.color = Color.Lerp(originalColor, Color.red, elapsed / destroyDelay);
            yield return null;
        }
        yield return new WaitForSeconds(destroyDelay);

        // TODO: (필수) 플레이어 측의 로프 연결을 강제로 해제하는 로직 호출

        // 발판 파괴 (오브젝트 풀링을 사용한다면 gameObject.SetActive(false)로 변경)
        Destroy(gameObject);
    }
}