# 정체 확인·크툴루 소환 UI 시안 적용

작성자: 김지훈
작성일: 2026-10-23
상태: 구현·프리팹 생성·`MainGame`·`JH04TestScene` 연결 완료, 플레이 미검증, 미커밋

## 요청과 결정

- 사용자가 버튼 시안 1장(정체 확인·크툴루 소환, 흰색·노란색 각 2개)을 주고 마피아의 정체 확인·크툴루 소환 UI에 적용하라고 했다.
- 사용자 결정:
  - 클릭으로만 조작한다.
  - 투표 지목 확인창처럼 확인창을 거친 뒤 요청한다.
  - 결과는 알림창으로 띄운다.
  - 박태욱 영역(`07.Mafia/`)에는 새 파일만 추가하는 방식으로 박태욱과 협의됐다(사용자 확인).
- 흰색은 평소 모양, 노란색은 마우스를 올리거나 누를 때 모양(SpriteSwap)으로 쓴다. 지금 누를 수 없는 버튼은 회색으로 흐리게 한다(`CanCheck`·`CanSummon`).
- 플레이 중 커서는 `PlayerController`가 `Confined`·보이기로 두므로 클릭할 수 있다.
- 시안에 없는 값은 실시간 텍스트로 넣었다:
  - 버튼 위 상태줄: 남은 확인 횟수, 소환 권리 남은 초
  - 확인창: 남은 확인 횟수 또는 소환 남은 초
- 투표 확인창의 확정 버튼에 "지목 확정"이 그려져 있어서, 글자 영역을 좌우 바깥 열로 보간해 지운 빈 버튼(`id_button_accept(_hover)`)을 만들고 "확인하기"·"소환하기"·"확인"을 실시간 텍스트로 올렸다.
- 문구 근거: [승패·투표 결정 2절](../../design/win-vote-decisions.md) — 성공하면 10초 소환 권리, 실패하면 무고한 시민 1명 공개, 난파 중 불가. 수리도 100% 소환 경고는 마피아 화면이 등대에 의존하게 돼서 넣지 않았다(시험 패널에는 남아 있음).
- 알림창은 이 화면이 보낸 요청의 다음 응답만 띄운다. 서버가 보내는 상태 갱신(`StateRetrieved`)은 무시한다. 확인창을 연 사이 확인권이 사라지거나 소환 권리가 만료되면 확인창을 닫고 사유를 알린다.

## 변경 파일

- 새 파일:
  - `Assets/01.Scripts/07.Mafia/IdentityCheck/View/IdentityActionScreen.cs`(+meta): 버튼·확인창·알림창. 투표 컨트롤러의 공개 API만 쓴다.
  - `Assets/01.Scripts/07.Mafia/Voting/Editor/IdentityActionScreenSketchSetup.cs`(+meta): 기존 `Lighthouse.Mafia.Voting.Editor` asmdef를 쓰고 새 asmdef는 만들지 않았다. 메뉴 `Lighthouse/Voting/Build Identity Action Screen Prefab`, `Use Identity Action Screen In Open Scene`.
  - 스프라이트 `Assets/03.Images/IdentityUiSketch/` 6장: 시안에서 자른 `id_check(_hover)`, `id_summon(_hover)`과 글자를 지운 `id_button_accept(_hover)`. 원본 시안은 `output/design/identity-ui/`(미추적).
  - 프리팹 `Assets/02.Prefabs/UI/Voting/IdentityActionScreen_Sketch.prefab`: 캔버스 순서 76(투표 75 위), 투표 버튼 바로 위 오른쪽 아래.
- 씬: `Assets/00.Scenes/Main/MainGame.unity`, `Assets/00.Scenes/Jihun/JH04TestScene.unity`에 프리팹을 넣고 `Voting`에 연결해 저장했다.

## 검증

- 컴파일 오류 0(기존 `BoatController` 경고 1).
- 빌더 실행, 두 씬 연결·저장.
- 임시 씬에서 1920×1080으로 렌더링해 확인했다: [버튼](images/identity-ui-hud.png), [확인창](images/identity-ui-confirm.png), [알림창](images/identity-ui-notice.png).
- 미검증: 플레이 동작 전부. 마피아에게만 정체 확인 상태가 오고, 투표에는 시민 2명 이상이 필요하므로 3인 이상 M2에서 확인해야 한다. 현재 떠 있는 `Builds/Main4` 빌드에는 이 UI가 없다.

## 남은 일

1. 새 UI를 넣어 빌드한 뒤 3~4인으로 확인: 마피아만 버튼이 보이는지, 확인 → 성공 알림 → 소환 버튼 노란 강조·남은 초 → 소환 → 최후 전투, 실패 알림, 난파 중 거절.
2. 시험 패널의 마피아 버튼과 화면이 중복된다(개발자 창은 기본으로 꺼져 있음).
3. 박태욱에게 새 파일 위치와 사용 API를 공유(전달은 사용자).
