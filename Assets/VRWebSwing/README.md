# VR Web Swing 기술 데모

## 프로젝트 확인과 변경 범위

- Unity **6000.3.24f1**, URP **17.3.0**, Input System **1.20.0** (`activeInputHandler = 1`).
- XRI **3.5.1**, XR Core Utils **2.6.0**, XR Management **4.6.1**, OpenXR **1.17.1**, XR Hands **1.8.1**, Meta OpenXR **2.5.1**, Android XR OpenXR **1.3.1**, AR Foundation **6.5.0**, Composition Layers **2.5.0**가 기존 설치되어 있다. 버전/manifest/lock 파일은 변경하지 않았다.
- 프로젝트 내 AGENTS.md 및 별도 작업 규칙은 발견되지 않았다. 기존 `Assets/Scenes/SampleScene.unity`, `BasicScene.unity` 및 Starter Assets의 `XRI Default Input Actions.inputactions`를 확인했다. 템플릿 입력은 많은 상호작용/이동 바인딩을 포함하므로 새 리그에는 스윙용으로 한정한 InputAction을 사용한다. XRI의 설치된 비네트 프리팹은 재사용한다.
- 기존 씬 파일은 원본 복구용으로 보존한다. 실행 씬은 **빈 씬에서 생성한 WebSwingCourse**로 교체하고 SampleScene 빌드 항목은 비활성화했다. 기존 템플릿의 오브젝트나 이동 Provider는 새 씬에 포함되지 않는다.
- 작업 시작 전에 변경되어 있던 `Assets/XR/Settings/OpenXRPackageSettings.asset`는 수정하지 않았다.

## 생성·실행

1. 같은 Unity 버전으로 프로젝트를 연다.
2. `Assets/VRWebSwing/Scenes/WebSwingCourse.unity`를 열고 Play한다.
3. 씬이 없으면 메뉴 **VR Web Swing > Create or Open Test Course**를 실행한다. 기존 작업 중인 씬의 저장 여부를 묻고, 코스가 이미 있으면 그대로 연다. 기존 코스·설정·프리팹을 덮어쓰거나 중복 생성하지 않는다.
4. 배치 생성 진입점은 `-executeMethod VRWebSwing.Editor.SwingSceneBuilder.Generate`이다. 이미 있는 씬은 생성 작업을 건너뛴다.

회색 시작 플랫폼 → 머리 위의 넓은 밝은 회색 타깃 → 금색 CP 1/2/3 순서다. 각 구간에는 착지 플랫폼과 좌우 건물이 있다. 시작 플랫폼 가장자리로 움직이며 위쪽 타깃에 연결하고 점프하여 진자를 만든다. 해제 후 다음 타깃에 다시 연결한다. CP 3 통과 시 속도와 줄을 정지하고 완료 표시를 띄운다. Y < -8m이면 최근 CP의 안전한 플랫폼으로 복귀한다. 씬의 Fall Volume은 이 높이를 시각적으로 확인하는 편집용 trigger이며, 실제 실패 판정은 motor의 높이 검사로 처리한다.

## 조작

| 기능 | 데스크톱 | VR 기본값 |
|---|---|---|
| 조준 | Play 시작 시 마우스 자동 잠금, 마우스로 시점 회전, 화면 중앙 십자 조준 | 각 컨트롤러 전방 |
| 왼손 연결/해제 | 왼쪽 마우스 버튼 누름/놓음 | 왼쪽 트리거 누름/놓음 |
| 오른손 연결/해제 | 오른쪽 마우스 버튼 누름/놓음 | 오른쪽 트리거 누름/놓음 |
| 지상 이동/제한된 공중 조향 | WASD | 왼쪽 스틱/트랙패드 |
| 점프 | Space | 오른손 primaryButton (Quest A) |
| 최근 CP 복귀 | R | 왼손 secondaryButton (Quest Y) |
| 전체 재시작 | T | 왼손 menuButton; 완료 후에는 복귀 버튼도 전체 재시작 |
| 마우스 잠금 해제 | Esc | 해당 없음 |

