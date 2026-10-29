# 등대 판 진행 UI 연출 — 기획·구현

작성일: 2026-10-26 · 갱신일: 2026-10-26 · 담당: 김지훈 · 브랜치: `feature/jihun`
상태: **①~⑤ + 추가 A~D + 공용 알림 띠 `ScreenNotice` 구현, M0(1인 호스트) 확인 완료. `feature/jihun`에 커밋·푸시(2026-10-27), develop 미병합.** `MainGame` 배치·M2는 남음 → [남은 일](#남은-일-최종).
읽는 순서: 기획(아래) → [구현](#구현-2026-10-26-다음-세션) → [추가 A~D·공용 띠](#추가-연출-ad와-공용-알림-띠-같은-날-이어서) → [남은 일](#남은-일-최종). 기획 세션의 [인수인계](../../handover/김지훈/2026-10-26/UI연출-세션인수인계.md)는 이 문서로 완료 처리.

## 배경

- 사용자: "게임이 재미없어 보인다." 마감 11/1, 기능 동결 10/25가 지난 시점이라 규칙 재기획(10-20~10-21 제안 8건, [총정리](../2026-10-21/JH04-기획제안-총정리.md))은 하지 않고, 체감을 올리는 연출부터 넣기로 했다.
- 등대 모델에 붙이는 연출(층별 불빛·창문)은 빼기로 했다. 사용자 설명: **등대는 마지막에 한 번 켜지고 탈출하는 이야기**라 층마다 불이 켜지면 어긋난다. 창문 모델도 없다.
- 그래서 **화면 UI 연출만** 한다. 카메라 흔들림은 최명기 `PlayerCamera` 영역이라 쓰지 않고, 화면 테두리 번쩍임(비네트, UI 이미지 한 장)으로 대신한다.

## 확정 연출 (사용자: "다 할거야")

| 순간 | 연출 | 문구 |
|---|---|---|
| ① 모듈 장착 | 수리도 게이지가 부드럽게 차오르며 반짝이고, 옆에 "+12.5%"가 떠올랐다 사라짐 | 알림 띠: "등대 부품 장착 (3/8)" |
| ① 층 완성 | 게이지에 금색 번쩍임, 보상 해금 알림 | 알림 띠: "2층 수리 완료 — 보상 해금" |
| ② 수리 100% | 중앙 큰 제목, 금색 테두리 번쩍임 | "등대를 밝힐 준비가 끝났다" |
| ② 의식 시작 | 중앙 제목 후 상단 60초 카운트. 마지막 10초는 숫자가 붉게 맥박침 | "점화 의식 시작" |
| ③ 소환 → 최후 전투 | 붉은 테두리 크게 번쩍, 중앙 제목, 최후 전투 HUD가 위에서 내려옴 | "심해의 괴물이 깨어났다" |
| ④ 등대 피격 | 내구도 바가 흔들리며 붉게 깜빡이고 피해 숫자 표시. 30% 미만이면 옅은 붉은 테두리가 계속 맥박침 | — |
| ⑤ 판 종료 | 화면이 서서히 어두워지며 결과 제목 | 시민 승: "등대에 불이 켜졌다 — 탈출하라" / 마피아 승: "등대는 어둠에 잠겼다" |

함께 정한 것:
- **장착자 이름은 표시하지 않는다.** 현재 결정 "보유자는 본인만 앎"([모듈 결정](../../design/lighthouse-module-decisions.md))을 바꾸지 않기 위해서다.
- **소리는 이번에 넣지 않는다.** 프로젝트에 게임용 오디오가 없다(Mirror 예제만). 클립 칸만 두고 비면 건너뛴다. 사용자가 a/b/c 선택을 따로 답하지 않아 기본값 (b)로 진행.
- **메인 씬은 복사하지 않는다.** 연출은 Canvas 프리팹 하나로 만들고 `JH04TestScene`에서 확인한 뒤 `MainGame`에 배치한다.
- 다른 영역 알림(날짜 전환 `GameClock` — 강민서 `02.World/Clock`, 무고 공개 `InnocentCitizenRevealed` — 박태욱)은 이번 범위에서 제외.

## 확인 안 된 것

- "탈출"이 결과 문구만인지 실제 플레이 단계인지 사용자 답 없음. 현재 규칙([승패·투표 결정](../../design/win-vote-decisions.md))에는 탈출 단계가 없으므로 **문구로만** 쓴다.

## 작업대 표시 확인 (같은 대화)

- 질문: 작업대와 선반이 둘 다 뜨는가 → 아니요. [TowerTrialPanel.cs](../../../Assets/01.Scripts/99.Dev/Tower/TowerTrialPanel.cs) `ApplyWorkbenchMode`가 한쪽만 켠다(선반 모드면 나무 모델 숨김). `JH04TestScene` 개발 패널 전용.
- ~~결정 대기 유지: 선반 모드에서 나무 모델도 보일지, 공용 `Workbench.prefab` 반영 여부.~~ → 2026-10-27 사용자 결정: **선반 방식 폐기, 본편은 화면 방식(`WorkbenchScreen`)**. 작업대·모듈·등대 UI 전체는 김지훈 담당(사용자 확인).

## 변경 파일 (기획 세션)

- 이 문서, 인수인계, 담당자 pointer. 기획 세션에서는 코드·씬 무변경. 구현 변경은 아래 각 절.

## 구현 (2026-10-26 다음 세션)

### 진행 중 결정
- **수리도 게이지 위치:** 최후 전투 HUD를 수리 단계에도 켜는 안(왼쪽 묶음만 표시)을 검토했으나, 사용자가 "안 어울릴 것 같다"며 **알림 띠 안의 작은 게이지**로 결정. HUD는 지금처럼 최후 전투 때만 보인다.
- **메인 씬 복사 안 함:** 연출은 프리팹 하나라 `JH04TestScene`에서 확인 후 `MainGame`에 프리팹만 놓는다(사용자 확인).
- **SyncVar hook 대신 매 프레임 읽기(인수인계 계획에서 변경):** `TryEnd`·`TryBeginFinalBattle`·`TryStartRitual`이 `_phase`를 먼저 바꾸고 결과·체력·기한을 나중에 바꾼다. hook은 호스트에서 대입 즉시, 원격에서 선언 순서대로 불리므로 phase hook 안에서 `Outcome`·`RitualEndsAt`이 아직 옛값이다. 그래서 `FinalBattleHud`와 같은 방식으로 `LateUpdate`에서 단계를 비교한다(그 시점에는 한 번의 변경이 모두 반영됨). `TowerMatchNet`은 바꾸지 않았다. 처음 본 단계는 연출 없이 받아들여 늦게 들어온 사람에게 지난 연출이 재생되지 않는다.
- **장착 신호:** `TowerNet.SlotInstalled(int slot)`을 추가. Mirror `SyncList.OnChange`는 `OP_SET`에 **이전 값**을 넘기므로([SyncList.cs:132](../../../Assets/Mirror/Core/SyncList.cs)) 새 값은 목록에서 읽는다. 호스트에서는 서버 대입 시 한 번, 원격은 변경 수신 시 한 번 불린다(초기 상태 수신은 콜백 없음).
- **층 완성 판단:** 장착된 칸의 층이 모두 찼을 때(`TowerNet.IsFloorComplete`). 보상 구간(`GetRewardTier`)이 이번 장착으로 올라가면 "— 보상 해금"을 붙인다. 시험 배치(2칸×4층, 25/50/75%)에서는 1~3층 완성이 보상 구간과 일치. 마지막 층 완성은 ② 중앙 제목으로 대신한다.
- 이름: 연출 View는 `TowerMatchEffects`(가칭 `TowerFx` 대신). `docs/` 검색 결과 같은 이름 없음.
- 테두리 이미지는 에셋 없이 실행 시 64×64 텍스처로 만든다. 소리는 클립 칸만 두고 비면 건너뛴다.

### 변경 파일
| 파일 | 내용 |
|---|---|
| `04.Lighthouse/Tower/Net/TowerNet.cs` | `SlotInstalled` 이벤트, `IsFloorComplete(floor)` |
| `04.Lighthouse/Tower/View/TowerMatchEffects.cs`(신규) | 알림 띠(문구·작은 게이지·"+12.5%", 대기열 8 — 이후 공용 `ScreenNotice`로 이전), 중앙 제목, 의식 카운트(10초 이하 붉은 맥박), 테두리 번쩍임(수리 100% 금색·소환 붉은색, 30% 미만 맥박), 종료 어둡게+결과 제목, 선택 소리 |
| `04.Lighthouse/Tower/View/FinalBattleHud.cs` | 위에서 내려오는 진입, 피격 시 바 흔들림·붉은 깜빡임(기본 색 변경)·피해 숫자(연속 피해 합산) |
| `04.Lighthouse/Workbench/Editor/TowerMatchEffectsSetup.cs`(신규) | 메뉴 `Lighthouse/Tower/Build Match Effects Prefab`, `Use Match Effects In Open Scene`. Canvas 순서 90(작업 화면 80 위, 소환 메뉴 100 아래) |
| `04.Lighthouse/Workbench/Editor/FinalBattleHudSetup.cs` | `HealthDamage` 라벨 추가·연결 |
| `99.Dev/Tower/TowerTrialRound.cs`, `TowerTrialPanel.cs` | 최후 전투 중 "등대 피해 100" 개발 버튼(`ApplyDamage`와 같은 경로) |
| 같은 두 파일(추가 요청) | "시민 승리 강제"·"마피아 승리 강제" 개발 버튼(판 종료 전 표시). `TryEnd`로 실제 종료와 같은 경로, 사유는 시민 `RepairCompleted`·마피아 `Deadline` |
| `02.Prefabs/UI/Tower/TowerMatchEffects.prefab`(신규), `FinalBattleHud.prefab`(재빌드) | 빌더 메뉴로 생성. HUD 컴포넌트 fileID는 그대로라 `MainGame`·`JH04TestScene`의 `_match` 연결 유지(확인) |
| `00.Scenes/Jihun/JH04TestScene.unity` | 메뉴로 연출 프리팹 배치·`_match` 연결, 에디터 저장 |

### 검증
- 실행함: 컴파일 오류 0(경고 1건은 최명기 `BoatController` 기존 경고). 프리팹 2개 빌드, 씬 저장.
- 실행함(M0, 에디터 1인 호스트, `JH04Offline` → 리플렉션으로 시작 인원 1·호스트 → 로비 준비 → `JH04TestScene`, 판 자동 시작):
  - 1회차: 서버에서 칸을 직접 채워 `SlotInstalled` 발생 → 띠 "등대 부품 장착 (1/8)"·게이지 0.125·"+12.5%", 2번째 칸 뒤 "1층 수리 완료 — 보상 해금". 개발 강제 소환 → `FinalBattle`, HUD 표시, 제목 "심해의 괴물이 깨어났다". 피해 버튼 7회 → HP 160(시험 촉수 공격 포함), 피해 숫자·30% 미만 테두리 맥박. 나머지 칸+`TryEnd(CitizensWin)` → 어둡게+"등대에 불이 켜졌다 — 탈출하라", 대기 알림 비움.
  - 2회차: 8칸 → "등대를 밝힐 준비가 끝났다"·금색 테두리. 의식 시작 → "점화 의식 시작"·카운트 60, 기한을 7.5초로 당겨 붉은 카운트 확인. 표 없이 종료 → `MafiaWin/RitualFailed`, 카운트 꺼짐, 어둡게+"등대는 어둠에 잠겼다".
  - 화면은 두 캔버스를 잠시 카메라 모드로 바꿔 1920×1080 렌더(`Temp/Screens/`, Git 제외). 오버레이 캡처는 Game 뷰(501×697)에서 UI가 빠져서 이 방식을 썼다. 명령 간격 때문에 일부 캡처는 타이머를 중간 값으로 맞춘 뒤 찍었다(동작은 상태값으로 따로 확인).
  - 콘솔 오류 0.
  - 강제 승리 버튼(추가 요청): 컴파일 0. 1인 호스트 판에서 각각 호출 → `MafiaWin/Deadline`·"등대는 어둠에 잠겼다", `CitizensWin/RepairCompleted`·"등대에 불이 켜졌다 — 탈출하라", 콘솔 오류 0. 패널 버튼 직접 클릭은 미실행(같은 메서드 호출).
- 미실행: 실제 장착 요청(`InstallModule`) 경로로의 연출(신호는 같은 SyncList 변경이라 동일할 것으로 예상, 미확인), M2 원격 클라이언트 재생과 호스트 1회 재생의 실측, `MainGame` 배치, 실제 소리, 맥박 순간의 시각 확인.

(남은 일은 문서 끝 [남은 일(최종)](#남은-일-최종)에 모음.)

## 추가 연출 A~D와 공용 알림 띠 (같은 날 이어서)

### 결정
- 사용자: 추가 연출 A(내구도 30% 첫 하락 제목)·B(마지막 1칸 알림)·C(모듈 완성 알림)·D(들고 있는 부품 표시) 진행. E(인출 공개)·F(종료 요약, 최명기)·G(보상 표시, 최명기)는 보류.
- 사용자: 알림 띠를 공용 유틸로. 정적 호출(`ScreenNotice.Show`) 추천안 채택. 기본 모습은 기존 띠 그대로, 알림별 아이콘·강조 3종, 전체 모양은 프리팹 인스펙터에서 변경.
- 위치 `00.Common/UI`(김지훈 조율 영역, 선례 `UiButtonFeedback`). 네트워크 없음 — 누가 보는지는 부르는 쪽이 정함. 빌더는 새 asmdef 없이 기존 `Lighthouse.Workbench.Editor`에.
- C 신호: 보관 개수 증가는 "되돌려 놓기"와 구별이 안 돼 `WorkbenchNet.RpcRecipeCompleted(recipeId)`(ClientRpc, 만든 사람 미포함)를 추가.
- D는 계속 떠 있는 표시라 띠 대신 `CarriedModuleIndicator`(왼쪽 아래). 본인에게만 오는 `OwnCarriedRecipe`를 읽음.

### 변경 파일(추가분)
| 파일 | 내용 |
|---|---|
| `00.Common/UI/ScreenNotice.cs`·`ScreenNoticeRequest.cs`·`ScreenNoticeStyle.cs`(신규) | 공용 알림 띠 |
| `04.Lighthouse/Workbench/Editor/ScreenNoticeSetup.cs`(신규) | `Lighthouse/UI/Build Screen Notice Prefab`, `Use Screen Notice In Open Scene` |
| `04.Lighthouse/Workbench/View/CarriedModuleIndicator.cs`(신규) | D |
| `04.Lighthouse/Workbench/Net/WorkbenchNet.cs` | `RecipeCompleted` 이벤트 + `RpcRecipeCompleted` |
| `04.Lighthouse/Tower/View/TowerMatchEffects.cs` | 자체 띠 제거 → `ScreenNotice` 사용, A·B·C 추가, 종료 시 `ScreenNotice.Clear()` |
| `04.Lighthouse/Workbench/Editor/TowerMatchEffectsSetup.cs` | 띠 제거, D 표시 생성·`_workbench` 연결, 씬에 공용 띠 자동 추가 |
| `02.Prefabs/UI/Common/ScreenNotice.prefab`(신규), `TowerMatchEffects.prefab`(재빌드) | 빌더 생성 |
| `00.Scenes/Jihun/JH04TestScene.unity` | 공용 띠 추가·연결 저장(`JH04Offline`의 미저장 변경은 건드리지 않으려고 추가 열기로 작업) |
| [ScreenNotice 사용법](../../handover/김지훈/2026-10-26/ScreenNotice-사용법.md)(신규) | 팀원용 |

### 검증(추가분)
- 컴파일 오류 0. 1인 호스트(`JH04Offline` → 로비 → `JH04TestScene`), 콘솔 오류 0.
- B: 7칸 채움 → 대기열 마지막 순서가 "장착 (7/8)" → 금색 "마지막 부품 — 1칸 남음"(층 완성 "2층·3층 수리 완료 — 보상 해금"도 금색). 렌더 확인.
- C: 서버에서 `RpcRecipeCompleted(3)` 호출 → 호스트 클라이언트 수신 → "렌즈 모듈 완성"+렌즈 아이콘, 게이지 없이 문구 가운데. 렌더 확인. 실제 제작(`TryCraft`) 경로는 미실행.
- D: 서버 `NotifyCarried(1, 2)` → 본인 TargetRpc → "들고 있는 부품: 기어 장치"+기어 아이콘. 렌더 확인. 실제 꺼내기·되돌리기·소환 시 소멸 경로는 미실행.
- A: 강제 소환 후 피해 8회(1000→200) → "등대가 무너지고 있다" 1회·붉은 테두리. 렌더 확인.
- 미실행: M2, `MainGame` 배치(공용 띠와 연출 프리팹 둘 다 필요 — `Use Match Effects In Open Scene` 한 번이면 둘 다 놓임).

## 같은 대화의 다른 확인 (코드 무변경)

- **기어 모듈을 꺼낼 수 없던 이유:** 1인 로비 판이라 본인이 마피아이고, 소환(또는 판 종료)으로 공개된 상태였다. `ModuleInteractionHandler.HandleTake` → `CarryCheck` = `TowerMatchFlow.CanCarry`가 공개된 마피아를 거절(2026-10-20 "소환 시 마피아 인벤토리 닫힘"의 서버 강제). 당시 작업대 보관 레시피 2(기어)·3 각 1개, 들고 있는 사람 없음, `RevealedMafiaId=1`. 시험하려면 소환 전에 꺼내거나 단일 씬 방식에서 시민으로 시작.

## 남은 일 (최종)

1. **`MainGame` 배치**(사용자 확인 후): develop 최신을 받고 `MainGame`을 열어 `Lighthouse/Tower/Use Match Effects In Open Scene` 한 번 → 연출 프리팹·공용 띠가 함께 놓이고 `_match`·`_workbench` 연결 → 저장.
2. **M2(호스트+원격 1)**: 원격에서 ①~⑤·A~D가 한 번씩 나오는지, 호스트에서 두 번 나오지 않는지.
3. **실제 경로 확인**: 장착 요청(`InstallModule`)·제작(`TryCraft`)·꺼내기/되돌리기/소환 시 소멸로 연출이 나오는지(이번엔 서버 신호를 직접 호출).
4. **모양 판단(사용자)**: 띠 위치(위에서 206px), 밝은 하늘에서 옅은 금색 테두리, D 표시 위치(왼쪽 아래)가 최명기 HUD와 겹치는지.
5. **공유**: [ScreenNotice 사용법](../../handover/김지훈/2026-10-26/ScreenNotice-사용법.md)을 커밋 뒤 팀에 알림(강민서 날짜 전환·박태욱 무고 공개 알림에 쓸 수 있음, 사용 여부는 각 담당 결정).
6. **결정 대기 — 마피아 자원 빼내기**: 서버 판정(`HandleWithdraw` 마피아 확인, `WorkbenchNet.Withdraw`)은 있으나 본편 작업대 화면 버튼이 없고 시험 패널에서만 가능. 서버가 단계(수리 중)를 검사하지 않음, 인출 기록 없음, 한도 미정(W08). 2026-10-27 선반 방식 폐기로 남은 선택은 화면 버튼 추가 여부와 인출 공개 방식(E, R19)이다.
7. **보류된 연출**: E(인출 공개 — R19 결정 필요), F(종료 요약 — 최명기 협의), G(보상 표시 — 최명기 계약).
8. ~~커밋 범위 정리~~ 2026-10-27 커밋·푸시 완료. `ProjectSettings.asset`·`Assets/Resources.meta`는 이번 작업이 아니라 제외(미커밋 유지). PR은 별도 요청 시.

## 투입·제작·장착 연출 제안 (2026-10-27, 코드 무변경)

- 사용자 결정: 선반 방식 폐기, 화면 방식으로 다시 제안. 작업대·모듈·등대 UI 전체 김지훈 담당.
- 제안 내용(미승인): 투입 — 넣기 버튼→재료 줄 아이콘 비행·숫자 올림·줄 완성 도장·전체 바 차오름·제작 가능 맥박, 다른 선원 투입도 화면을 보고 있으면 같은 연출(이름 없음). 제작 — 성공 결과 수신 뒤 재료 줄 흡수→모듈 아이콘 생성→보관함 칸 이동, 작업대 모델 파티클·소리(`RpcRecipeCompleted`). 장착 — 장착 화면 칸 끼움·층 스윕, 등대 칸 모델 내려앉음·층 창문 불·등대 밝기 단계(`SlotInstalled`).
- 결정 필요: 화면을 보는 중 자원 감소(마피아 인출)를 표시할지(E·R19), 화면 방식 인출 버튼.

### 승인 범위 (2026-10-27 같은 날 이어서)
- 투입 연출 4종: 구현됨([작업대-투입연출](../2026-10-27/작업대-투입연출.md)).
- 진행 승인: 제작(본인 흡수·보관함 이동·다른 선원 +1 펄스·작업대 모델 파티클/3D 소리·화면을 연 PC에서 띠 C 생략), 가져가기/되돌려놓기(본인 화면만, D 미끄러짐), 장착(화면 칸 끼움·층 스윕·카운트, 등대 칸 모델 내려앉음). 제안에 있던 층 창문 불·불빛 단계는 이 문서 10행 결정(등대는 마지막에 한 번 켜짐, 창문 모델 없음)과 어긋나 삭제했다(사용자 지적).
- 사용자 결정: **작업대 화면에서 인출(제작 아닌 감소)은 재료 줄을 빨갛게 깜빡인다(E·R19 화면 범위). 인출 버튼은 작업대 화면에 둔다.** 이 둘은 마지막에 한 번에 구현한다.
- 순서·구현 메모: [인수인계](../../handover/김지훈/2026-10-27/작업대-제작운반장착연출-세션인수인계.md).
