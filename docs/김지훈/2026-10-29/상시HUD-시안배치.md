# 상시 HUD — 확정 시안대로 부품 배치

작성일: 2026-10-29

## 한 일
1. 사용자가 준 조립 시안(1672×941)과 부품 시트 2장을 받아, 시트를 알파 영역 기준으로 41장으로 잘라 `Assets/03.Images/InGameHud/`(`hud_*.png`)에 넣었다. 한 단어가 둘로 나뉜 영역(최후 전투, 시민 승리 문구 등)은 합쳤다. 숫자(64%, 1000/1000, 78/100)는 그림 대신 Text로 그린다.
2. 공용 배치 도우미 `HudSketchLayout`(Editor): 시안 픽셀을 1920×1080으로 환산하고, 그룹마다 화면 모서리(왼위·위가운데·오른위·왼아래·아래가운데)에 고정한다.
3. 최후 전투 띠 `FinalBattleHudSetup`을 새 부품·시안 좌표로 다시 짰다. 부제 문구를 없애고 목표 문구를 시안 그림(완성하면 시민 승리 / 파괴되면 마피아 승리)으로 바꿨다. 런타임 `FinalBattleHud`는 현재 체력 숫자만 금색이 되도록 바꿨다(`_healthNumberColor`).
4. 상시 HUD 새 프리팹 `Assets/02.Prefabs/UI/Hud/InGameHud.prefab`(`InGameHudSetup`, 메뉴 `Lighthouse/UI/Build In-Game HUD Prefab`·`Use In-Game HUD In Open Scene`)과 런타임 `09.UI/Hud/InGameHud.cs`. 렌즈 카드·직군 패널·단축키(M/V/Tab/Esc)를 배치했다(E 안내는 아래 추가 절에서 제거).
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

## 추가: 직군·배 패널 여백 (2026-10-29)
- 사용자 4인 화면에서 항해사 아이콘이 위 테두리에, 배 아이콘이 왼쪽 모서리에 닿았다. 사용자 요청대로 항해사·배 내구도 글자와 항해사 아이콘을 조금 줄였고, 추가로 패널을 396×146→410×150으로 넓혔다. 배 아이콘·막대·숫자도 조금 줄이고 테두리 안쪽으로 옮겼다. `100 / 100`도 테두리 안에 들어가는 것을 렌더링으로 확인했다.
- `- / -` 표시는 정상 동작이다. `JH04TestScene`에는 `PlayerBoatSpawner`가 없어 플레이어에게 내 배가 없다(MainGame에는 있음).
- 프리팹 재빌드, `Builds/JH04` 재빌드 성공, 창 4개 다시 실행(로그 예외 0).

## 추가: E 안내 제거·단축키 정렬 (2026-10-29)
- 화면의 "E [아이콘] 사용"은 상시 HUD의 시안 고정 안내 "E 작업대 사용"이었다. 그 "작업대" 글자를 작업대 화면이 아래 가운데에 그리는 들고 있는 모듈 카드가 덮고 있었다. 작업대·등대는 범위 안에서 자기 안내를 띄우므로, 늘 떠 있던 고정 안내를 상시 HUD에서 뺐다(부품 그림은 남김).
- 사용자 요청대로 단축키(M/V/Tab/Esc)를 오른쪽 위, 최후 전투 띠 바로 아래로 옮기고 1.12배로 키웠다. 키는 한 열 가운데 정렬, 설명 글자는 같은 왼쪽 선에 맞췄다.
- 프리팹 재빌드, 960×540 렌더링 확인, `Builds/JH04` 재빌드 성공, 창 4개 다시 실행(로그 예외 0).

