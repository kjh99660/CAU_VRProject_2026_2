using System.Collections.Generic;
using UnityEngine;
using VRWebSwing;

public class LaserGenerator : MonoBehaviour
{
    [Header("Generator Settings")]
    [Tooltip("높이 범위 안에 만들 레이저 개수")]
    [Range(1, 10)] public int laserCount = 1;
    [Tooltip("레이저가 가로지르는 폭(m). 통로 폭(건물 사이 12m)에 맞춘다.")]
    public float width = 10f;
    public float laserThickness = 0.15f;

    [Header("Height Range (기믹 루트 기준 Y, m)")]
    [Tooltip("낙하 판정 높이를 읽어 올 설정 에셋. 지정하면 하한 = failHeight + Fail Height Margin")]
    public SwingSettings settings;
    [Tooltip("낙하 판정 높이(failHeight)보다 이만큼 위를 레이저 최저 높이로 쓴다(m). 아래로 피하려면 낙사 직전까지 내려가야 한다.")]
    [Min(0)] public float failHeightMargin = 4f;
    [Tooltip("settings가 없을 때 쓰는 레이저 최저 높이(m)")]
    public float minHeight = -9f;
    [Tooltip("레이저 최고 높이(m). 머리 위 연결 타깃(Y=22)보다 낮게 둬야 스윙 경로를 가로막는다.")]
    public float maxHeight = 16f;

    [Header("Pass Gap (레이저 사이 최소 통과 공간)")]
    [Tooltip("플레이어 CharacterController 높이(m). 레이저 사이로 서 있는 자세 그대로 지나가야 한다.")]
    [Min(0.1f)] public float playerHeight = 1.8f;
    [Tooltip("플레이어 CharacterController 반지름(m). 너비 = 반지름 × 2")]
    [Min(0.05f)] public float playerRadius = 0.3f;
    [Tooltip("몸 높이 위에 더할 여유 = 플레이어 너비 × 이 배수. 기본 3배(0.6m × 3 = 1.8m 여유).")]
    [Range(0f, 6f)] public float clearanceRatio = 3f;

    // 레이저 두께를 뺀 순수 빈 공간 = 몸 높이 + 너비 × 배수
    public float ClearGap => playerHeight + playerRadius * 2f * clearanceRatio;
    // 레이저 중심선 사이 간격
    public float CenterSpacing => ClearGap + laserThickness;

    [Header("Laser Visuals")]
    [Tooltip("가운데 얇게 들어갈 하얀색 불투명 머티리얼")]
    public Material coreMaterial;
    [Tooltip("겉을 감쌀 두꺼운 붉은색 반투명(Transparent) 머티리얼")]
    public Material glowMaterial;

    public float LowestHeight => settings ? settings.failHeight + failHeightMargin : minHeight;

    // 레이저 한 줄을 이루는 오브젝트 묶음
    private sealed class Laser
    {
        public Transform root;
        public BoxCollider collider;
        public LineRenderer glow;
        public LineRenderer core;
    }

    private readonly List<Laser> lasers = new List<Laser>();
    private float[] leftHeights = new float[0];
    private float[] rightHeights = new float[0];
    private bool warnedCount;

    // 높이 범위에 간격을 지키며 들어갈 수 있는 최대 레이저 수
    public int MaxFittingCount
    {
        get
        {
            float range = Mathf.Max(0f, maxHeight - LowestHeight);
            return 1 + Mathf.FloorToInt(range / Mathf.Max(0.01f, CenterSpacing));
        }
    }

    void Awake()
    {
        EnsureLasers(laserCount);
    }

    // 무한 맵 풀에서 다시 나타날 때마다 새 높이로 배치한다.
    void OnEnable()
    {
        int count = Mathf.Min(laserCount, MaxFittingCount);
        if (count < laserCount && !warnedCount)
        {
            warnedCount = true;
            Debug.LogWarning($"{name}: 높이 범위 {LowestHeight:0.#}~{maxHeight:0.#}m에 간격 {CenterSpacing:0.##}m로는 레이저 {count}개까지만 들어갑니다 (설정 {laserCount}개).", this);
        }
        EnsureLasers(count);
        SampleSpacedHeights(count, ref leftHeights);
        SampleSpacedHeights(count, ref rightHeights);
        for (int i = 0; i < lasers.Count; ++i)
        {
            bool used = i < count;
            lasers[i].root.gameObject.SetActive(used);
            if (used) PlaceLaser(lasers[i], leftHeights[i], rightHeights[i]);
        }
    }

    // 범위 안에서 서로 CenterSpacing 이상 떨어진 높이 count개를 아래부터 오름차순으로 뽑는다.
    // 남는 여유 공간만 무작위로 나눠 주므로 간격은 항상 보장된다.
    private void SampleSpacedHeights(int count, ref float[] heights)
    {
        if (heights.Length < count) heights = new float[count];
        float low = LowestHeight;
        float slack = Mathf.Max(0f, maxHeight - low - (count - 1) * CenterSpacing);
        for (int i = 0; i < count; ++i) heights[i] = Random.Range(0f, slack);
        System.Array.Sort(heights, 0, count);
        for (int i = 0; i < count; ++i) heights[i] += low + i * CenterSpacing;
    }

