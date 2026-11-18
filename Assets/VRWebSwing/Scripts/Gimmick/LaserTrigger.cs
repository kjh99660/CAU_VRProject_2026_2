using System;
using UnityEngine;

// 글로벌 이벤트 매니저 (또는 기존에 구축해둔 이벤트 파이프라인 클래스에 추가)
public static class GameEvents
{
    // 플레이어 사망 이벤트 정의
    public static Action<string> OnPlayerDeath;
}

// 레이저 충돌체 표식. 플레이어 판정은 SwingMotor가 이동 경로 전체를 훑어서 한다
// (얇은 트리거는 고속 스윙 중 한 물리 프레임에 통과해 OnTriggerEnter가 누락될 수 있음).
public class LaserTrigger : MonoBehaviour
{
    [Tooltip("사망 이벤트에 전달할 원인 이름")]
    public string deathCause = "LaserObstacle";

    // SwingMotor가 플레이어와 닿았다고 판정하면 호출한다. 이후 시작점 복귀는 SwingMotor가 처리한다.
    public void OnPlayerHit()
    {
        GameEvents.OnPlayerDeath?.Invoke(deathCause);
    }
}
