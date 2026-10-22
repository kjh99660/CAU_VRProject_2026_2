# 시야 제한 비교 시스템

## 조사 결과와 범위

Unity 6000.3.24f1 / URP 17.3.0 / Input System 1.20.0 / XRI 3.5.1 / XR Core Utils 2.6.0 / XR Management 4.6.1 / OpenXR 1.17.1. 추가 XR 패키지는 기존 프로젝트 README와 manifest에 기록되어 있다. 조사 시 AGENTS.md와 미커밋 변경은 없었다. Unity·패키지는 업그레이드하지 않았다.

장비 연결은 확인되지 않아 VIVE Pro PC VR 및 Quest 3 독립 실행을 기존 목표로 가정했다. XR 설정에는 대상별 렌더 모드 0(MultiPass), 1(SinglePassInstanced)이 존재한다. 설치된 OpenXR 소스의 enum을 확인했으며 런타임·플랫폼에 따라 실제 경로는 달라질 수 있다.

```text
Swing XR Origin (CharacterController + SwingMotor, 가상 이동의 유일한 작성자)
  Interpolated Tracking Group (SwingVisualSmoothing이 위치 보간과 인위적 pitch/roll 분리)
    Camera Offset (XROrigin 높이 보정)
      Main Camera (TrackedPoseDriver: 실제 HMD pose)
        TunnelingVignette (XRI 제어기 + 기존 hemisphere mesh)
        Distance HUD (VR 우측 상단; 기존 중앙 World HUD는 제거)
      Left Hand / Right Hand (TrackedPoseDriver)
```

캐릭터 모델, 자동 진행 방향 회전, head bob, 충돌·착지 카메라 흔들림, snap turn은 기존에 없었다. 새로 추가하지 않았다. 모델을 추가할 때는 추적 그룹이 아닌 별도 Model Pivot에 배치한다. `stabilizeArtificialTilt`는 가상 부모 기울기만 제거하고 루트 yaw는 유지하므로 나중에 의도적인 yaw 회전을 추가할 여지를 남긴다. 실제 HMD의 pitch/roll/yaw와 local position은 수정·평활화하지 않는다.

## 변경 구조

| 파일 | 역할 |
|---|---|
| `SwingComfort.cs` | A/B/C 모드, XRI provider, 강도 보간·히스테리시스, 중단·메뉴·수명주기 |
| `ComfortTuning.cs` | 직렬화 Inspector 설정, 유한값·정규화·점수·강도 순수 계산 |
| `SwingPrediction.cs` | 복사 상태의 짧은 궤적, 고정 배열, 제한된 SphereCast, ProfilerMarker |
| `SwingMotionMath.cs` | 실제 motor와 예측이 공유하는 기존 중력·지상/공중 가속·속도 제한 계산 |
| `ComfortTelemetry.cs` | 고정 상한 수치 버퍼와 세션/모드별 CSV |
| `Settings/ComfortVignette.shader`, `.mat` | XRI UV·파라미터를 사용하는 URP 주변 마스크, stereo/instancing 매크로 |
| 기존 motor/rope/course/input/HUD | 정지·리스폰 알림, 상태 revision, 발사 카운터, 모드·중단 입력과 비교 UI |
| `Tests/*/Comfort*Tests.cs` | 수학 경계·스윙 예측 일치·모드·카메라·중단·CSV 검증 |

기존 스윙 로직 전체를 교체하지 않았다. motor의 속도 적분식을 같은 연산 순서의 순수 함수로 옮겼다. 중력 18m/s², 최고 속도 35m/s, 조향, 줄 길이, 보정 예산과 50Hz 물리 주기는 그대로다. 선택 모드가 이 수치에 접근해 수정하지 않는다. 과도한 보정/벽 충돌 시 줄 해제, 재연결의 속도 보존, 근접 연결 거부도 유지한다. 추가 속도 제한은 도입하지 않았다.

## 씬 연결과 사용

현재 기본 씬은 무한 건물 맵이며 자동 완료/체크포인트 없이 낙하·중단 시 시작점으로 돌아간다. 이전 유한 코스는 `Scenes/WebSwingValidation.unity`에 보존했다. A/B/C 비교는 동일한 무한 맵의 같은 출발점·같은 이동 설정에서 시간/도달 거리 기준으로 수행할 수 있다. 원점 이동에서는 추적 pose·속도·비네트 강도를 유지하고 예측 좌표와 미분 기록만 버린다. 자세한 거리 정의와 풀 설정은 프로젝트 README를 참고한다.

