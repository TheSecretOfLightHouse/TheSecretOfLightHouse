# 비밀 투표 UI 연출 보강

작성일: 2026-10-28 · 갱신일: 2026-10-29 · 담당: 김지훈 · 상태: 코드 반영, 컴파일 확인, 실행 검증 전

## 배경
사용자가 투표 UI 연출 제안을 요청했다. 제안한 항목 가운데 사용자가 아래만 고르고 나머지는 빼거나 보류했다.

| 제안 | 사용자 결정 |
|---|---|
| HUD 등불 아이콘 | 하지 않음. 지금 투표용지 스프라이트 유지 |
| 확인창 Enter 키 | 추가 |
| 열기 페이드인 | 추가 |
| 후보 선택 강조 | 진행 |
| 확인창 인라인화·차감 깜빡임·빨간 경고 | 구현 전에 설명 요청(보류) |
| 의식 시작 잠금 자물쇠 | 하지 않음 |

성공 자동 닫힘·거절 흔들림, 입력 잠금 제거(v4 4.8 "투표 모습 노출" 대응)는 이번 범위에 들지 않았다.

## 변경
- `Assets/01.Scripts/07.Mafia/Voting/View/VotingScreen.cs`
  - 확인창이 열린 동안 Enter·숫자패드 Enter로 확인한다. ESC 취소는 그대로다.
  - 창을 열 때 `CanvasGroup` 알파를 0→1로 `_fadeInSeconds`(기본 0.12초, unscaled) 동안 올린다. `CanvasGroup`은 `Awake`에서 없으면 붙여 기존 프리팹을 재빌드하지 않아도 된다.
- `Assets/01.Scripts/07.Mafia/Voting/View/VoteCandidateCard.cs`
  - 금색 테두리·"현재 지목" 버튼·체크는 시안에 이미 있었다. 추가한 것은 현재 지목 카드의 번호 배지를 금색(`_currentBadgeColor`)으로 칠하는 것과, 같은 카드가 새로 지목되는 순간의 0.2초 확대(1.06배→1배)다.
  - 카드 기준점이 왼쪽 위라 확대 중 위치를 보정해 가운데에서 커지게 했다. 창이 꺼지면 원래 크기·위치로 되돌린다.
  - 카드를 처음 채울 때나 다른 선원으로 바뀔 때는 확대하지 않는다. 시민은 서버 응답(`Changed`) 뒤, 마피아(가짜 지목)는 확인 즉시 같은 확대가 나온다.

## 검증
- 실행: Unity 스크립트 재컴파일 후 콘솔 오류 0건(Voting 필터 경고 0건).
- 실행: 일반 빌드(`BuildOptions.None`, 씬 `JH04Offline`→`JH07Lobby`→`JH04TestScene`, 출력 `Builds/JH04/TheSecretOfLightHouse.exe`) 성공. 빌드 DLL에 `_fadeInSeconds`·`_currentBadgeColor`가 들어간 것을 확인했다. 기존 창 3개를 닫고 960×540 창 3개를 다시 띄웠다(로그 `Builds/JH04/logs/vote1~3.log`, 시작 시 Exception·error 0).
- 미실행: 3인 창에서 사용자가 직접 하는 확인(창 열기 페이드, 숫자→Enter 확인, 배지 색·확대).

## 남은 일
- M0로 페이드 길이와 확대 크기를 체감 확인한다.
- 확인창 인라인화 설명을 듣고 사용자가 진행 여부를 정한다.
- 이 파일은 `07.Mafia`(박태욱 영역)에 있는 지훈 작성 시안이다. 정식 UI(MK06, 최명기)로 옮길 때 이 연출을 넘긴다.

## 후속: 2차 연출 (2026-10-29)
사용자가 2차 제안 8개 가운데 1·3·4·5·6·7번을 골랐다. 2번(지목 도장)과 8번(효과음)은 고르지 않았다. 입력 잠금(창을 열면 배가 멈춰 다른 선원에게 보이는 문제, v4 4.8)은 이번 범위가 아니다.

| # | 연출 | 구현 |
|---|---|---|
| 1 | 투표용지 차감 | 새로 쓴 용지가 0.25초 동안 -12° 기울고 14px 내려가며 흐려진 뒤 '사용' 그림으로 바뀐다. 남은 횟수가 줄어든 것을 감지해 재생하므로 시민(서버 응답 뒤)·마피아(가짜 지목 즉시) 모두 같다. 창을 열 때는 재생하지 않는다 |
| 3 | 성공 후 자동 닫힘 | `Changed`·마피아 가짜 지목 뒤 `_autoCloseSeconds`(0.6초)에 창을 닫는다. 기다리는 동안 카드 선택을 막는다 |
| 4 | 거절 흔들림 | 남은 기회 없음·서버 거절·잠김일 때 알림 문구와 해당 카드를 0.2초 좌우로 흔든다(8px, 3회). 카드는 기존 확대와 같이 돌 수 있다 |
| 5 | 알림 페이드아웃 | 문구가 `_noticeHoldSeconds`(2.5초) 뒤 0.4초 동안 사라진다 |
| 6 | 마지막 1회 경고 | 남은 기회가 1회면 확인창 줄을 주황(`_lastChanceColor`) "마지막 기회 1회 → 0회"로 바꾼다. HUD 숫자는 1회로 줄어든 뒤 HUD가 다시 보일 때 0.8초 동안 주황으로 두 번 깜빡인다 |
| 7 | 의식 시작 잠금 | 창이 열려 있으면 카드 테두리·이름·초상·버튼을 회색으로 바꾸고 `LockBand`("점화 의식이 시작되어 투표가 잠겼습니다")를 0.15초 페이드인한다. HUD "투표 잠김"은 판 중에 잠겼을 때만 빨갛게 두 번 깜빡인다(이미 잠긴 판에 들어오면 깜빡이지 않음) |

### 변경 파일
- `Assets/01.Scripts/07.Mafia/Voting/View/VotingScreen.cs`: 위 연출 전부. 새 설정 `_autoCloseSeconds`·`_noticeHoldSeconds`·`_lastChanceColor`·`_lockBand`(비어 있으면 띠만 생략).
- `Assets/01.Scripts/07.Mafia/Voting/View/VoteCandidateCard.cs`: `Shake()`, 잠김 회색 처리(`Show`에 `isLocked` 추가), 흔들림 계산 `ShakeOffset`을 화면과 공유.
- `Assets/01.Scripts/07.Mafia/Voting/Editor/VotingScreenSketchSetup.cs`: 카드 위에 `LockBand`(어두운 띠 + 문구)를 만들고 연결한다.
- `Assets/02.Prefabs/UI/Voting/VotingScreen_Sketch.prefab`: 빌더 메뉴로 재생성.

### 검증
- 실행: `dotnet build` Runtime·Voting.Editor 오류 0, 바뀐 파일 새 경고 없음. Unity 강제 새로고침·컴파일 후 콘솔 오류 0. `Lighthouse/Voting/Build Sketch Screen Prefab` 성공 로그. 프리팹 YAML에서 `LockBand` 생성과 `_lockBand`·새 설정값 연결을 확인했다.
- 미실행: 플레이 확인(용지 떨어짐·자동 닫힘 길이·흔들림 세기·주황/빨강 깜빡임·잠김 띠 위치와 글자 폭), M2.

### 남은 일
- M0/M2에서 연출 길이와 세기를 체감으로 확인하고 조정한다.
- 입력 잠금(투표 중 배 정지) 처리 여부를 결정한다.
