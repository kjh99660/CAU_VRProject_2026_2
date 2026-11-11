using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("시작 위치를 기준으로 이동할 목표 위치 (상대 좌표)")]
    public Vector3 moveOffset = new Vector3(5f, 0f, 0f);
    
    [Tooltip("왕복 이동 속도")]
    public float speed = 2f;
    
    [Tooltip("끝 지점에 도달했을 때 대기하는 시간(초)")]
    public float waitTime = 0.5f;

    private Vector3 startPos;
    private Vector3 endPos;
    private Vector3 targetPos;
    private float waitTimer;

    void Start()
    {
        // 시작 위치와 끝 위치 초기화
        startPos = transform.position;
        endPos = startPos + moveOffset;
        targetPos = endPos;
    }

    void FixedUpdate()
    {
        // 대기 시간 처리
        if (waitTimer > 0)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }

        // 플랫폼 이동 (Rigidbody가 부착되어 있다면 rb.MovePosition 권장)
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.fixedDeltaTime);

        // 목표 지점 도달 확인 (오차 허용 범위 0.01f)
        if (Vector3.Distance(transform.position, targetPos) < 0.01f)
        {
            // 목표 위치 스왑 (시작점 <-> 끝점)
            targetPos = targetPos == startPos ? endPos : startPos;
            waitTimer = waitTime;
        }
    }

    // 인스펙터 창에서 오브젝트 선택 시 이동 경로를 미리 보여주는 기능
    private void OnDrawGizmosSelected()
    {
        Vector3 start = Application.isPlaying ? startPos : transform.position;
        Vector3 end = Application.isPlaying ? endPos : transform.position + moveOffset;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start, end);
        
        // 시작점과 끝점에 플랫폼 크기만한 가이드 박스 표시
        Gizmos.DrawWireCube(start, transform.localScale);
        Gizmos.DrawWireCube(end, transform.localScale);
    }
}