using System.Collections;
using UnityEngine;

public class ToggleWall : MonoBehaviour
{
    [Header("Toggle Settings")]
    [Tooltip("다른 벽들과 타이밍을 엇갈리게 하기 위한 초기 대기 시간")]
    public float timeOffset = 0f;
    
    [Tooltip("벽이 켜져 있는 시간")]
    public float activeDuration = 2.0f;
    
    [Tooltip("벽이 꺼져 있는 시간")]
    public float inactiveDuration = 2.0f;

    [Header("Target Object")]
    [Tooltip("실제로 껐다 켤 자식 오브젝트 (MeshRenderer와 Collider가 있는 오브젝트)")]
    public GameObject targetWall;

    void Start()
    {
        if (targetWall != null)
        {
            StartCoroutine(ToggleRoutine());
        }
        else
        {
            Debug.LogError("타겟 벽 오브젝트가 할당되지 않았습니다!");
        }
    }

    private IEnumerator ToggleRoutine()
    {
        // 1. Time Offset 만큼 초기에 대기 (이때 벽의 초기 상태는 인스펙터에 설정된 상태를 유지)
        if (timeOffset > 0f)
        {
            yield return new WaitForSeconds(timeOffset);
        }

        // 2. 주기적으로 껐다 켜기 무한 반복
        while (true)
        {
            // 벽 켜기
            targetWall.SetActive(true);
            yield return new WaitForSeconds(activeDuration);

            // 벽 끄기
            targetWall.SetActive(false);
            yield return new WaitForSeconds(inactiveDuration);
        }
    }
}