Unity Editor의 Game 뷰에 포커스를 두고 테스트한다. Esc로 잠금을 풀었어도 좌/우 클릭하면 다시 잠그면서 즉시 발사한다(가운데 클릭으로도 잠금 가능). 버튼을 누르는 동안 연결하고 놓으면 해제한다. 십자는 연결 가능한 표면을 향할 때 녹색이다. 데스크톱 발사 Ray는 카메라 중앙을 따르고 줄 그림은 해당 손에서 시작한다. `SwingInput.mouseSensitivity`(도/픽셀)와 `captureMouseOnStart`로 감도·시작 시 잠금을 조정할 수 있다. VR에서는 계속 각 손의 추적 방향으로 발사한다.

마우스 조작과 스윙 코어는 2026-10-20 실제 Unity PlayMode 회귀 테스트에서도 통과했다. 화면 중앙 발사 Ray를 검증하며, 상세 멀미 완화 모드·중단 입력·튜닝은 [Comfort.md](Comfort.md)를 참고한다.

한 번에 줄 하나만 존재한다. 반대 손의 **성공한 발사**는 기존 줄을 교체하며 기존 속도를 유지한다. 실패한 발사는 기존 연결을 보존한다. 해제는 현재 줄 소유 손의 버튼을 놓을 때만 적용된다. 소유권을 넘긴 뒤 이전 손을 놓아도 새 줄은 유지된다. 금색 구체는 마지막 발사 손의 유효 조준점이다. 매우 가까운/범위 밖의 연결은 거부된다.

## 구성과 계산

| 파일/컴포넌트 | 책임 |
|---|---|
| SwingInput | Input System 콜백에서 입력 edge와 당시 조준 Ray를 FIFO에 저장; FixedUpdate에서 순서대로 소비. HMD 없는 경우 마우스 시점/손 배치 |
| SwingMotor | 루트 위치의 유일한 작성자. 중력, 속도, 지상 가속/공중 조향, CharacterController sweep, 착지, 줄 제약, 안전 복귀 |
| RopeMath | 느슨한 줄은 무보정; 초과 길이만 보정하고 바깥쪽 속도 성분만 제거 |
| WebRope | 표면 Raycast, 연결점/길이/소유 손, LateUpdate의 LineRenderer와 조준 표시 |
| SwingCourse | Ready → Playing → Complete, CP 순서와 swept segment 통과 검사, 최근 CP 및 전체 재시작 |
| SwingHud | 속도·접지·줄 길이·연결 상태·조준 가능·CP·조작 안내. 표시 문자열은 10Hz로 갱신 |
| SwingVisualSmoothing | 50Hz 물리 위치 사이에서 추적 그룹의 시각 위치만 보간; 리그 루트/추적 카메라 로컬 자세를 수정하지 않음 |
| SwingComfort | 설치된 XRI TunnelingVignetteController에 빠른 VR 이동을 연결. 비활성화 시 provider 정리 |
| SwingSceneBuilder (Editor 전용 assembly) | 회색 박스 씬, 설정, 정적 건물 프리팹, 머리·양손 TrackedPoseDriver 및 XR Origin 생성 |

CharacterController + 직접 제약 계산을 선택했다. 학생 팀이 속도와 보정량을 추적하기 쉽고, Joint solver와 XR Origin 이동이 서로 위치를 수정하는 일을 피할 수 있다. 런타임 전역 이벤트/DI는 없으며 직접 호출로 진행을 연결한다.

초기 길이는 **고정된 신체 pivot과 Raycast 연결점 거리**다. 줄이 팽팽할 때만 최대 길이를 제한하고 접선/안쪽 속도는 보존한다. 해제·재연결은 속도를 초기화하지 않는다. 보정은 CharacterController.Move로 sweep하여 벽을 통과하지 않는다. 보정 예산 초과 또는 벽 때문에 길이를 만족하지 못하면 줄을 해제한다. 고속 이동은 속도 제한과 최대 0.2m 이동 substep으로 얇은 정적 벽의 통과를 줄인다. 체크포인트도 이동 구간을 검사한다. 정지·영벡터는 epsilon으로 처리한다.

## VR 리그와 수동 설정

