using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("시작 위치를 기준으로 이동할 목표 위치 (부모 기준 상대 좌표)")]
    public Vector3 moveOffset = new Vector3(5f, 0f, 0f);

    [Tooltip("왕복 이동 속도")]
    public float speed = 2f;

    [Tooltip("끝 지점에 도달했을 때 대기하는 시간(초)")]
    public float waitTime = 0.5f;

    // 무한 맵 풀링/원점 이동 시 부모가 통째로 옮겨지므로 부모 기준 로컬 좌표를 사용한다.
    private Vector3 startPos;
    private Vector3 endPos;
    private Vector3 targetPos;
    private float waitTimer;

    void Awake()
    {
        // 시작 위치와 끝 위치 초기화
        startPos = transform.localPosition;
        endPos = startPos + moveOffset;
    }

    void OnEnable()
    {
        // 풀에서 재사용될 때마다 처음 상태로 되돌린다.
        transform.localPosition = startPos;
        targetPos = endPos;
        waitTimer = 0f;
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
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetPos, speed * Time.fixedDeltaTime);

        // 목표 지점 도달 확인 (오차 허용 범위 0.01f)
        if (Vector3.Distance(transform.localPosition, targetPos) < 0.01f)
        {
            // 목표 위치 스왑 (시작점 <-> 끝점)
            targetPos = targetPos == startPos ? endPos : startPos;
            waitTimer = waitTime;
        }
    }

    // 씬 뷰에 항상 표시: 발판의 시작·끝 위치(실제 메시 크기)와 왕복하며 지나가는 전체 범위를 칠해서 보여 준다.
    private void OnDrawGizmos()
    {
        Vector3 localStart = Application.isPlaying ? startPos : transform.localPosition;
        Vector3 localEnd = localStart + moveOffset;
        Matrix4x4 parentMatrix = transform.parent ? transform.parent.localToWorldMatrix : Matrix4x4.identity;

        // 발판 크기: 메시 경계(없으면 단위 큐브)를 발판 회전·스케일로 그린다.
        var filter = GetComponent<MeshFilter>();
        Bounds mesh = filter && filter.sharedMesh ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        Matrix4x4 startMatrix = parentMatrix * Matrix4x4.TRS(localStart, transform.localRotation, transform.localScale);
        Matrix4x4 endMatrix = parentMatrix * Matrix4x4.TRS(localEnd, transform.localRotation, transform.localScale);

        // 이동 범위: 시작~끝 사이를 발판이 쓸고 지나가는 영역(부모 기준 축 정렬 근사)
        Bounds sweep = LocalBounds(startMatrix, mesh, parentMatrix.inverse);
        sweep.Encapsulate(LocalBounds(endMatrix, mesh, parentMatrix.inverse));
        Gizmos.matrix = parentMatrix;
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.08f);
        Gizmos.DrawCube(sweep.center, sweep.size);
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.5f);
        Gizmos.DrawWireCube(sweep.center, sweep.size);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(localStart, localEnd);

        // 시작 위치는 진하게, 끝 위치는 옅게 칠한다.
        Gizmos.matrix = startMatrix;
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.45f);
        Gizmos.DrawCube(mesh.center, mesh.size);
        Gizmos.matrix = endMatrix;
        Gizmos.color = new Color(0f, 0.9f, 1f, 0.25f);
        Gizmos.DrawCube(mesh.center, mesh.size);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(mesh.center, mesh.size);
        Gizmos.matrix = Matrix4x4.identity;
    }

    // 메시 경계의 8개 꼭짓점을 부모 공간으로 옮겨 축 정렬 경계를 만든다.
    private static Bounds LocalBounds(Matrix4x4 toWorld, Bounds mesh, Matrix4x4 worldToParent)
    {
        Matrix4x4 m = worldToParent * toWorld;
        Bounds result = new Bounds(m.MultiplyPoint3x4(mesh.min), Vector3.zero);
        for (int i = 1; i < 8; ++i)
        {
            Vector3 corner = new Vector3((i & 1) != 0 ? mesh.max.x : mesh.min.x, (i & 2) != 0 ? mesh.max.y : mesh.min.y, (i & 4) != 0 ? mesh.max.z : mesh.min.z);
            result.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return result;
    }
}
