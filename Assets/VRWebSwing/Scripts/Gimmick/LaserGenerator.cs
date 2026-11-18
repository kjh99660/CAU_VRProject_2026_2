using UnityEngine;

public class LaserGenerator : MonoBehaviour
{
    [Header("Generator Settings")]
    public float width = 10f;
    public float height = 8f;
    public float laserThickness = 0.15f;

    [Header("Laser Visuals")]
    [Tooltip("가운데 얇게 들어갈 하얀색 불투명 머티리얼")]
    public Material coreMaterial; 
    [Tooltip("겉을 감쌀 두꺼운 붉은색 반투명(Transparent) 머티리얼")]
    public Material glowMaterial;

    void Start()
    {
        GenerateSingleLaser();
    }

    private void GenerateSingleLaser()
    {
        float leftY = Random.Range(-height / 2f, height / 2f);
        float rightY = Random.Range(-height / 2f, height / 2f);

        Vector3 startPoint = transform.position + new Vector3(-width / 2f, leftY, 0);
        Vector3 endPoint = transform.position + new Vector3(width / 2f, rightY, 0);

        // 1. 레이저 부모 및 충돌체 설정
        GameObject laserObj = new GameObject("LaserLine");
        laserObj.transform.SetParent(transform);

        BoxCollider col = laserObj.AddComponent<BoxCollider>();
        col.isTrigger = true;
        float distance = Vector3.Distance(startPoint, endPoint);
        col.size = new Vector3(distance, laserThickness, laserThickness);
        laserObj.transform.position = (startPoint + endPoint) / 2f;
        laserObj.transform.rotation = Quaternion.FromToRotation(Vector3.right, endPoint - startPoint);
        laserObj.AddComponent<LaserTrigger>();

        // 선은 laserObj 기준 로컬 좌표(X축 방향)로 그린다.
        Vector3 localStart = new Vector3(-distance / 2f, 0, 0);
        Vector3 localEnd = new Vector3(distance / 2f, 0, 0);

        // 2. 외곽 빛번짐 (Glow) 그리기
        GameObject glowObj = new GameObject("LaserGlow");
        glowObj.transform.SetParent(laserObj.transform, false);
        LineRenderer glowLine = glowObj.AddComponent<LineRenderer>();
        SetupLineRenderer(glowLine, localStart, localEnd, laserThickness * 2.5f, glowMaterial);

        // 3. 중심 심지 (Core) 그리기
        GameObject coreObj = new GameObject("LaserCore");
        coreObj.transform.SetParent(laserObj.transform, false);
        LineRenderer coreLine = coreObj.AddComponent<LineRenderer>();
        SetupLineRenderer(coreLine, localStart, localEnd, laserThickness * 0.4f, coreMaterial);
    }

    // LineRenderer 중복 세팅 코드를 분리하여 깔끔하게 정리
    private void SetupLineRenderer(LineRenderer line, Vector3 start, Vector3 end, float width, Material mat)
    {
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = width;
        line.endWidth = width;
        line.material = mat;
        // 무한 맵 풀링/원점 이동 때 부모와 함께 움직이도록 로컬 좌표를 사용한다.
        line.useWorldSpace = false;
    }
}