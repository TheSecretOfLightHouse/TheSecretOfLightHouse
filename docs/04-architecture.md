# 등대 소프트웨어 아키텍처

기준일: 2026-10-11. 게임은 리슨 서버(호스트 1 + 원격 클라이언트 3)를 사용한다. 기능별 담당자가 서버·클라이언트를 함께 구현한다. [담당표](CODE_OWNERSHIP.md)가 소유권 기준이며 [코딩 컨벤션](coding-convention.md)이 코드 스타일 기준이다. 게임 규칙은 [승패·투표 결정](design/win-vote-decisions.md)이 충돌하는 [v4](design/references/game-design-v4.md) 규칙보다 우선하며, 기술 연결은 [기술설계](03-technical-design.md)를 따른다.

## 1. 폴더와 담당

`Assets/01.Scripts/` 아래 구조다. 서버·클라이언트별 전역 폴더 대신 도메인 안에서 책임을 나눈다.

| 폴더 | 담당 | 기능 |
|---|---|---|
| `00.Common/` | 김지훈 조율, 관련 담당 합의 | 기능 간 공용 계약·기반. 기능 전용 타입은 각 도메인에 둠 |
| `01.Network/` | 김지훈 | 세션·NetworkManager·스폰 등록·연결 조회·판 진행 중 이탈 시 판 종료, 재접속은 10/25 이후 선택 |
| `02.World/` | 강민서 | 바다·날씨·시간·안개·맵·섬·파밍·배치·특수 섬·유령선·환영 섬·토네이도 |
| `03.Interaction/` | 김지훈 | 대상 탐지·선택·대상 확인·요청·Relay·Router·Validator·지속 상호작용 세션·기록·유령선 힌트 |
| `04.Lighthouse/` | 김지훈 | 작업대·모듈·부식 낙하·등대 |
| `05.Player/` | 최명기 | 배·이동·입력·상태·직군 능력·인벤토리·채집 행동·애니메이션 연결 |
| `06.Monsters/` | 박태욱 | 몬스터 AI·스폰 실행 |
| `07.Mafia/` | 박태욱 | 투표·심해 게이지 |
| `08.Systems/` | 최명기 | 기존 시스템 범위 및 각 기능의 서버·클라이언트·동기화 |
| `09.UI/` | 김지훈 | 상시 HUD 등 게임 UI. 인벤토리 화면·배 모듈 장착 UI는 `05.Player/Inventory/View/`로 최명기 담당 (2026-10-30 사용자 결정) |
| `99.Dev/` | 관련 기능 담당 | 공용 개발·검증 도구, 4인 테스트는 김지훈 리드 |

Unity 루트의 `Assets/00.Scenes`, `02.Prefabs`~`07.Materials`, `Packages/`, `ProjectSettings/` 구성은 유지한다. 번호 폴더의 구분자는 점으로 통일한다. `Assets/00.Scenes/` 아래 사람별 개발 씬 폴더(Minseo, Jihun, Myeongki, TaeWook)는 이동·개명하지 않고, [CODEOWNERS](../.github/CODEOWNERS)에서 폴더 주인을 리뷰 담당으로 지정한다. 문서·자동화·스킬은 AX 영역이며 김민성을 게임 코드 담당으로 지정하지 않는다.

## 2. 기능 내부의 책임

| 층 | 책임 | 실행 |
|---|---|---|
| `Rules/` | 계산·정의·상태 전이·계약 | 호출되는 기능의 권위 경계에 따름 |
| `Net/` | NetworkBehaviour·지속 상태·Command/Rpc 입구·전송 계약 데이터 | Mirror 서버/클라이언트 콜백을 구분 |
| `Server/` | 요청 검증·자원 변경·몬스터 판정 등 | 리슨 호스트의 서버 측에서 실행 |
| `View/` | 로컬 입력·UI 연결·애니메이션·소리·표현 | 소유 클라이언트 또는 관찰 클라이언트 |

필요한 층만 생성한다. 기능 루트에는 아직 책임이 혼합된 기존 코드를 둘 수 있다. 이동만으로 로직 분리·네트워크 권위·복원이 구현되었다고 취급하지 않는다. 같은 기능의 네 층은 같은 담당자가 수정한다. 공용 네트워크 담당자가 모든 `Net/`·`Server/`를 소유하지 않는다.

## 3. 어셈블리

`Assets/01.Scripts/Lighthouse.Runtime.asmdef`가 기능별 런타임 코드를 포함한다.

