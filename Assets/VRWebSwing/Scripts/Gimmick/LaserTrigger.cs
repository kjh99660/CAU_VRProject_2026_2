using System;
using UnityEngine;

// 글로벌 이벤트 매니저 (또는 기존에 구축해둔 이벤트 파이프라인 클래스에 추가)
public static class GameEvents
{
    // 플레이어 사망 이벤트 정의
    public static Action<string> OnPlayerDeath; 
}

public class LaserTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 플레이어 태그 확인 (플레이어 오브젝트에 "Player" 태그 필수)
        if (other.CompareTag("Player"))
        {
            // 플레이어 사망 이벤트 발생 (원인 전달)
            GameEvents.OnPlayerDeath?.Invoke("LaserObstacle");
        }
    }
}