## 추가: 원본 반원 날짜·낮밤 UI (2026-10-29)
- 요청: 첨부된 반원 날짜 UI의 형태·배치를 유지하고 분리된 부품 원화로 낮·밤 버전을 구성해 코드 연결. 작성 담당자는 사용자에게 김지훈으로 확인했다. 기존 HUD 합의 범위에서 확장하며 World 코드는 수정하지 않았다.
- 기획 대조: v4 2.1의 낮밤·일차와 서버 시간 소비는 기존 `GameClock`·`WorldStateNet`·`WorldServices`로 구현돼 있다. W15·W16/K03의 의식·최후 전투 시간 정책은 미정이므로 UI는 시간을 변경하지 않고 현재 시계만 읽는다.
- 원본 PNG 2장을 `Assets/03.Images/DayCycleHud/`에 복사했다. `Sprite.Create`와 에디터 에셋 API로 부품별 sprite rectangle을 만들었으며 재생성·재채색·배경 제거 없이 제공된 픽셀을 사용한다. 원본 crest는 첫 이미지, 배경·진행선·해·달은 부품 시트에서 가져왔다.
- `09.UI/Hud/DayCycleHud.cs`: 서버 시계와 같은 `IClock.Now - GameStartTime`·설정값으로 일차, 낮밤, 구간 진행률을 계산한다. 황혼은 낮에 포함하며 밤 진입 시 남색 배경·푸른 진행선·달로 전환하고 밤 종료 시 다음 일차를 표시한다. 숫자 문자열은 일차·낮밤이 바뀔 때만 갱신한다. 시계가 없거나 정지 상태면 표시를 숨긴다.
- `Workbench/Editor/DayCycleHudSetup.cs`: 기존 `InGameHud.prefab`의 다른 요소를 유지하며 `Hud/DayCycle`만 갱신한다. 1920×1080 기준 상단 가운데, 위 여백 20, 크기 232×112로 배치했다. `InGameHudSetup.Build()` 끝에서도 호출해 전체 HUD 재생성 시 유지한다. 프리팹은 Unity API로 저장했으며 씬 YAML은 수정하지 않았다.
- 검증: Unity 컴파일 후 콘솔 error 0. 에디터 격리 객체의 `ManualClock`을 주입해 실제 `LateUpdate`를 실행했다. 0·150·299.999·300·360·419.999·420·840초에서 `DayCycleMath`와 일차/낮밤 그림 일치 8/8, Stop 후 숨김 통과. 원본을 사용하는 실제 프리팹의 낮·밤을 각각 928×448로 렌더링해 글자·진행선·아이콘 배치를 눈으로 확인했다(`output/day-cycle-hud/day.png`, `night.png`).
- 미실행: 이번 변경 이후 Player 빌드·Play·M2 이상 시험, 최후 전투 HUD와 동시 표시 및 다른 해상도 통합 확인. 이전 절의 빌드 성공은 이번 날짜 UI의 빌드 검증을 뜻하지 않는다. 기존 JH04TestScene은 동일 HUD 프리팹을 참조함을 확인했다.
- 상태: 구현됨(미검증). 다음 행동은 실제 플레이에서 동기화·재시작·최후 전투 동시 표시 확인. 커밋·푸시는 하지 않았다.

## 추가: V 투표 안내 제거, 투표 UI 미표시 원인 (2026-10-29)
- 사용자 요청대로 단축키 안내에서 V 투표 줄을 뺐다(M·Tab·Esc 3줄). 투표 화면은 투표 중일 때 오른쪽 아래에 자기 버튼을 띄운다.
- "투표창·정체 확인·크툴루 소환이 없어짐" 확인: 세 UI 프리팹은 `JH04TestScene`에 그대로 있다. 직전 시험 로그(`log2`)에 `Match 1 started. players=1`, `[TowerTrialRound] Started players=1 voting=False`가 찍혔고, 다른 세 창은 로비에 들어오지 않았다. 1명 판은 마피아뿐이라 시민 0명 → 투표 미시작이 된다(`TowerTrialRound.StartRound`의 `citizens > 0` 조건, 기존 동작). 그래서 `VotingScreen`(`HasOwnVoteState`)과 `IdentityActionScreen`(`HasOwnIdentityState`, 정체 확인·크툴루 소환)이 켜지지 않았다. 코드 복구는 필요 없다.
- 프리팹 재빌드, `Builds/JH04` 재빌드 성공, 창 4개 다시 실행(로그 예외 0). 2인 이상 판에서 세 UI 표시는 아직 미확인.

## 추가: 수리도 상시 표시, 정체 확인 선원 그림 제거, 투표·정체 확인 버튼 크기 (2026-10-29)
- `FinalBattleHud`: 수리도(왼쪽)는 판 진행 중(스폰된 판, `Ended` 전) 항상 표시한다. 제목과 내구도는 새 하위 그룹 `_battleRoot`(`Battle`)로 묶어 최후 전투에서만 켜고, 내려오는 연출도 이 그룹에만 준다. Setup에서 `Battle` 레이어를 만들어 연결했다.
- `IdentityActionScreenSketchSetup`: 확인창과 알림창 왼쪽 아래의 선원 그림(`vt_sailor`, 레거시 캐릭터 아이콘)을 뺐다. 투표 확인창에서 이미 뺀 것과 맞춘 것이다. 정체 확인·크툴루 소환 버튼은 0.9배(270×70)로 줄이고 오른쪽 끝에 맞췄다.
- `VotingScreenSketchSetup`: 투표 버튼을 1.4배에서 1.2배로 줄였다. 바닥은 28에서 55로 올려 왼쪽 아래 직군·배 패널 바닥(1080 − 893×1920/1672 ≈ 55)과 맞췄다. 정체 확인 버튼 묶음은 그 위(196)에 그대로 둔다.
- 이전 최후 전투 띠(커밋 `a4dce2c2~1` 프리팹)는 임시로 꺼내 렌더링해 사용자에게 보여 줬고, 임시 파일은 지웠다.
- 세 프리팹 재빌드, 1920×1080 렌더링 확인, `Builds/JH04` 재빌드 성공, 창 4개 다시 실행(로그 예외 0). 실제 플레이 확인은 아직이다.

