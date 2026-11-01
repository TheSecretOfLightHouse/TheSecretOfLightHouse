# 등대 수리도·내구도와 오른쪽 버튼 줄 좌우 여백 대칭 (JH04 시험)

작성일: 2026-10-31

## 요청 (사용자, 2026-10-31)
- 기준: 왼쪽 아래 직군·배 내구도 패널(`InGameHud/Hud/Panel`)과 화면 왼쪽 끝 사이의 간격.
- 등대 수리도(왼쪽 위)의 왼쪽 끝을 그 패널의 왼쪽 끝에 맞춘다.
- 투표 버튼·정체 확인·크툴루 소환 버튼 줄의 오른쪽 끝을 화면 오른쪽 끝에서 같은 간격에 둔다.
- 등대 내구도(최후 전투, 오른쪽 위)도 같은 간격으로 둔다. 결과적으로 수리도와 내구도가 좌우 대칭이다.
- 메인 씬은 건드리지 않고 JH04TestScene에서만 확인한다. 이식은 나중에 사용자가 한다.

## 수행
- 기준 간격: Panel 왼쪽 = 52.82(InGameHud 캔버스 단위, 1920×1080 기준·match 0.5).
- `JH04TestScene.unity`의 `FinalBattleHud` 프리팹 인스턴스 오버라이드(프리팹 자체는 수정 안 함)
  - 수리도 묶음(Lighthouse·RepairTitle·RepairBar·RepairValue·RepairGoal) x −57.42: 등대 아이콘 왼쪽 110.24 → 52.82.
  - 내구도 묶음(Shield·HealthTitle·HealthValue·HealthBar·HealthGoal·HealthDamage) x +66.60: 가장 오른쪽인 HealthBar 오른쪽 −119.42 → −52.82.
  - FinalBattleHud와 InGameHud는 같은 CanvasScaler(1920×1080, match 0.5)라 캔버스 단위 값이 해상도와 무관하게 화면에서도 같다. 흔들림·피해 숫자 위치는 FinalBattleHud가 Awake에 저장하므로 실행 중 이동이 아니라 씬 값으로 옮겼다.
- 신규 `Assets/01.Scripts/09.UI/Hud/HudEdgeAlign.cs`
  - 투표(`VotingScreen_Sketch/Hud`)·정체 확인(`IdentityActionScreen_Sketch/Buttons`)은 CanvasScaler가 match 0(가로 기준)이라 InGameHud와 배율이 16:9가 아닐 때 다르다. 그래서 실행 중 화면 픽셀 간격(패널 왼쪽 × InGameHud scaleFactor)을 각 캔버스 단위로 바꿔 오른쪽 끝을 맞춘다. 간격이 바뀔 때만 다시 적용한다.
  - 컴포넌트가 없는 씬(Main)은 기존 간격(오른쪽 28)을 그대로 쓴다.
- `JH04TestScene.unity`: 루트에 `HudEdgeAlign` 오브젝트 추가, `_edgePanel`=InGameHud Panel, `_rightGroups`=투표 Hud·정체 확인 Buttons.

## 검증
- Unity 컴파일 error 0.
- 씬 저장 후 값 확인(계산 결과 dl −57.42, dr +66.60).
- 플레이·빌드에서 화면 확인은 하지 않았다.
- refresh 중 Mirror가 `MainIslandWorkbench.prefab`의 `_assetId`를 0 → 3079912359로 자동 기록했다. 이 작업의 수정이 아니며 되돌리지 않았다.

## 남은 일
- JH04Offline → JH07Lobby → JH04TestScene 플레이로 수리도·버튼 줄·최후 전투 내구도 위치 확인.
- 메인 이식: MainGame의 FinalBattleHud 인스턴스에 같은 x 이동을 주고 `HudEdgeAlign` 오브젝트를 같은 참조로 추가(사용자 진행). 투표·정체 확인 프리팹은 박태욱 CODEOWNERS라 프리팹을 직접 바꾸려면 합의가 필요하다.