- 참조: `Unity.InputSystem`, `Mirror`.
- `autoReferenced: true`, `noEngineReferences: false`. 플랫폼 제한·서버 전용 define constraint는 없다.
- 리슨 호스트 실행에 필요한 `Server/`도 일반 플레이어 빌드에 포함한다. 전용 서버 어셈블리·`UNITY_SERVER` 전용 제외를 도입하지 않는다.
- 에디터와 테스트를 도메인 아래에 추가할 때는 Editor/Test 전용 asmdef로 분리한다. `Editor/`라는 이름만으로 부모 Runtime asmdef의 참조 경계를 해결했다고 보지 않는다.
- `99.Dev/`의 개발 전용 기능은 `UNITY_EDITOR || DEVELOPMENT_BUILD` 조건으로 감싸 릴리스에서 제외한다. 에디터 전용 API는 별도 Editor asmdef로 분리한다. 현재 `Network/ActorIdProbe.cs`에 JH01 송신자·ActorId 대조용 개발 코드가 있다.
- URP·uGUI·TMP 등 추가 참조는 실제 소비 코드가 생길 때 김지훈과 해당 기능 담당자가 영향 범위를 확인한 뒤 Runtime asmdef에 등록한다.
- 폴더 소유권은 코드 리뷰 경계다. 단일 asmdef가 도메인 간 의존을 컴파일러에서 금지하지는 않으므로 공용 계약과 의존 방향은 담당자 합의로 검토한다.

## 4. 현재 코드의 배치

표의 경로는 `Assets/01.Scripts/` 기준이다. 기존 Player 코드와 추가된 기능 계약·접속 기반을 기능별로 배치한다.

| 기능 | 위치·현재 상태 |
|---|---|
| 플레이어 제어 | `05.Player/Control/PlayerController.cs` — 기존 입력·상태·물리·승하선 연결 |
| 입력 | `05.Player/Control/View/PlayerInputReader.cs`. Player 씬의 실제 입력 에셋은 `Assets/InputSystem_Actions.inputactions`이며 최명기 담당. 이동한 `05.Player/Control/View/InputActions.inputactions`는 현재 씬 참조 없음 |
| 배 제어 | `05.Player/Boat/BoatController.cs`, `Boat/Rules/ShipPhysicsProfile.cs` |
| 이동 | `05.Player/Movement/PlayerMotor.cs`, `Movement/Rules/MoveConfig.cs` |
| 상태 | `05.Player/State/Rules/` — PlayerHFSM·StateMachine·상태 타입 |
| 능력 | `05.Player/Abilities/` — PlayerAbility·Runner, `Rules/`의 능력 계약 |
| 애니메이션 | `05.Player/Animation/View/` — Driver·Source·인터페이스 |
| 섬·바다 계약 | `02.World/Islands/Rules/IslandHandle.cs`, `Ocean/Rules/SeaZone.cs` |
| 배치 계약 | `02.World/Spawning/Rules/SpawnerPoint.cs` — 실제 스폰 실행은 Monsters 담당 |
| 자원 계약·정의 | `02.World/Harvest/Rules/SpotInfo.cs`, `ResourceDefinition.cs`, `ResourceRarity.cs` |
| 상호작용 계약 | `03.Interaction/Requests/Net/InteractionRequest.cs`·`InteractionResult.cs`·`InteractionAction.cs`, `Requests/Rules/`(대상 종류·결과 코드), 처리기 계약 `Router/Server/IInteractionHandler.cs`, 세션 처리기 계약 `Sessions/Server/IInteractionSessionHandler.cs`, 비네트워크 대상 계약 `Targets/Server/INonNetworkTargetAdapter.cs`, 대상 표식 `Targets/Net/InteractionTarget.cs` |
| 접속·참가자 식별 | `01.Network/Session/Net/LighthouseNetworkManager.cs`, `Connections/Server/ConnectionDirectory.cs`, `Connections/Net/NetworkActor.cs` — JH01의 4인 접속과 송신자/ActorId 식별. JH07(같은 파일 + `Session/Net/LobbyPlayer`·`SessionJoinAuthenticator`·세션 메시지, `Session/Rules/MatchPhase`·`SessionEndReason`)이 로비 준비·자동 시작·전원 로딩 확인·판 진행 중 이탈 시 판 종료를 맡는다. 판 시작 게이트 `Session/Rules/IMatchStartGate`(2026-10-16): 시작 전 준비가 필요한 기능이 서버에서 `LighthouseNetworkManager.RegisterStartGate`로 등록하면 전원 로딩과 모든 게이트 준비 뒤에만 `MatchStarted`를 낸다. 계약을 Network에 두고 기능이 등록하는 방향은 Network가 기능 코드를 참조하지 않게 하려는 결정이다. 로비 설정이 없는 매니저는 기존 단일 씬 방식 |
| 투표 규칙 | `07.Mafia/Voting/Rules/VoteService.cs`, `VoteRecord.cs` — 참가자·횟수·같은 대상 재클릭 처리. 새 결정의 역할/자기 투표 제한·의식 잠금·서버 전송 통합은 완료 상태가 아님 |

