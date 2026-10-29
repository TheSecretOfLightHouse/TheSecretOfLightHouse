# 정체 확인 UI 점검 — 디버그 문구 노출·소환 권리 만료 처리

작성일: 2026-10-29 · 담당: 김지훈 · 상태: 코드 반영, dotnet 컴파일 오류 0, Unity 컴파일·플레이 검증 전

## 요청
투표 UI에 `CheckFailed`라는 디버그 문구가 인게임 화면에 그대로 뜬다. 다른 오류가 없는지, 정체 확인이 조건대로 되는지, 확인 후 소환하지 않았을 때 처리가 맞는지 기능까지 확인해 달라.

## 원인
- `CheckFailed`는 MainGame에 배치된 판 진행 시험 패널 `99.Dev/Tower/TowerTrialPanel`의 마피아 칸 `최근: {response.Result.ToString()}`에서 나왔다. 실제 UI(`IdentityActionScreen`)는 이미 한국어 문구로 바꿔 보여 준다.
- 같은 패널의 투표 거절 기본 문구도 `({response.Result})`로 enum 이름을 띄웠다.

## 기능 대조 (win-vote-decisions 2절·7절)
- 성공 판정: 서버가 요청을 처리하는 순간 마피아를 지목 중인 표 수 ≥ `IdentitySettings._requiredVoteCount`(2). 성공도 확인 횟수 1회를 쓴다. 맞음.
- 실패: 아직 공개되지 않은 시민 1명을 무작위 공개(중복 없음), `_revealedCitizens` SyncList로 전원에게 전달. 맞음.
- 10초 권리 중 재확인은 거절, 만료 뒤 남은 횟수가 있으면 재확인 가능, 마지막 확인권 성공도 10초 사용 가능, 난파 중 둘 다 불가, 의식 중 가능. 서버 규칙(`IdentityCheckService`)은 결정과 맞음.
- **결함(수정함)**: 권리가 쓰이지 않고 만료되어도 서버가 상태를 다시 보내지 않는다. 클라이언트는 마지막 상태(`CanSummon=true`, `CanCheck=false`)에 멈춰 "소환 가능 0초"가 남고, 소환 버튼은 켜진 채, 정체 확인 버튼은 계속 꺼져 재확인을 할 수 없었다(소환을 눌러 거절 응답을 받아야 풀림).

## 변경
- `07.Mafia/IdentityCheck/View/IdentityActionScreen.cs`: 클라이언트 서버 시각(`NetworkTime.time`)이 만료 시각을 넘으면 권리를 끝난 것으로 보고 소환 버튼·남은 초 표시를 내린다. 서버가 아직 권리를 보고하는 동안 0.5초 간격으로 `RequestOwnIdentityState`를 다시 요청해 확인 버튼을 되살린다. 소환 확인창이 열려 있었다면 "소환 권리 시간이 지났습니다"로 닫는다.
- `99.Dev/Tower/TowerTrialPanel.cs`: 정체 확인 결과를 한국어 문구로 표시(`IdentityText`), 투표 거절 기본 문구에서 enum 이름 제거.
- 07.Mafia는 박태욱 담당 폴더지만 `IdentityActionScreen`은 김지훈이 만든 UI라 그 안에서만 고쳤다. 서버(`VotingNetworkController`)는 손대지 않았다.

## 남은 문제 (수정 안 함, 결정·담당 협의 필요)
- 실패로 공개된 무고 시민이 실제 UI에는 나오지 않는다. 시민 화면에서 보이는 곳은 시험 패널(`플레이어 {actorId}는 시민`, 닉네임 아님)뿐이다. 마피아 알림은 "모두에게 공개되었습니다"라고 말한다. 투표 카드 표시나 전체 공지 등 표시 방식 결정 필요.
- 세션 자동 초기화(`VotingNetworkController.HandleMatchStarted`)는 설정값 2회·2표를 그대로 쓴다. 시민이 1명(2인 시험)이면 `IdentityCheckService` 생성자가 예외를 던지고, `MatchStarted?.Invoke` 안에서 나는 예외라 뒤에 등록된 판 시작 처리까지 멈출 수 있다. `TowerTrialRound`처럼 시민 수로 줄이는 처리가 필요(박태욱 영역).
- 서버 쪽에서 만료 시 상태를 밀어 주면 클라이언트 재요청이 필요 없다(박태욱 영역, 선택).