    // 부족한 만큼만 새로 만들고, 남는 레이저는 OnEnable에서 꺼 둔다.
    private void EnsureLasers(int count)
    {
        while (lasers.Count < count) lasers.Add(CreateLaser(lasers.Count));
    }

    private Laser CreateLaser(int index)
    {
        var laser = new Laser();

        // 1. 레이저 부모 및 충돌체 설정
        laser.root = new GameObject("LaserLine " + (index + 1)).transform;
        laser.root.SetParent(transform, false);
        laser.collider = laser.root.gameObject.AddComponent<BoxCollider>();
        laser.collider.isTrigger = true;
        laser.root.gameObject.AddComponent<LaserTrigger>();

        // 2. 외곽 빛번짐 (Glow) 그리기
        GameObject glowObj = new GameObject("LaserGlow");
        glowObj.transform.SetParent(laser.root, false);
        laser.glow = glowObj.AddComponent<LineRenderer>();
        SetupLineRenderer(laser.glow, laserThickness * 2.5f, glowMaterial);

        // 3. 중심 심지 (Core) 그리기
        GameObject coreObj = new GameObject("LaserCore");
        coreObj.transform.SetParent(laser.root, false);
        laser.core = coreObj.AddComponent<LineRenderer>();
        SetupLineRenderer(laser.core, laserThickness * 0.4f, coreMaterial);

        return laser;
    }

    // 왼쪽/오른쪽 끝 높이 모두 같은 순서로 정렬돼 있으므로 레이저끼리 교차하지 않고,
    // 직선이라 폭 전체에서 위아래 레이저 간격이 CenterSpacing 이상으로 유지된다.
    private void PlaceLaser(Laser laser, float leftY, float rightY)
    {
        // 높이는 기믹 루트(부모) 기준 Y다. 생성기 자신의 Y 위치는 쓰지 않는다.
        Transform space = transform.parent ? transform.parent : transform;
        Vector3 local = space == transform ? Vector3.zero : transform.localPosition;
        Vector3 startPoint = space.TransformPoint(new Vector3(local.x - width / 2f, leftY, local.z));
        Vector3 endPoint = space.TransformPoint(new Vector3(local.x + width / 2f, rightY, local.z));

        float distance = Vector3.Distance(startPoint, endPoint);
        laser.root.position = (startPoint + endPoint) / 2f;
        laser.root.rotation = Quaternion.FromToRotation(Vector3.right, endPoint - startPoint);
        laser.collider.size = new Vector3(distance, laserThickness, laserThickness);

        // 선은 레이저 부모 기준 로컬 좌표(X축 방향)로 그린다.
        Vector3 localStart = new Vector3(-distance / 2f, 0, 0);
        Vector3 localEnd = new Vector3(distance / 2f, 0, 0);
        laser.glow.SetPosition(0, localStart); laser.glow.SetPosition(1, localEnd);
        laser.core.SetPosition(0, localStart); laser.core.SetPosition(1, localEnd);
    }

    // LineRenderer 중복 세팅 코드를 분리하여 깔끔하게 정리
    private void SetupLineRenderer(LineRenderer line, float width, Material mat)
    {
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.material = mat;
        // 무한 맵 풀링/원점 이동 때 부모와 함께 움직이도록 로컬 좌표를 사용한다.
        line.useWorldSpace = false;
    }

    // 씬 뷰에 항상 표시: 레이저가 나올 수 있는 높이 범위를 붉게 칠하고, 설정 개수를 최소 간격으로 고르게 놓은 예시 선을 그린다.
    // 실제 높이는 Play 때마다 이 범위 안에서 무작위(간격 보장)로 정해진다.
    private void OnDrawGizmos()
    {
        Transform space = transform.parent ? transform.parent : transform;
        Vector3 local = space == transform ? Vector3.zero : transform.localPosition;
        float low = LowestHeight, high = Mathf.Max(low, maxHeight);
        Gizmos.matrix = space.localToWorldMatrix;
        Vector3 center = new Vector3(local.x, (low + high) / 2f, local.z);
        Vector3 size = new Vector3(width, high - low, 0.2f);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.18f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(center, size);

        // 예시 배치: 남는 여유를 위아래로 균등하게 나눈 높이. 선 사이 빈칸이 플레이어가 통과할 최소 공간이다.
        int count = Mathf.Min(laserCount, MaxFittingCount);
        float slack = Mathf.Max(0f, high - low - (count - 1) * CenterSpacing);
        float first = low + slack / (count + 1);
        for (int i = 0; i < count; ++i)
        {
            float y = first + i * (CenterSpacing + slack / (count + 1));
            Gizmos.color = new Color(1f, 0.35f, 0.35f, 1f);
            Gizmos.DrawCube(new Vector3(local.x, y, local.z), new Vector3(width, Mathf.Max(0.05f, laserThickness), 0.3f));
        }
        Gizmos.matrix = Matrix4x4.identity;
#if UNITY_EDITOR
        UnityEditor.Handles.Label(space.TransformPoint(new Vector3(local.x, high + 1f, local.z)),
            $"Laser x{count}  {low:0.#}~{high:0.#}m  gap {ClearGap:0.##}m" + (count < laserCount ? $"  (설정 {laserCount}개 중 {count}개만 들어감)" : ""));
#endif
    }
}
