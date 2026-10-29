# 상시 HUD — 확정 시안대로 부품 배치

작성일: 2026-10-29

## 한 일
1. 사용자가 준 조립 시안(1672×941)과 부품 시트 2장을 받아, 시트를 알파 영역 기준으로 41장으로 잘라 `Assets/03.Images/InGameHud/`(`hud_*.png`)에 넣었다. 한 단어가 둘로 나뉜 영역(최후 전투, 시민 승리 문구 등)은 합쳤다. 숫자(64%, 1000/1000, 78/100)는 그림 대신 Text로 그린다.
2. 공용 배치 도우미 `HudSketchLayout`(Editor): 시안 픽셀을 1920×1080으로 환산하고, 그룹마다 화면 모서리(왼위·위가운데·오른위·왼아래·아래가운데)에 고정한다.
3. 최후 전투 띠 `FinalBattleHudSetup`을 새 부품·시안 좌표로 다시 짰다. 부제 문구를 없애고 목표 문구를 시안 그림(완성하면 시민 승리 / 파괴되면 마피아 승리)으로 바꿨다. 런타임 `FinalBattleHud`는 현재 체력 숫자만 금색이 되도록 바꿨다(`_healthNumberColor`).
4. 상시 HUD 새 프리팹 `Assets/02.Prefabs/UI/Hud/InGameHud.prefab`(`InGameHudSetup`, 메뉴 `Lighthouse/UI/Build In-Game HUD Prefab`·`Use In-Game HUD In Open Scene`)과 런타임 `09.UI/Hud/InGameHud.cs`. 렌즈 카드·직군 패널·단축키(M/V/Tab/Esc)·E 작업대 사용 안내를 배치했다.
   - 배 내구도만 실제 값: 로컬 플레이어 `PlayerController.OwnedBoat`의 `BoatDurability`를 매 프레임 읽는다(배에 타지 않아도 표시, 복구 배로 바뀌면 다시 연결). 배가 없으면 `- / -`.
   - 직군(항해사)·렌즈 카드·E 안내는 시안 고정 그림이다.

## 결정 이유
- 사용자 선택: "전체 + 배 내구도 연결". 왼쪽 아래 패널·단축키는 최명기(시스템·UI) 영역이나 사용자가 합의 완료로 간주하도록 결정했다. 런타임은 최명기 폴더 `09.UI/Hud/`에 두고, Editor 빌더는 새 asmdef를 피하려 기존 `Lighthouse.Workbench.Editor`에 뒀다.
- 최후 전투 띠는 국면에 따라 켜지고 상시 HUD는 늘 켜지므로 프리팹을 둘로 나눴다(정렬 60 / 55).
- 직군 데이터가 코드에 없어 연결할 수 없었다.

## 변경 파일
- 새: `Assets/03.Images/InGameHud/`(41장+meta), `Workbench/Editor/HudSketchLayout.cs`, `Workbench/Editor/InGameHudSetup.cs`, `09.UI/Hud/InGameHud.cs`, `02.Prefabs/UI/Hud/InGameHud.prefab`
- 수정: `Workbench/Editor/FinalBattleHudSetup.cs`, `Tower/View/FinalBattleHud.cs`, `02.Prefabs/UI/Tower/FinalBattleHud.prefab`(재빌드)
- 자르기 스크립트는 세션 임시 폴더에만 있다(저장소 밖).

## 검증
- Unity 컴파일 오류 0, 두 프리팹 메뉴 빌드 성공(콘솔 로그).
- 두 프리팹을 시안 위에 겹쳐 1920×1080으로 렌더링해 위치가 시안과 맞는지 눈으로 확인했다.
- 재빌드 뒤 FinalBattleHud 루트·컴포넌트 fileID가 유지되어 JH04TestScene·MainGame·MK_MainGame·TW_MainGame의 `_match` 연결이 살아 있음을 씬 YAML에서 확인했다.
- `InGameHud`를 `JH04TestScene`에 배치·저장했다(다른 씬은 미배치).
- `Builds/JH04` 일반 빌드 성공(`JH04Offline`→`JH07Lobby`→`JH04TestScene`), 960×540 창 4개 실행, 시작 로그(`Builds/JH04/log1~4.txt`) 예외 0. 실제 4인 플레이·HUD 표시 확인은 사용자가 진행.

## 남은 일
- 씬 배치: JH04TestScene 외 씬은 `Use In-Game HUD In Open Scene`으로 넣고 저장(공용·다른 담당 씬은 담당자 확인).
- 직군 데이터(최명기), 들고 있는 모듈 카드(기존 `CarriedModuleHud`와 중복 정리), 상호작용 대상별 안내 문구 연결.
- 이전 부품 `Assets/03.Images/FinalBattleHud/`는 `ScreenNoticeSetup`이 아직 쓰므로 유지.
- 4인 플레이로 배 내구도 갱신·최후 전투 띠 표시 확인.
