# VR Web Swing 기술 데모

중앙대학교 소규모 VR 기술 시연 프로젝트. 건물에 거미줄을 연결하고 중력·관성을 이용해 **조준 → 연결 → 스윙 → 해제 → 비행 → 재연결**하는 흐름을 검증합니다.

## 현재 진행 사항

- 시작 플랫폼만 남긴 공중 무한 맵: 동일 건물 구간을 고정 풀로 반복 재배치
- 화면 우측 상단에 최대 전진 거리·플레이 세션 최고 기록·직전 기록 표시, 낙하 시 시작점 복귀
- 양손 번갈아 단일 거미줄 발사, 줄 길이 제약, 관성 유지, 벽 충돌 및 고속 이동 검사
- 연결 시 바닥 높이와 스윙 원의 최저점을 한 번 계산해 목표 줄 길이를 정하고 점진적으로 감기
- HMD 없이 마우스 시점·클릭 발사·WASD로 실행 가능
- HMD 추적과 가상 이동 분리, 인위적 카메라 기울기 억제, 물리 위치 렌더 보간
- 멀미 완화 비교 모드 **A Baseline / B SpeedBased / C Predictive**, 동적 비네트·짧은 궤적 예측·안전 중단 구현
- 모드별 가상 이동·비네트·프레임 간격·완료/중단 CSV 기록

## 실행

1. **Unity 6000.3.24f1**으로 프로젝트를 엽니다. URP 17.3, Input System 1.20, XRI 3.5.1, OpenXR 1.17.1을 사용합니다.
2. `Assets/VRWebSwing/Scenes/WebSwingCourse.unity`를 열고 **Play → Game 뷰 클릭**합니다.
3. 씬이 없으면 **VR Web Swing > Create or Open Test Course**를 실행합니다. 기존 씬의 멀미 완화 연결은 **Upgrade Comfort in Test Course**로 갱신할 수 있습니다.
4. 이전 유한 코스를 변환하려면 **VR Web Swing > Upgrade to Endless City**를 실행합니다. 현재 저장된 기본 씬에는 이미 적용되어 있습니다.

| 동작 | 데스크톱 |
|---|---|
| 조준 / 이동 / 점프 | 마우스 / WASD / Space |
| 왼손·오른손 거미줄 | 좌·우 클릭으로 연결, 놓으면 해제 |
| 시작점 복귀 / 전체 재시작 | R / T |
| 마우스 잠금 해제 | Esc; 다시 클릭하면 잠금·발사 |
| A/B/C 순환 / 비교 메뉴·일시정지 | M / P 또는 F1 |
| 즉시 중단·안전 지점 복귀 후 대기 | Backspace |
| CSV 내보내기 | F8 |

VR 기본값은 양손 트리거 발사, 왼손 스틱 이동, 오른손 primaryButton 점프, 왼손 secondaryButton 복귀입니다. 비교 모드는 왼손 primaryButton, 중단은 오른손 secondaryButton, 일시정지는 오른손 menuButton입니다. 장비의 지원 버튼과 OpenXR 프로필에 맞춰 확인해야 합니다.

## 무한 맵과 거리

출발점에서 **+Z 방향으로 도달한 최대 거리(m)**를 측정합니다. 위아래 이동이나 같은 구간 왕복으로 기록이 부풀지 않습니다. `DISTANCE`는 이번 시도, `BEST`는 현재 Play 세션 최고, `LAST`는 직전 시도 기록입니다. 낙하·R·T는 이번 거리를 초기화하고 시작점으로 복귀하며 BEST/LAST는 유지합니다. 게임을 종료하면 기록은 초기화됩니다. 데스크톱에서는 화면 우측 상단, VR에서는 머리 추적 카메라의 우측 상단 월드 HUD로 표시합니다.

`Prefabs/EndlessCitySegment.prefab`의 좌우 건물과 머리 위 연결 타깃을 24m 간격으로 반복합니다. 12개 구간 중 11개를 주변에 배치하고 1개는 연결된 구간 보호용 예비로 사용합니다. 주행 중 Instantiate/Destroy 없이 재사용하고 512m 부근에서 원점을 옮겨 좌표 오차를 줄입니다. 체크포인트·중간 착지 바닥·완료 지점은 없으며 Y < -13m에서 복귀합니다(기존 -8m보다 5m 낮춤). 건물의 벽·옥상 충돌은 유지합니다.