## 검증
- 실행: `dotnet build Lighthouse.Runtime.csproj` 오류 0(두 파일 포함 확인). Mirror 위버는 돌지 않은 빌드.
- 미실행: Unity MCP ping 무응답으로 에디터 컴파일·콘솔 확인 못 함. 마피아로 확인 성공 → 10초 대기 → 확인 버튼 복귀, 실패 시 시민 공개 플레이 확인 전.

## 후속 점검: 소환 실패·소환 후 버튼 (같은 날)

### 요청
인게임 UI로 확인 후 소환을 눌렀는데 소환이 안 됐을 때 다시 확인할 수 있는지, 소환 후 확인 버튼이 비활성으로 보이고 실제로 막히는지, 다른 문제가 없는지 점검.

### 시나리오별 결과 (코드 대조)
| 상황 | 서버 응답·상태 | UI 동작 |
|---|---|---|
| 확인 성공 → 소환 누름 → 이미 10초 지남 | `SummonRightExpired` + 새 상태(권리 없음, 남은 횟수 있으면 `CanCheck`) | "소환 권리 시간이 지났습니다" 알림, 확인 버튼 다시 켜짐 → 재확인 가능 |
| 확인 성공 → 10초 동안 아무것도 안 함 | 서버가 상태를 보내지 않음 | 지난 수정으로 0.5초마다 상태 재요청 → 확인 버튼 복귀 |
| 마지막 확인권 성공 후 소환 실패 | 남은 0회 → `CanCheck` false | 두 버튼 회색, 재확인 불가(결정 2절대로) |
| 권리 중 난파 → 소환 시도 | `Downed`, 권리는 유지 | "난파 중에는 할 수 없습니다". 10초 안에 회복하면 소환 가능 |
| 소환 승인 | `SummonApproved`, 이후 상태는 `AlreadySummoned`로 막힘(`CanCheck`·`CanSummon` false) | 두 버튼 `interactable=false` + 회색 틴트. 클릭 불가 |

### 고친 것 (`IdentityActionScreen.cs`)
- 소환 후 상태 문구가 계속 "정체 확인 N회 남음"이라 아직 확인할 수 있는 것처럼 보였다 → `SummonApproved`/`AlreadySummoned` 응답을 기억해 "크툴루 소환 완료"로 표시.
- 권리가 남았는데 난파로 소환 확인창이 닫힐 때도 "소환 권리 시간이 지났습니다"가 떴다 → 만료와 그 밖의 차단을 나눠 "지금은 소환할 수 없습니다"로 표시.
- 요청을 보낸 뒤 응답이 오지 않으면 `_awaitedAction`이 남아 두 버튼이 눌러도 반응하지 않았다 → 5초 뒤 대기 해제.
- 다른 세션이 같은 파일에 넣은 실패 배너 연동(`CheckFailed`는 알림창 없이 `InnocentRevealBanner`로)은 그대로 보존했다.

### 확인만 한 것
- 버튼은 SpriteSwap 전환이고 비활성 스프라이트가 없어, 비활성 표시는 코드의 `_unavailableColor` 틴트(회색 반투명)로만 이루어진다. 동작은 정상.
- 확인 버튼은 10초 권리 중에도 회색이다(권리 중 재확인 금지 규칙).

### 검증
- 실행: `dotnet build Lighthouse.Runtime.csproj` 오류 0.
- 미실행: Unity 컴파일·플레이(마피아로 확인 성공 → 10초 방치/소환 실패/소환 승인 시 버튼 상태 확인).

