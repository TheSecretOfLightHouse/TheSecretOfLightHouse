# MainGame UI 이식
작성자: 최명기
작성일: 2026-10-29
갱신일: 2026-10-29
상태: 구현됨(미검증)
브랜치·기준 커밋: feature/myeongki / 2886923d

## 요청과 목표
MK_MainGame에서 InventoryConnection을 제외한 표의 8개 오브젝트를 MainGame으로 복사한다. 통합 씬 수정은 이번 사용자 요청 범위다.

## 진행 과정과 결정 이유
- Unity 에디터 API로 InventoryChestCanvas, EmergencyRepairCanvas, GuardShieldCanvas, NavigatorRouteCanvas, NavigatorRoutePlanner, RescueCanvas, PlayerHandler, EventSystem을 자식과 함께 복사했다.
- NavigatorRoutePlanner._map을 MainGame WorldState의 MapStateNet으로 재연결했다.
- 두 씬의 동시 로드 중 Mirror가 기존 NetworkIdentity sceneId를 재생성한 것을 diff에서 발견했다. 원본 씬을 저장 없이 닫고 에디터 SerializedObject API로 MainGame의 기존 sceneId 8개를 원래 값으로 보존한 뒤 저장했다.
- InventoryConnection 및 추가 HUD 프리팹은 추가하지 않았다. MK_MainGame은 저장·수정하지 않았다. CLI 업데이트는 사용자가 거절하여 기존 버전으로 진행했다.

## 변경 파일과 결과
- Assets/00.Scenes/Main/MainGame.unity: 8개 루트 추가, 항로 맵 참조 연결.
- 이 기록과 담당자 POINTER.md 갱신.

## 검증
- 실행: 에디터 stopped/ready 확인, 대상 8개 존재 및 중복 없음, 복사 대상 Missing Script 0, MK 씬을 가리키는 참조 0, InventoryConnection 0, nonzero sceneId 중복 없음 확인.
- 저장 결과의 기존 직렬화 블록 비교로 기존 오브젝트 삭제·불필요한 설정 변경 여부를 확인했다.
- git diff --check는 Unity 직렬화의 빈 m_Name/m_EditorClassIdentifier 줄 뒤 공백을 보고했다. YAML 직접 편집 없이 에디터 저장 형식을 유지했다.
- 미실행: Play 입력·실제 접속·수리·구조·직군 능력·원격 M2 검증.

## 남은 일과 차단 조건
- 사용자가 제외한 InventoryConnection의 런타임 생성 작업대 연결은 별도 후속이다. 운반 모듈 표시까지 완료됐다고 볼 수 없다.
- 실제 Play/M2 확인 필요.

## 다음 행동
MainGame 실제 접속 후 인벤토리 열기·버튼 입력·직군 대상 선택·구조 화면을 확인한다.

## 2026-10-29 후속: InventoryConnection 추가·실제 생성 작업대 연결
- 사용자 후속 요청으로 InventoryConnection 제외 범위를 해제했다. MainGame에 같은 이름의 루트와 PlayerInventoryModuleBinder를 추가하고 저장했다.
- PlayerInventoryModuleBinder에 선택적 MainIslandFacilitiesBuilder 참조를 추가했다. MainGame에서는 LocalMapBuilders의 해당 컴포넌트를 연결하고 고정 Workbench는 None으로 둔다. 생성기의 Workbench 준비·변경·해제와 로컬 플레이어 변경을 따라 기존 BindModuleSource를 갱신한다.
- 기존 MK_MainGame은 Facilities 참조가 없으므로 기존 고정 Workbench 방식 그대로 동작한다. 시설 생성기·작업대·서버 판정 코드는 수정하지 않았다.
- 변경: Assets/01.Scripts/05.Player/Inventory/View/PlayerInventoryModuleBinder.cs, Assets/00.Scenes/Main/MainGame.unity, 이 원문과 POINTER.md.
- 검증: Unity 재컴파일 completed/failed false/errors 0. 비활성 preview 오브젝트의 Mirror netIdentity/isLocalPlayer와 localPlayer를 격리 설정한 코루틴 검사에서 고정 연결, 시설 대기 시 고정 참조 제거, 늦은 시설 연결, 시설 해제, 플레이어 이탈, 재연결, Binder 비활성 정리 7조건 통과. 최초 검사는 비활성 테스트 객체의 Mirror netIdentity 초기화 누락으로 실패했고 테스트 설정 보완 후 통과했다. 실제 접속 검증으로 확대하지 않는다.
- 저장 상태: InventoryConnection 1개, Facilities=LocalMapBuilders, 고정 Workbench=None, Missing Script 0, MainGame dirty false.
- 남은 일: 실제 모듈 획득→인벤토리/HUD 표시→배 장착·반환 및 원격 M2 확인. 앞 절의 InventoryConnection 미추가·별도 연결 필요 상태는 이 후속으로 갱신한다.
