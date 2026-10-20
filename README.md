# VR Web Swing 기술 데모

중앙대학교 소규모 VR 기술 시연 프로젝트. 건물에 거미줄을 연결하고 중력·관성을 이용해 **조준 → 연결 → 스윙 → 해제 → 비행 → 재연결**하는 흐름을 검증합니다.

## 현재 진행 사항

- 기본 VR 템플릿 대신 회색 박스 테스트 코스, 안전 플랫폼, 체크포인트 3개, 낙하 복귀·완료·재시작 구현
- 양손 번갈아 단일 거미줄 발사, 줄 길이 제약, 관성 유지, 벽 충돌 및 고속 이동 검사
- HMD 없이 마우스 시점·클릭 발사·WASD로 실행 가능
- HMD 추적과 가상 이동 분리, 인위적 카메라 기울기 억제, 물리 위치 렌더 보간
- 멀미 완화 비교 모드 **A Baseline / B SpeedBased / C Predictive**, 동적 비네트·짧은 궤적 예측·안전 중단 구현
- 모드별 가상 이동·비네트·프레임 간격·완료/중단 CSV 기록

## 실행

1. **Unity 6000.3.24f1**으로 프로젝트를 엽니다. URP 17.3, Input System 1.20, XRI 3.5.1, OpenXR 1.17.1을 사용합니다.
2. `Assets/VRWebSwing/Scenes/WebSwingCourse.unity`를 열고 **Play → Game 뷰 클릭**합니다.
3. 씬이 없으면 **VR Web Swing > Create or Open Test Course**를 실행합니다. 기존 씬의 멀미 완화 연결은 **Upgrade Comfort in Test Course**로 갱신할 수 있습니다.

| 동작 | 데스크톱 |
|---|---|
| 조준 / 이동 / 점프 | 마우스 / WASD / Space |
| 왼손·오른손 거미줄 | 좌·우 클릭으로 연결, 놓으면 해제 |
| 최근 체크포인트 / 전체 재시작 | R / T |
| 마우스 잠금 해제 | Esc; 다시 클릭하면 잠금·발사 |
| A/B/C 순환 / 비교 메뉴·일시정지 | M / P 또는 F1 |
| 즉시 중단·안전 지점 복귀 후 대기 | Backspace |
| CSV 내보내기 | F8 |

VR 기본값은 양손 트리거 발사, 왼손 스틱 이동, 오른손 primaryButton 점프, 왼손 secondaryButton 복귀입니다. 비교 모드는 왼손 primaryButton, 중단은 오른손 secondaryButton, 일시정지는 오른손 menuButton입니다. 장비의 지원 버튼과 OpenXR 프로필에 맞춰 확인해야 합니다.

## 검증과 남은 작업

2026-10-20 동일 버전 Unity에서 컴파일·씬 연결을 확인하고 **EditMode 8/8, PlayMode 2/2** 테스트가 통과했습니다. 기존 스윙·충돌·복귀와 비교 모드·예측·중단·CSV를 검증했습니다. 예측은 근사 계산이며 효과 개선을 입증한 결과는 아닙니다.

실제 HMD의 양안 표시, 컨트롤러 매핑, 멀미 완화 효과와 프레임 성능은 미검증입니다. VIVE Pro PC VR 90FPS 및 Quest 3 지원 설정 120FPS는 향후 측정 목표입니다. 기본 이동 수치와 50Hz 물리 주기는 유지했습니다. 룸스케일 신체 추종·머리 충돌 처리는 후속 작업입니다.

CSV는 `Application.persistentDataPath/ComfortSessions/`에 저장됩니다. 프레임 간격은 게임 루프 측정이며 XR CPU/GPU 시간·디스플레이 누락 프레임으로 해석하면 안 됩니다.

- [구조·생성·VR 설정](Assets/VRWebSwing/README.md)
- [멀미 완화 구조·튜닝·HMD 비교 절차](Assets/VRWebSwing/Comfort.md)

멀미 완화 시스템은 멀미를 진단하거나 방지를 보장하지 않습니다. 비교 중 불편감이 생기면 즉시 중단하고, 증상이 남아 있는 동안 다음 조건을 진행하지 않습니다.