StateMachine·State·Transition은 현재 MoveConfig에 의존하므로 Player 안에 둔다. ShipPhysicsProfile은 배 물리 정의로 Player가 관리하며 World에서 소비할 때 합의한다.

현 코드의 PlayerController 등은 MonoBehaviour 기반이다. Mirror 기반은 JH01의 공용 접속·ActorId 식별과 개발용 검사 코드이며, 게임 기능별 권위·동기화 통합은 아직 완료 상태가 아니다. 구현 기록은 [담당자별 작업 §6](06-owner-work-plan.md#6-처음-함께-맞출-일과-작업-기록)을 따른다. 빈 기능 폴더는 작업 위치를 표시하며 구현 완료를 뜻하지 않는다. 새 결정의 단절 객체·ActorId 기반 상태 보존을 별도 구현·검증하고 재접속은 후순위로 검토한다.

## 5. 기존 참조 보존

기존 스크립트 파일명·타입명·직렬화 필드를 유지한다. 전역 Player 타입 이름도 폴더 이동으로 변경하지 않는다. 신규 코드의 네임스페이스는 도메인을 따른다. 기존 `Lighthouse.Map.Net.*` 계약 9개는 소비하는 코드가 없는 것을 확인하고 도메인 네임스페이스로 이관했다: World 6개(`IslandHandle`, `SeaZone`, `SpotInfo`, `SpawnerPoint`, `ResourceDefinition`, `ResourceRarity`)는 `Lighthouse.World.{기능}.Rules`, `InteractionRequest`·`InteractionAction`은 `Lighthouse.Interaction.Requests.Net`, `ShipPhysicsProfile`은 `Lighthouse.Player.Boat.Rules`.

씬·프리팹 YAML을 직접 변경하지 않는다. 파일과 meta를 함께 이관하고 GUID를 보존한다. 기존 Player 씬의 다섯 스크립트 참조를 대조한다. 어셈블리가 Assembly-CSharp에서 Lighthouse.Runtime으로 바뀌므로 GUID 대조와 별개로 지정 Unity 버전의 컴파일·MonoScript 타입 복원·씬 로드 검증이 필요하다.

## 6. 기능 간 통합

스포너 배치/실행, 자원 지점/채집 행동, 유령선 연출/힌트처럼 둘 이상의 담당자가 만나는 계약은 [기능별 경계](CODE_OWNERSHIP.md#2-기능-사이의-경계)를 따른다. 한쪽 폴더로 옮겨 상대 담당자의 기능을 통째로 인수하지 않는다. 김지훈은 접속 기반과 4인 테스트를 조율하고 각 담당자는 자기 기능의 복원·동기화를 책임진다.

승패·투표 연결에서는 김지훈의 등대 상태, 박태욱의 투표·소환/공격력 판정, 강민서의 월드 시간·아침 이벤트를 최명기의 게임 단계·최종 승패 및 UI가 소비한다. 숨은 수리도·최후 전투 수리 진행도·현재/최대 HP를 구분하고, 연결 객체와 참가자 ID 기반 게임 상태도 구분한다. 게임 기한 정지/제거를 World 시간 전체 정지로 확대하지 않는다. 이 의미 계약의 반영은 API 합의·코드 연결·4인 실행 검증 완료를 뜻하지 않는다([기술설계 §6·§7](03-technical-design.md#6-상태와-실행값)).

## 7. 검증 경계

코드 이관의 GUID·원문·참조 정적 확인과 대상 버전의 실행 검증은 별개다. 원본 프로젝트의 버전·패키지·씬·설정을 업그레이드하지 않는다. 지정 버전 6000.0.83f1에서 컴파일·씬 로드·Play Mode·4인 접속을 확인하기 전에는 실행 완료로 표시하지 않는다.

폴더 이동 뒤에는 지정 버전에서 빌드 씬 목록과 `m_UseUCBPForAssetBundles: 0` 기본값을 확인해야 한다. 다른 버전의 에디터는 이 필드를 직렬화에서 생략할 수 있다. 그 외 ProjectSettings·ProjectVersion·Packages는 유지한다.