- XR Origin 루트는 발 위치이며 높이 1.8m/반경 0.3m의 고정 capsule을 가진다. 카메라와 손은 별도 추적 그룹 아래에 있고 Input System TrackedPoseDriver가 실제 자세를 입력한다. Floor tracking을 기본값으로 요청하고 Device fallback 카메라 높이는 1.65m이다.
- **실제 HMD 이동으로 capsule 중심/크기와 물리 pivot은 변하지 않는다.** 줄 계산 pivot은 발 루트 + 0.9m이고 화면상 줄 시작점은 추적 손이다. 머리/손만 움직여도 물리 줄 길이와 속도는 변하지 않는다. 현재는 제자리 VR 기준이므로 실제로 capsule 바깥으로 걸으면 머리가 벽에 들어갈 수 있다. 룸스케일 신체 추종 및 머리 충돌 fade는 후속 작업이다.
- 자동 카메라 회전/롤/흔들림은 없다. 데스크톱 시점 회전은 HMD 미감지일 때만 수행한다. 렌더 보간은 추적 그룹의 부모만 움직인다.
- 유효한 XR Head 장치가 없으면 데스크톱 모드다. 강제 데스크톱은 SwingInput.forceDesktop으로 설정한다. 실행 중 모드를 바꿀 경우 입력 큐를 초기화한다.
- OpenXR 런타임과 해당 컨트롤러 interaction profile을 장비에 맞게 Project Settings > XR Plug-in Management > OpenXR에서 확인해야 한다. VIVE Pro는 SteamVR OpenXR 런타임과 해당 컨트롤러 프로필, Quest 3는 Android OpenXR/Meta 설정과 컨트롤러 프로필, 빌드 모듈과 장비 배포 설정을 확인한다. 기존 사용자 XR 설정은 유지했다.
- VIVE wand처럼 secondaryButton이 없는 장비에서는 SwingInput의 복귀 바인딩을 지원되는 버튼으로 바꾼다. menuButton이 런타임 시스템 메뉴에 예약된 장비에서는 전체 재시작 바인딩을 다른 사용 가능한 버튼으로 바꾼다.
- 장비 없이 OpenXR 자동 초기화하면 `XR_ERROR_RUNTIME_UNAVAILABLE` 진단이 나올 수 있다. 데스크톱 전용 검증 시 Standalone의 Initialize XR on Startup을 잠시 끌 수 있다. 이 프로젝트 설정은 자동으로 바꾸지 않는다.
- 비네트는 기존 XRI 제어기/메시와 전용 URP 머티리얼을 사용한다. SwingComfort의 A/B/C 모드와 Tuning에서 속도·예측 점수, 최대 강도·복원·시야 확보를 조정한다. M으로 모드 변경, P/F1로 메뉴·일시정지, Backspace로 안전 복귀 후 중단한다. 장비별 체감 검증이 필요하다.

## 파라미터와 성능

`Settings/DefaultSwingSettings.asset`의 Inspector Tooltip에 단위와 역할을 기재했다.

| 항목 | 기본값 |
|---|---:|
| 중력 | 18 m/s² |
| 최대 속도 | 35 m/s |
| 지상 가속 / 지상 속도 | 24 m/s² / 8 m/s |
| 공중 조향 / 점프 | 4 m/s² / 8 m/s |
| 최대 연결 거리 / 최소 줄 길이 | 40m / 1m |
| 물리 step당 보정 예산 / 길이 허용 오차 | 1.5m / 0.08m |
| 충돌 이동 substep | 0.2m |
| 물리 pivot 높이 / 낙하 실패 높이 | 0.9m / Y -8m |

attachMask는 생성 씬의 layer 8이다. 프로젝트 Layer 이름은 변경하지 않았다. 플레이어 capsule은 Ignore Raycast layer 2이며 자기 몸은 조준/접지 검사에서 제외한다. 연결 Raycast는 모든 일반 충돌체의 첫 hit를 검사하므로 연결 불가 벽 뒤 타깃에 붙지 않는다. 타깃은 정적 물체만 지원한다.

기존 Fixed Timestep **0.02초(50Hz)**를 유지했다. XR 표시 주사율과 물리 주기는 별도다. 줄은 Rigidbody 체인 대신 LineRenderer 하나를 사용한다. 정적 프리미티브/불투명 Unlit 재질을 사용하고 새 코스의 실시간 그림자는 끈다. 런타임 전체 씬 탐색과 반복 GetComponent는 없다. HUD의 제한된 문자열 갱신을 제외하고 이동/줄 업데이트에서 프레임별 배열·컬렉션 생성을 하지 않는다. VIVE Pro 90FPS 및 Quest 3 지원 모드 120FPS는 **향후 실측 목표**이며 달성을 검증한 것이 아니다.

## 검증

