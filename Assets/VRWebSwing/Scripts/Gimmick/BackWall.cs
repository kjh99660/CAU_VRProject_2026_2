using UnityEngine;
using VRWebSwing;

// 플레이어 뒤에서 +Z(진행 방향)로 계속 따라오는 벽. 플레이어에게 닿으면 게임오버.
// EndlessCity가 랜덤 기믹 구간 진입 시 Activate()로 배치·시작한다. 프리팹 루트에 붙인다.
public class BackWall : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("벽이 앞으로 따라오는 속도(m/s). 플레이어 최대 속도는 35m/s.")]
    [Min(0)] public float speed = 8f;

    [Header("Hit")]
    [Tooltip("벽 앞면이 루트에서 +Z 방향으로 얼마나 앞에 있는지(m). 벽 메시 두께의 절반 정도로 맞춘다.")]
    [Min(0)] public float frontOffset = 0.5f;
    [Tooltip("사망 이벤트에 전달할 원인 이름")]
    public string deathCause = "BackWall";

    public bool Running { get; private set; }
    public float FrontZ => transform.position.z + frontOffset;

    private SwingMotor motor;

    public void Activate(SwingMotor player, Vector3 position)
    {
        motor = player;
        transform.position = position;
        gameObject.SetActive(true);
        Running = true;
    }

    public void Deactivate()
    {
        Running = false;
        gameObject.SetActive(false);
    }

    void FixedUpdate()
    {
        if (!Running || !motor) return;
        // 메뉴/중단 일시정지 중에는 벽도 멈춘다.
        if (motor.Paused) return;

        transform.position += Vector3.forward * (speed * Time.fixedDeltaTime);

        // 얇은 충돌체 대신 진행축 위치로 판정한다: 고속 이동·통과 누락 없이 "벽 앞면이 몸 뒤쪽에 닿았는가"만 본다.
        float playerBack = motor.transform.position.z + motor.BodyCenter.z - motor.BodyRadius;
        if (FrontZ >= playerBack) HitPlayer();
    }

    private void HitPlayer()
    {
        Running = false;
        GameEvents.OnPlayerDeath?.Invoke(deathCause);
        // 낙사·레이저와 같은 처리: 이번 기록을 종료하고 시작점에서 다시 시작한다(벽은 EndlessCity가 정리).
        motor.course.Respawn();
    }
}
