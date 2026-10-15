# E키 공용 상호작용 돌 획득
작성자: 최명기
작성일: 2026-10-15
갱신일: 2026-10-15
상태: 구현됨(호스트 기능 확인, M2 미검증)
브랜치·기준 커밋: feature/myeongki / eb76660

## 요청과 목표
- 사용자가 만든 E키 Interact를 김지훈의 기존 공용 상호작용과 연결하여, 드롭 stone을 서버에서 인벤토리에 지급한 뒤 제거한다.
- 별도 주변 대상 검색이나 새 공용 요청 경로를 만들지 않는다.

## 진행 과정과 결정 이유
- 2026-10-15 후속 사용자 지시: 김지훈의 공용 코드를 수정하지 않고 Player 영역에서 E키가 자원 드롭만 줍도록 처리. PlayerPickupInput을 추가하여 기존 InteractionTarget.Active 목록에서 ResourcePickup이 있고 PickUp을 지원하는 범위 내 최근접 대상만 요청한다. 별도의 전역 대상 장부는 만들지 않으며 Relay/Router/서버 처리기를 재사용한다. tech 프리팹에서 PlayerInteractionInput만 제거하고 PlayerPickupInput을 부착했다. 공용 Selector 컴포넌트/원본 코드는 유지하지만 E키 요청에는 사용하지 않는다. 아래 기존 Selector 연결 설명은 이전 구현 이력이다. 현 단계 E키는 자원 줍기 전용이며 작업대 등 다른 E키 행동은 후속 연결이 필요하다.
- 현재 InputActions의 E키 Interact, PlayerController의 기존 InteractionRequested 경로를 확인했다.
- 기존 PlayerInteractionInput/InteractionSelector의 플레이어 부착이 빠져 있어 tech 프리팹에 연결했다. 공용 03.Interaction 코드는 수정하지 않았다.
- ResourcePickup은 서버에 자원 ID/수량과 초기화/획득 상태를 보관한다. HarvestDropSpawner가 서버에서 초기화한 뒤 Spawn한다.
- PickupInteractionHandler는 공용 Router를 통과한 PickUp을 처리한다. 플레이어 상태/인벤토리를 확인하고 지급 성공 시에만 드롭을 제거한다. 지급 실패 시 획득 잠금을 해제해 드롭을 유지한다.
- 인벤토리 제한 관련 미정 수치를 추가하지 않았다. N09의 동시 자원 거래 전체를 해결했다고 주장하지 않으며, 드롭당 일회 지급과 실패 시 유지 범위만 구현했다. N10의 기존 공통 검증을 재사용했다.
- 기존 Selector는 가장 가까운 모든 대상의 기본 Action을 실행한다. 채집 원본이 더 가까우면 원본이 선택될 수 있다. E키 전용 대상 필터가 필요하면 김지훈과 선택 계약을 맞춰야 한다. 이번에 공용 Selector를 변경하지 않았다.

## 변경 파일과 결과
- 후속: Assets/01.Scripts/05.Player/Inventory/View/PlayerPickupInput.cs 및 Unity 생성 meta 추가, tech 프리팹 입력 어댑터 교체. 공용 03.Interaction의 git diff 없음 확인.
- Assets/01.Scripts/05.Player/Inventory/Net/ResourcePickup.cs 및 meta 추가.
- Assets/01.Scripts/05.Player/Inventory/Server/PickupInteractionHandler.cs 및 폴더/스크립트 meta 추가.
- Assets/01.Scripts/05.Player/Gathering/View/HarvestDropSpawner.cs: 프리팹 ResourcePickup 검사와 Spawn 전 데이터 초기화.
- Assets/02.Prefabs/Player/tech.prefab: 기존 InteractionSelector/PlayerInteractionInput 부착.
- Assets/02.Prefabs/Player/stone.prefab: ResourcePickup 부착, InteractionTarget Default/Supported Action을 PickUp으로 설정.
- Assets/00.Scenes/Myeongki/MKTestScene.unity: 기존 HarvestServer에 PickupInteractionHandler 부착.
- 씬/프리팹 수정은 Unity PrefabUtility/EditorSceneManager API로 수행했다. 기존 사용자 변경을 유지했으며 YAML 직접 편집은 하지 않았다.

## 검증
- 후속 E키 회귀 시험: Unity 컴파일 통과. Host 로비 준비 API로 InProgress 진입 후 실행 중에만 임시 바닥/위치 구성. 원본 Sphere 거리 1.35m, stone 거리 2.27m로 기존 Selector.Selected가 Sphere인 상황을 만들었다. E키 모의 입력 결과 서버 요청 PickUp:Success만 1회, 수량 0→1(동기화도1), stone netId=5 제거 확인. 드롭이 없는 상태에서 E키 재입력 시 추가 요청/Harvest/생성 없음, 수량1 유지. 관찰 이벤트 해제 및 Play 종료, 저장된 시작 인원2 유지. 원격/동시 획득은 여전히 미검증.
- Unity 재컴파일 completed, failed=false, errors=[] 확인.
- Host Play 시험: 저장값은 바꾸지 않고 실행 중 시작 인원만 1로 설정한 뒤 기존 로비 RequestSetReady를 사용해 MKTestScene/InProgress 진입. 프레임 진행 확인.
- 기존 HarvestDropSpawner로 ID=1, 수량=1 드롭(netId=5) 생성. 대상을 격리해 선택 검증하기 위해 실행 중에만 임시 바닥과 플레이어/드롭 위치 조정.
- Input System E키 모의 입력 → 기존 PlayerInteractionInput/Selector/Relay/Router/픽업 처리기 경로로 서버 수량 0→1, 호스트 동기화 수량 1, netId=5 서버 객체 제거 확인.
- 카탈로그에 없는 ID(int.MaxValue)의 드롭(netId=6) 요청: Rejected, 드롭 유지, ID=1 수량은 1 유지.
- 이미 제거된 netId=5 재요청: InvalidTarget, 서버/동기화 수량 1 유지.
- 임시 RequestProcessed 관찰 구독 해제 후 Play 종료. 임시 바닥/이동/설정은 저장하지 않았다. Offline 활성 씬 dirty=false, 저장된 시작 인원=2 확인.
- 새 코드 및 수정 생성 코드 diff --check 통과, meta 생성 확인.
- 미실행: 원격 클라이언트 동기화·다른 플레이어 인벤토리 비노출·동시 E키 경쟁(M2/M3), 실제 HUD. 기존 에디터/부력 오류의 해결을 주장하지 않는다.

## 남은 일과 차단 조건
- 두 플레이어가 같은 드롭을 요청했을 때 한 번만 지급되는지 다인 확인.
- 원격 획득 시 모든 화면에서 드롭 제거 및 본인 수량 갱신 확인.
- 자원 줍기 외 작업대 등 E키 행동 통합 시 기존 공용 선택 정책과 연결 합의. 현재 자원 전용 선택은 사용자 지시로 Player 쪽에서 처리.
- 드롭 물리·위치 동기화 및 채집 잔량은 별도 기존 후속 범위다.

## 다음 행동
- Offline에서 2인 전원 준비 후 테스트 씬 진입 → stone 생성 → stone 가까이에서 E → 본인 수량 증가와 양쪽 제거 확인.

## 관련 원문·인수인계
- [왼쪽 클릭 채집](왼쪽클릭-채집대상-감지.md)
- [공용 연결 안내](../../handover/김지훈/2026-10-14/JH02-상호작용-최명기-연결안내.md)