현재 테스트 씬은 연결되어 있다. 기존 씬을 갱신하려면 **VR Web Swing > Upgrade Comfort in Test Course**를 실행한다. 이 명시적 메뉴는 테스트 씬만 열어 기존 오브젝트를 유지하고 연결한다. 새 코스 생성 메뉴는 기존 씬을 덮어쓰지 않는다. Editor 코드는 별도 Editor assembly에만 포함된다.

리그의 `SwingComfort`에서 Motor/Input/XRI Vignette 참조를 연결한다. XRI 프리팹의 제어기와 hemisphere mesh는 그대로 재사용한다. 기존 샘플 CG 셰이더에는 명시적인 instancing variant 선언이 없어, 샘플을 수정하는 대신 별도 URP 머티리얼을 연결했다. 새로운 셰이더는 XRI의 주변 마스크 계산과 속성명을 유지하고 `multi_compile_instancing` 및 stereo eye 매크로를 포함한다. projection FOV를 변경하지 않는다. 실제 양안·multiview 작동은 장비별로 검증해야 한다.

| 모드 | 차이 |
|---|---|
| A Baseline | 카메라/물리 기본 안정성만 적용, 비네트 0 |
| B SpeedBased (초기값) | 실제 인위적 속도의 정규화 점수 |
| C Predictive | 현재/예측 최고 속도, 가속도·방향 변화·장애물 접근·줄 보정의 조절 가능한 점수 |

M / 왼손 primaryButton으로 순환한다. P 또는 F1 / 오른손 menuButton으로 이동을 정지하고 비교 메뉴를 연다. 데스크톱 메뉴의 A/B/C 버튼을 선택하고 Pause/resume 또는 P로 재개한다. 메뉴 클릭은 거미줄을 발사하지 않는다. 모드만 바꾸면 현재 이동 상태를 보존하며, 메뉴를 열 때에는 이동을 정지하고 줄을 해제한다. 비교를 시작할 때는 동일 시작점에서 T로 재시작한다.

Backspace / 오른손 secondaryButton은 중단 기록을 남기고 최근 체크포인트의 안전한 위치로 복귀하여 대기한다. 재개 전 사용자의 의사를 확인한다. P로 재개하거나 R/T로 복귀·재시작할 수 있다. `Time.timeScale`은 바꾸지 않아 머리 추적·렌더링·UI가 계속 동작한다. 메뉴·포커스 손실·앱 일시정지도 이동을 정지시킨다. OS/런타임이 점유한 menuButton이나 미지원 secondaryButton은 해당 컨트롤러에서 재매핑한다.

`SetMenuOpen(bool)`는 외부 메뉴 통합용 명시적 진입점이다. 리스폰/정지 시 motor 이벤트로 강도·예측·미분을 즉시 초기화한다. 줄 연결/해제 revision이 바뀌면 예측·미분 기록만 초기화해 오래된 값이 남지 않게 하고, 기존 강도는 부드럽게 이어 불필요한 깜빡임을 줄인다. 모드 전환·씬 종료·비활성화 시 provider 종료와 renderer 숨김을 수행한다. 이벤트는 OnEnable/OnDisable에서 구독·해제한다.

## 주요 파라미터

설정은 `SwingComfort > Tuning`에 직렬화되어 있고 각 필드에 단위 Tooltip이 있다.

| 항목 | 초기값 / 의미 |
|---|---|
| Preview On Desktop | true: 데스크톱 마스크 미리보기. 실제 HMD 검증 아님 |
| Max Strength | 0.7: 정규화 제한 강도 상한 |
| Minimum Aperture | 0.55: 강도 1일 때 투명 중앙 지름. 실제 지름은 1에서 이 값까지 강도로 보간 |
| Feathering | 0.3: 마스크 경계 완화 |
| Enter / Restore Rate | 2 / 1.5 강도 단위/초. 머리 움직임을 보간하는 설정이 아님 |
| Activate / Deactivate Score | 0.08 / 0.03: 시작/종료 임계값 히스테리시스 |
| Speed Range | 6~24m/s: 점수 0~1의 구간 |
| Acceleration Range | 10~45m/s²: 현재 및 예측 속도 변화 |
| Turn Range | 15~90도: 예측 시간 내 방향 변화, 속도 1m/s 미만이면 0 |
| Obstacle Range | 0.5~4m: 가까울수록 높은 접근 점수 |
| Constraint Range | 0.005~0.1m/예측 step: 줄 보정량 지표 |
| Prediction Weights | 가속 0.4, 방향 0.2, 장애물 0.25, 줄 보정 0.15 |
| Predictive Weight | 0.5: 속도 점수의 남은 여유에 예측 지표를 더하는 비율 |
| Horizon / Steps / Hz | 0.3초 / 15 / 10Hz. 범위 0.2~0.5초, 4~32 step, 1~30Hz |
| Obstacle Casts | 갱신당 최대 6회, 설정 범위 0~12회 |
| Sample Hz / Max Samples | 5Hz / 2048개, 최대 8192개로 제한 |

