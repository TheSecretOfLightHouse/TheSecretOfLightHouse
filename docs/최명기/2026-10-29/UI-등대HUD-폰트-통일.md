# UI 등대 HUD 폰트 통일
작성자: 최명기
작성일: 2026-10-29
갱신일: 2026-10-29
상태: 구현됨(미검증)

## 요청과 목표
사용자는 게임 화면의 등대 수리도 HUD 글씨체를 본인 UI 전체에 적용하도록 요청했다. 확인 질문으로 정식 등대 상호작용창·개발 패널이 아닌 HUD임을 확인했다.

## 진행 과정과 결정 이유
- FinalBattleHud 프리팹의 RepairValue는 LegacyRuntime/Bold로 저장되며 실행 시 맑은 고딕 우선 OS 폰트로 교체된다.
- 개인 인벤토리·능력·구조 UI도 같은 OS 폰트 목록을 사용하고 있어 실제 차이인 Bold 스타일을 맞췄다. 크기·색·배치는 유지했다.
- InventoryChestScreen 초기 Text 및 런타임 MakeText, GuardShieldSelectionPanel, EmergencyRepairSelectionPanel, NavigatorRouteSelectionPanel, TemporaryRescuePanel 초기화에 fontStyle=Bold를 적용했다. 운반 HUD도 기존 MakeText 경로를 사용한다.
- ObstacleAlarmHud는 텍스트 없는 이미지 UI라 변경하지 않았다. 다른 담당의 등대 UI·프리팹과 씬은 수정하지 않았다. 두 개인/통합 씬에서 공유 코드가 적용된다.

## 변경 파일
- Assets/01.Scripts/05.Player/Inventory/View/InventoryChestScreen.cs
- Assets/01.Scripts/05.Player/Inventory/View/InventoryChestScreen.Layout.cs
- Assets/01.Scripts/05.Player/Abilities/View/GuardShieldSelectionPanel.cs
- Assets/01.Scripts/05.Player/Abilities/View/EmergencyRepairSelectionPanel.cs
- Assets/01.Scripts/05.Player/Abilities/View/NavigatorRouteSelectionPanel.cs
- Assets/01.Scripts/05.Player/Recovery/TemporaryRescuePanel.cs
- 이 원문과 POINTER.md.

## 검증
- Unity 재컴파일 completed, errors 0.
- MainGame을 PreviewScene으로 열어 5개 화면의 Awake를 격리 호출했다. InventoryChestCanvas 67/67, 능력창 각각13/13, RescueCanvas3/3로 109개 Text가 모두 Bold임을 확인했다. 런타임 생성 인벤토리·운반 HUD 포함. PreviewScene은 저장 없이 닫았고 기존 MainOffline 활성 씬을 복원했다.
- 코드 diff는 각 위치 1줄씩 총6줄 추가다.
- 실제 Play 시각·해상도별 줄바꿈/잘림·입력·원격 M2는 미실행.

## 남은 일과 다음 행동
실제 인벤토리 각 탭·직군 선택·구조 화면에서 글자 잘림과 읽기 편한지 확인한다. OS 폰트 목록의 플랫폼별 대체 동작은 기존과 같다.