## 후속: 소환 권리 연출 (같은 날)

### 요청
정체 확인 후 크툴루를 소환할 수 있는 상태라는 게 직관적이지 않다 → 제안한 연출(성공 알림을 막지 않는 띠로, 버튼 빛·맥박·시간 막대, 마지막 3초 붉게+숫자, 끝날 때 문구) 진행 승인.

### 결정
- 성공 알림창이 입력을 막는 동안에도 10초가 줄어 권리를 깎으므로, `CheckSucceeded`·`SummonApproved`는 알림창을 띄우지 않고 연출로 대체. 거절 사유만 알림창에 남김.
- 소환 버튼이 가로로 길어 원형 테두리 대신 버튼 바로 아래 줄어드는 시간 막대로 바꿈.
- 연출은 마피아 개인 상태(`HasOwnIdentityState`)로만 켜지므로 시민 화면에는 나오지 않는다(결정 7절 확인 성공은 마피아만).
- 그림 없이 빛(9-slice 부드러운 사각형)과 화면 가장자리 비네트를 런타임 텍스처로 만든다. 효과음은 `_grantedClip` 빈칸(본인 클라이언트 2D 재생).

### 변경
- 새 `07.Mafia/IdentityCheck/View/SummonRightCue.cs`: 권리 부여(새 만료 시각) 때 청록 비네트 0.9초 + "바다가 응답했다 — 지금 크툴루를 소환할 수 있다" 띠 1.8초. 권리 중 버튼 빛·맥박(1.2Hz, 난파로 못 누르면 멈춤), 시간 막대. 마지막 3초 붉은색·3Hz·버튼 왼쪽 큰 숫자. 만료 시 "소환 권리가 사라졌다", 승인 시 "크툴루가 깨어났다 — 최후 전투 시작".
- `IdentityActionScreen.cs`: 성공·승인 알림창 제거, 알림 제목은 "요청 거절" 하나로.
- `IdentityActionScreenSketchSetup.cs`: `BuildSummonCue` 추가 — Buttons 안에 `SummonGlow`(소환 버튼 뒤)·`SummonTimer/Fill`·`SummonSeconds`, 루트에 `SummonCue`(컴포넌트+AudioSource)/`Vignette`·`Line`. 소환 버튼 피벗을 가운데로 바꿈(맥박용, 위치 유지).
- 프리팹 `IdentityActionScreen_Sketch.prefab`을 메뉴로 재빌드(다른 세션의 실패 공개 배너도 같은 빌더라 함께 재생성).

### 검증
- 실행: dotnet — 새 파일을 임시로 넣은 `Lighthouse.Runtime`과 `Lighthouse.Mafia.Voting.Editor` 오류 0(csproj 원복). Unity 컴파일 후 콘솔 error 0, 메뉴 재빌드 로그 `Built ...` 확인, 프리팹 계층 41개 객체·배치 순서 확인.
- 미실행: Play로 연출 확인(마피아로 확인 성공 → 빛·막대·3초·만료/승인 문구), 해상도별 띠 위치·비네트 강도 확인.


## 커밋 분리 점검 (2026-10-29)
- 박태욱 영역의 이번 UI 변경은 사용자 포함 허용. 실제 사용 이미지 ir_*.png 6장과 meta를 포함하고 output/ 시안은 제외. 런타임 FindAnyObjectByType 사용은 코딩 컨벤션과 충돌하며 미해결로 남김.
- 이번 실행: 현재 diff·에셋/meta 짝·포함/제외 범위 점검, Unity MCP 콘솔 오류 0건 확인. 기존 컴파일·빌드·렌더 기록과 구분하며 이번에는 빌드·다인 플레이를 실행하지 않음.
- 씬·프리팹의 공백 경고는 Unity 직렬화 출력에 한정됨. YAML 직접 수정 없이 보존.