점수는 **시야 제한 제어용 휴리스틱 점수**이며 멀미 확률이나 검증된 안전 기준이 아니다. 모든 예측 임계값은 초기 튜닝값이다. 다음 연결점 탐색이 방해되면 Minimum Aperture를 올리거나 Max Strength/Predictive Weight를 낮춘다. 중앙 십자 조준과 실제 VR 손 조준은 유지한다. 정지 시에는 예측 중력만으로 마스크가 유지되지 않도록 점수를 0으로 하고 복원한다.

## 예측 모델의 가정과 한계

- 현재 root 위치·인위적 속도·접지 상태·고정 연결점/줄 길이를 복사한다. HMD pose의 위치/회전 미분은 입력하지 않는다. 실제 고개 회전만으로 속도 점수를 만들지 않는다.
- 현재 조향 방향을 유지하고 새로운 점프/연결/해제 입력은 없다고 가정한다. head-relative 조향 중 실제 입력 방향 변화까지 미리 알 수는 없다.
- 실제 motor와 같은 속도 적분, 거리 기반 이동 substep, RopeMath 최대 길이·바깥쪽 속도 제거, 보정 예산 초과 시 복사된 줄 해제를 사용한다. 독립적인 이상적 진자 방정식을 도입하지 않았다.
- 기본 15 step/0.3초는 실제 0.02초 물리 step과 같지만 설정을 바꾸면 오차가 생긴다. Grounded 상태는 예측 구간 동안 유지하므로 착지/플랫폼 가장자리 이탈은 예측하지 못한다.
- 2026-10-22 추가한 바닥 기준 자동 감기는 실제 motor와 예측에서 같은 목표 길이·감는 속도·보정 예산 규칙을 사용한다. 예측에서는 벽 때문에 감기를 중단하는 capsule 응답까지 재현하지 못한다. A/B/C 비교에서는 Auto Shorten For Floor 설정도 동일하게 고정하고, 이전 고정 길이 조건과 비교할 때는 모든 모드에서 함께 끈다.
- 장애물은 body 중심의 SphereCast로 근사한다. 실제 높이 1.8m capsule, 모서리 slide/stepOffset, depenetration, 동적 장애물의 미래 위치를 재현하지 않는다. 여러 예측 점 사이를 묶은 cast는 곡선에 오차가 있다. 충돌을 감지하면 그 지점에서 예측을 종료하고 이후 응답은 추측하지 않는다.
- 실제 세계의 `Physics.Simulate`, CharacterController.Move, transform 변경을 예측에서 호출하지 않는다. 코스/거미줄 상태도 바꾸지 않는다.
- 리스폰/정지·연결 변화에서 미분 이력을 버린다. deltaTime 0, 비유한 값, 정지 방향은 방어한다. 위치 보정의 transform 미분 대신 motor 속도를 사용한다.
- 선택된 SwingComfort의 보라색 Gizmo로 예측 선을 확인한다. 실제 HMD tracking을 예측·지연시키지 않는다.

## 측정과 성능

`Application.persistentDataPath/ComfortSessions/` 아래 UTC 식별자·모드·내보내기 번호가 붙은 CSV를 만든다. Windows의 기본 회사/제품 이름에서는 `%USERPROFILE%/AppData/LocalLow/DefaultCompany/My project/ComfortSessions/`이며 Android는 앱별 persistentDataPath이다. 실제 경로는 F8 내보내기 후 데스크톱 메뉴에 표시된다. 파일을 덮어쓰지 않고 모드 전환/재시작/완료/중단/컴포넌트 종료에 세션을 닫는다. 예외는 경고와 LastError로 표시한다.