화면 중앙의 안내·디버그 텍스트와 상시 디버그 패널은 제거했습니다. 거리 UI·조준선과 P/F1로 여는 메뉴는 유지합니다. 데스크톱 거리 표시는 화면 UI만 사용해 VR 월드 텍스트와 중복되지 않습니다. 줄 최대 길이는 `DefaultSwingSettings.asset > Max Rope Length`에서 조절하며 기본 40m입니다. 신체 기준 연결 거리가 이 값을 초과하면 기존 줄을 유지하고 발사를 거부합니다. Raycast 범위는 별도 `Max Attach Distance` 설정도 적용됩니다. 연결 중 최대값 변경은 다음 발사부터 적용하며 현재 줄을 갑자기 줄이지 않습니다.

기존 체크포인트 코스는 `Assets/VRWebSwing/Scenes/WebSwingValidation.unity`에 회귀 검증용으로 보존되어 있고, 기본 실행 씬은 계속 `WebSwingCourse.unity`입니다.

자동 줄 감소는 `Assets/VRWebSwing/Settings/DefaultSwingSettings.asset`의 **One-shot floor clearance reel**에서 조절합니다. 기본 안전 여유는 0.5m, 감는 속도는 3m/s입니다. 연결 때만 바닥을 최대 세 번 검사하고, 바닥을 찾지 못하면 길이를 유지합니다. 따라서 바닥이 없는 공중 구간에서는 기존 길이로 스윙합니다. **Auto Shorten For Floor**를 끄면 고정 길이 방식으로 비교할 수 있습니다. 감기가 벽에 막히면 중단하며 충돌 회피를 보장하는 기능은 아닙니다.

## 검증과 남은 작업

2026-10-22 동일 버전 Unity에서 컴파일을 확인하고 **EditMode 10/10, PlayMode 4/4** 테스트가 통과했습니다. 기존 스윙·충돌·복귀·비네트·줄 감소 회귀와 무한 맵의 거리/기록 초기화, 연결 구간 보호, 좌표 원점 이동, 낙하 복귀를 검증했습니다. 200구간(4,800m) 재배치 테스트에서도 구간 인스턴스 수가 유지됐습니다. 이 테스트는 실제 주행이나 HMD 성능 측정이 아닙니다. 예측은 근사 계산이며 효과 개선을 입증한 결과는 아닙니다.

이후 중앙 UI 제거·줄 최대 길이·낙하 높이 변경은 Unity 생성 response와 설치된 동일 버전 Roslyn으로 런타임/Editor/PlayMode 테스트 assembly 컴파일을 확인했습니다. 프로젝트가 Editor에 열려 있어 해당 변경 이후 PlayMode 재실행은 미수행입니다.

실제 HMD의 양안 표시, 컨트롤러 매핑, 멀미 완화 효과와 프레임 성능은 미검증입니다. VIVE Pro PC VR 90FPS 및 Quest 3 지원 설정 120FPS는 향후 측정 목표입니다. 기본 이동 수치와 50Hz 물리 주기는 유지했습니다. 룸스케일 신체 추종·머리 충돌 처리는 후속 작업입니다.

CSV는 `Application.persistentDataPath/ComfortSessions/`에 저장됩니다. 프레임 간격은 게임 루프 측정이며 XR CPU/GPU 시간·디스플레이 누락 프레임으로 해석하면 안 됩니다.

- [구조·생성·VR 설정](Assets/VRWebSwing/README.md)
- [멀미 완화 구조·튜닝·HMD 비교 절차](Assets/VRWebSwing/Comfort.md)

멀미 완화 시스템은 멀미를 진단하거나 방지를 보장하지 않습니다. 비교 중 불편감이 생기면 즉시 중단하고, 증상이 남아 있는 동안 다음 조건을 진행하지 않습니다.