## 추가: 날짜 UI를 등대 판 시안으로 교체 (2026-10-29)
- 사용자 요청: 위 "원본 반원 날짜·낮밤 UI"를 폐기하고, 새로 준 등대+판 시안(투명 PNG, 낮·밤 두 개)으로 맞춘다. 반원·해·달 부품(`day_cycle_parts.png`, `day_cycle_reference.png`, `.asset` 스프라이트 8개)은 미커밋 상태여서 지웠다.
- 시안에서 낮·밤 판을 각각 592×620으로 잘랐다. 일차·낮밤 글자는 실제 값으로 바뀌어야 하므로 글자 영역을 주변 판 색으로 메웠고, 막대는 채움을 지워 빈 막대로 만들었다(오른쪽 끝을 좌우 반전해 왼쪽 끝을 만들고 가운데는 빈 구간 반복). 채움은 9-slice 알약(108×37, 좌우 34px)으로 따로 뺐다. 밤 채움은 시안 끝이 옅은 테두리와 섞여 낮 채움 모양에 밤 색·질감을 입혔다. 결과: `Assets/03.Images/DayCycleHud/{day,night}_{badge,fill}.png`. 자르기 스크립트는 세션 임시 폴더에만 있다.
- `DayCycleHud`: 판 그림·채움 그림을 낮/밤으로 바꾸고, `"{n}일차"`·`낮/밤` 글자, 채움 폭(양 끝 둥근 모양 유지)을 현재 구간 진행률로 그린다. 시계 계산은 그대로다.
- `DayCycleHudSetup`: 상단 가운데, 위 여백 8, 0.3배(178×186). 글자는 LegacyRuntime 굵게 30·17.
- 검증: `dotnet build Lighthouse.Workbench.Editor.csproj` 오류 0. 시안 크기 미리보기를 Python으로 합성해 눈으로 확인했다. **Unity MCP가 끊겨(새로고침 시간 초과) 임포트·`Lighthouse/UI/Update Day Cycle HUD` 실행·프리팹 갱신을 하지 못했다.** 지금 `InGameHud.prefab`은 지워진 옛 스프라이트를 가리키므로 메뉴를 실행해야 한다.
- 사용자 실행에서 날짜 자리에 흰 사각형만 보였다(프리팹이 지운 옛 스프라이트 참조 → 빈 Image). MCP 재연결 뒤 메뉴 실행으로 프리팹 갱신, 콘솔 error 0. Play 모드에서 프리팹을 임시 생성해 `Render(3,낮,0.55)`·`Render(4,밤,0.3)` 강제 표시 → 스크린샷으로 판·한글·막대 표시 확인 후 종료(씬 저장 안 함).
- 미확인: 실제 판 진행 중 시계 연동 표시, 최후 전투 띠 제목(상단 가운데)과 겹침, `Builds/JH04` 재빌드.

## 추가: 작은 창에서 그림 글자 깨짐 (2026-10-29)
- 원인: 글자가 그려진 큰 원본(`id_check` 930×241 등)이 960×540 창에서 약 7배 축소되는데, 밉맵이 꺼져 있어 축소할 때 픽셀이 건너뛰어졌다. 이미지를 새로 만들 필요는 없다.
- 수정: `HudSketchLayout`·`VotingScreenSketchSetup`·`IdentityActionScreenSketchSetup`의 스프라이트 임포트를 밉맵 켬(Kaiser)·트라이리니어로 바꿨다. 네 프리팹 재빌드(텍스처 .meta 변경 포함), 960×540 렌더링에서 글자 테두리가 매끄러운 것을 확인했다.
- 작업대·등대·승리 화면 빌더도 같은 설정(밉맵 끔)이라 작은 창에서 같은 증상이 날 수 있다. 미수정.
- 빌드: 처음 두 번은 "script class layout is incompatible"로 실패했다. 빌드 도중 develop 병합(부력·애니메이션)과 다른 세션의 `DayCycleHud` 수정이 들어와 에디터 컴파일이 소스보다 늦었기 때문이다. 다시 컴파일한 뒤 성공했다(15:46). 창 4개 다시 실행(로그 예외 0).
- 사용자 요청: 최후 전투가 시작되면 날짜 UI를 끈다. `DayCycleHud`가 `TowerMatchNet`을 1초 간격으로 찾아(판과 함께 생성되므로 프리팹 연결 불가) `Phase == FinalBattle`이면 표시를 숨긴다. 최후 전투 띠와 상단 가운데 겹침도 이로써 사라진다. Unity 컴파일 error 0. 실제 최후 전투 진입 시 꺼지는지는 플레이 미확인.
- `Builds/JH04` 일반 빌드 성공(15:58, 새 DLL에 날짜 판·최후 전투 숨김 코드 포함 확인). 기존 창 4개를 닫고 960×540 창 4개를 다시 실행했다(`Builds/JH04/log1~4.txt`). 4인 플레이 확인은 사용자가 진행.