CSV에는 세션·모드·경과 시간·가상 속도·가속도·예측 방향 변화·휴리스틱 점수·적용 마스크 강도·활성 시간 비율·정지 상태·XR 감지 여부·발사 성공/실패·완료/중단 여부가 있다. 모드 전환은 새로운 세션이며 이후부터의 발사 횟수를 센다. 활성 비율은 세션의 일시정지 포함 경과 시간 중 실제 마스크 강도 >0.01인 시간 비율이다. 루프 프레임 평균/최대 간격은 표본 사이 `unscaledDeltaTime`을 집계한다.

**XR CPU/GPU 시간·실제 dropped frame은 측정하지 않아 해당 셀을 비워둔다.** 루프 간격을 GPU 시간이나 디스플레이 프레임 누락으로 해석하지 않는다. 장비 비교에는 Unity Profiler 및 해당 OpenXR 런타임/장비 도구의 CPU/GPU/프레임 타이밍 측정을 별도로 병행한다.

수치 표본은 고정 상한 배열에 5Hz로 저장하고 상한 이후는 버리며 discarded_samples에 센다. 매 프레임 파일을 쓰지 않는다. F8 및 세션 경계에서만 동기 내보내므로 해당 순간에는 저장 비용이 발생할 수 있다. 성능 비교에서 이 경계를 분리한다. 표본이 없는 세션도 요약 행을 남긴다. 종료 시 이중 내보내기를 방지한다.

예측 점 배열은 33개를 재사용하고 계산 주기·step·cast 수를 제한한다. 예측/강도 업데이트는 프레임별 컬렉션·문자열을 만들지 않는다. HUD 문자열은 기존처럼 10Hz 갱신한다. Profiler의 `VRWebSwing.Comfort.Predict`와 `VRWebSwing.Comfort.Update`로 비용을 확인한다. 실제 장비 FPS나 C 모드의 우월성을 주장하지 않는다.

## 검증과 HMD 비교 절차

자동 테스트는 Test Runner의 EditMode/PlayMode에서 `VRWebSwing.Tests` 필터로 실행한다. 결과 로그는 `Logs/Comfort-Configure.log`, `Comfort-EditMode.xml`, `Comfort-PlayMode.xml`에 있으며 gitignore 대상이다. 수학 테스트는 정규화·정지 방향·히스테리시스·복원·영 dt와 기존 적분식 보존을 확인한다. PlayMode는 기존 스윙 회귀와 예측의 비변경/다음 step 일치, A/B/C 전환, FOV 유지, 인위적 기울기 분리와 local pose 보존, 중단/메뉴/재시작/리스폰 초기화, 이벤트 수명주기 및 CSV 필드를 확인한다.

실제 HMD·Android 빌드/배포·스테레오 셰이더의 장비 variant·주사율 성능·불편감 효과는 자동 테스트가 대체하지 않는다. 다음을 장비에서 확인한다.

1. 같은 장비·주사율·코스·이동 설정을 고정한다. 각 조건 시작 전 T/재시작으로 같은 위치에서 출발하고 모드별 파일을 구분한다. 대상·실행 방식·설정값도 별도로 기록한다.
2. A/B/C 순서를 참가자마다 바꿔 순서/익숙해짐 영향을 줄인다. 충분히 회복한 상태에서만 다음 조건을 시작한다. 정해진 시간 또는 같은 체크포인트 목표로 비교한다.
3. 정지/스윙/관성 비행/재연결/벽 충돌/착지에서 양쪽 눈의 동일한 주변 마스크, 중앙 조준, 경계 feathering과 시야 복원을 확인한다. 고개 숙이기/기울이기/좌우보기는 즉시 추적되고 모델/루트의 pitch·roll은 시선에 강제로 적용되지 않아야 한다.
4. 연결/해제 직후·리스폰·일시정지·앱 포커스 손실/복구·메뉴·중단·씬 전환에서 마스크가 남지 않는지, 추적·UI가 계속 작동하는지 확인한다. 중단은 가까운 안전 플랫폼으로 돌아와 정지해야 한다.
5. 불편감뿐 아니라 연결 성공률, 다음 연결점 탐색의 어려움, 시야 방해, 재미/조작감도 같은 방식으로 기록한다. CSV는 행동/제어 지표이며 주관적 불편감의 대리 확정값이 아니다.
6. **불편감이 생기면 즉시 중단한다. 증상이 남아 있는 동안 다음 조건을 진행하지 않는다.** 이 시스템은 멀미를 진단하거나 완전히 방지하는 기능이 아니다.

이번 범위는 주변 시야에 다른 장면을 렌더링하는 Peripheral Teleportation을 포함하지 않는다. 기존 제자리 capsule의 룸스케일 머리 벽 침투 한계도 그대로 남아 있다.