Unity 6000.3.24f1 batch Editor로 컴파일 및 실제 씬/프리팹/재질/설정 생성에 성공했다. 제한 실행 환경에서 최초 Editor 프로세스가 소켓 초기화 중 충돌하여 일반 실행 환경으로 검증했다.

`Window > General > Test Runner`에서 EditMode의 `VRWebSwing.Tests`(8개)와 PlayMode의 `VRWebSwing.PlayModeTests`(통합 2개)를 각각 실행한다. 기존 스윙/입력/벽 충돌/복귀 회귀와 비교 모드/예측/중단/CSV를 검증한다. 이전 2026-10-14 기록은 당시 코어 검증 이력이며 최신 검증은 루트 README와 Comfort.md를 참고한다.

배치 명령:
```text
Unity.exe -batchmode -nographics -projectPath <project> -runTests -testPlatform EditMode -testFilter VRWebSwing.Tests -testResults <project>/Logs/WebSwing-Tests.xml -logFile <project>/Logs/WebSwing-Tests.log
Unity.exe -batchmode -nographics -projectPath <project> -runTests -testPlatform PlayMode -testFilter VRWebSwing.Tests -testResults <project>/Logs/WebSwing-PlayMode.xml -logFile <project>/Logs/WebSwing-PlayMode.log
```

로그는 `Logs/WebSwing-Generate.log`, `Logs/WebSwing-Tests.log`, `Logs/WebSwing-PlayMode.log`이고 결과 XML도 Logs에 있다. Logs는 기존 gitignore 대상이다.

### 최종 검증 기록 (2026-10-14)

- 동일 버전 Unity Editor 씬 생성 및 스크립트 컴파일: 성공, 종료 코드 0. 새 코드의 컴파일 오류와 런타임 NullReference/ArgumentException 없음.
- EditMode: **4/4 통과** (느슨한 줄, 최대 길이 보정, 접선/안쪽 속도 보존, 일치하는 연결점의 영벡터 안전성).
- 실제 PlayMode: **1/1 통합 테스트 통과**. 물리 업데이트 사이에 두 번의 누름/놓음(총 4 edge)을 실제 Input System에 주입하여 모두 순서대로 보관됨을 확인했다. 성공/실패 연결, 소유 손 해제, 반대 손 재연결, 해제 관성, 근접 연결 거부, 30 물리 단계의 진자 길이 제약, 얇은 벽 충돌, 착지, 자동 낙하 복귀, 순차 CP 및 최근 CP 복귀, 완료·전체 재시작까지 확인했다.
- 생성 도구 재실행: 종료 코드 0, 씬 SHA-256 동일, 파일 수 **56 → 56**. 중복 생성 및 기존 씬 덮어쓰기 없음 (`Logs/WebSwing-Idempotence.log`).
- 기존 패키지/Unity 버전/물리 주기 유지. 템플릿 씬은 보존하되 빌드 실행 대상에서 제외했다.
- 장비의 OpenXR 런타임이 없어 XR 초기화 진단은 발생했다. 데스크톱 테스트는 계속 정상 진행했다. HMD/실제 컨트롤러 입력, 렌더 화면 시각 검수, 장비 빌드·배포, 비네트 체감, 실제 VR 프레임 성능은 미검증이다.

## 알려진 제한과 후속 작업

- 데스크톱 자동 검증은 실제 VR 추적·컨트롤러 바인딩·멀미·주사율 검증을 대체하지 않는다. HMD 실측, Android 빌드/장비 설치, 렌더 시각 품질 점검은 별도다.
- 정적 건물에 줄 하나만 지원한다. 움직이는 연결점, 줄 벽 감김, 두 줄 동시 연결, 전투/도시 콘텐츠는 없다. 줄이 벽에 가려지는 시각적 교차는 허용하며 신체 보정이 벽에 막힐 때 해제한다.
- 룸스케일 capsule 추종, 손 모델, snap turn, 장비별 재시작 버튼/입력 에셋 외부화는 후속 작업이다.
- 목표 프레임 성능, 보정 예산, 공중 조향, 비네트 강도, 코스 난이도를 실제 장비에서 조정해야 한다. 플랫폼과 CP 크기는 처음 연결하기 쉽게 넓게 잡았다.
- substep은 정적 회색 박스 코스용이다. 복잡한 모서리·이동 장애물·극단적인 프레임 정지까지 일반적인 CCD 보장을 하지 않는다.
