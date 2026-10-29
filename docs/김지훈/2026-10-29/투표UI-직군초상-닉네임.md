# 투표 UI 직군 초상화·로비 닉네임 반영

작성일: 2026-10-29 · 담당: 김지훈 · 상태: 코드 반영, dotnet 컴파일 확인, Unity 프리팹 재빌드·실행 검증 전

## 요청
비밀 투표 카드에 캐릭터 이미지가 없고 이름이 `플레이어 01` 같은 임시 문자열로 나온다. 사용자가 준 직군 이미지 4장을 직군별로 띄우고 이름은 로비에서 정한 닉네임으로 보여 달라.

## 조사
- `VotingScreen.GetDisplayName`은 `플레이어 {actorId:00}` 임시 문자열만 반환했다.
- 닉네임은 `LobbyPlayer._nickname` SyncVar에만 있다. `BeginLoading`에서 로비 플레이어를 파괴하므로 게임 씬에는 닉네임이 넘어오지 않았다.
- 별도 직군 enum이나 동기화 상태는 없다. 직군은 플레이어 프리팹으로 나뉘며(2026-10-27 최명기), 각 프리팹 루트에 그 직군의 능력 요청 컴포넌트가 하나만 있다. 프리팹 YAML로 확인: Captain 1=NavigatorRouteRequest, Tech 1=EmergencyRepairRequest, Tanker 1=GuardShieldRequest, Farm 1=GatheringFeverRequest.
- `SpawnGamePlayer`는 아직 `playerPrefab` 하나만 생성한다. 직군 배정 공용 기능이 없어 지금 세션을 그대로 돌리면 모든 카드에 같은 직군(현재 프리팹) 초상화가 나온다.

## 기획 대조
- v4 4.8 허점 표는 직군을 당사자에게만 보내는 비밀 정보로 둔다. 하지만 직군별 플레이어·배 프리팹(2026-10-27)이 이미 스폰 assetId와 외형으로 모든 클라이언트에 직군을 드러낸다. 이번 표시는 클라이언트가 이미 가진 정보만 읽고 네트워크로 새 비밀을 보내지 않는다.
- 직군을 공개로 볼지 비밀로 볼지는 아직 문서로 결정되지 않았다. 비밀로 정하면 프리팹 외형 구분부터 다시 봐야 한다. 이번 요청(사용자 지시)으로 투표 카드에 공개 표시했다.

## 변경
- `01.Network/Connections/Net/NetworkActor.cs`: `_displayName` SyncVar, `DisplayName`(없으면 로비와 같은 `플레이어 {ActorId}`, 캐시해 매 프레임 할당 없음), 서버 전용 `AssignDisplayName`.
- `01.Network/Session/Net/LighthouseNetworkManager.cs`: `BeginLoading`에서 로비 플레이어를 지우기 전에 닉네임을 `_nicknames`에 저장하고, `SpawnGamePlayer`가 스폰 전에 넣는다. `ResetServerSession`에서 비운다. 단일 씬 직접 모드는 로비가 없어 fallback 이름이 나온다.
- `07.Mafia/Voting/View/VotingScreen.cs`: 명단 갱신 때 `NetworkActor`를 저장해 이름(`DisplayName`)과 직군 초상화를 읽는다. 직군 스프라이트 4개 필드 추가.
- `07.Mafia/Voting/View/VoteCandidateCard.cs`: 선택 필드 `_portrait`. 직군을 모르면 숨기고, 확인 문구가 카드 위에 뜬 동안에는 알파 0.18로 물러난다. 필드가 비어 있는 기존 프리팹에서도 동작한다.
- `07.Mafia/Voting/Editor/VotingScreenSketchSetup.cs`: 카드마다 200×200 `Portrait`를 맨 뒤 레이어에 만들고 직군 스프라이트를 화면에 연결한다.
- `Assets/03.Images/VotingUiSketch/vt_job_{navigator,mechanic,guard,gatherer}.png`: 사용자 이미지 1254²→512² 축소. 매핑은 외형으로 판단했다. 흰 선장모=항해사, 고글·렌치=정비공, 꽃 모자=채집가, 갈색 헬멧·빨간 스카프=경비병. `.meta`는 Unity 임포트 때 생성돼야 한다.

## 검증
- 실행: `dotnet build Lighthouse.Runtime.csproj`, `Lighthouse.Mafia.Voting.Editor.csproj` 모두 오류 0. 바뀐 파일에는 새 경고가 없다. Mirror 위버는 돌지 않은 빌드다.
- 미실행: Unity 에디터가 꺼져 있고 MCP 연결이 없다. 이미지 임포트(.meta 생성), `Lighthouse/Voting/Build Sketch Screen Prefab` 재빌드, M0/M2 플레이 확인을 하지 못했다.

## 후속: 초상화 액자 (같은 날)
- 문제: 흉상 이미지 밑면이 수평으로 잘려 카드 위에 떠 보였다(사용자 스크린샷).
- 결정: 직군 그림 4장을 다시 받는 대신 액자 1장을 사용자가 GPT로 생성해 주었다. 흉상을 액자 안쪽 마스크로 자르고 액자 밑변에 붙여, 잘린 밑면이 테두리 아래로 숨게 했다.
- 이미지 처리(스크립트는 세션 scratchpad, PIL):
  - `vt_portrait_frame.png`: 알파 16 미만 노란 테두리 잔여를 0으로 지우고 모양대로 잘라 512²로 줄였다.
  - `vt_portrait_mask.png`: 크림색 테두리 안쪽을 flood fill로 뽑아 2px 줄이고 경계를 부드럽게 했다.
  - `vt_job_*.png`: 원본에서 캐릭터 영역만 잘라(가장자리 잡점 제거) 밑면을 정사각형 바닥에 맞췄다. 같은 파일에 덮어써 GUID를 유지했다.
- 빌더: 카드마다 `PortraitFrame` → `PortraitMask`(Mask, 그래픽 숨김) → `Portrait`(하단 중앙 기준, 액자의 0.92배) 순서로 만든다.
- 검증(실행): Unity 강제 새로고침·컴파일 오류 0(콘솔 오류는 이전 VictoryScreenSetup 메뉴 2건뿐), `Build Sketch Screen Prefab` 성공 로그, 프리팹을 읽어 계층·Mask·흉상 rect(184², pivot 하단)·직군 스프라이트 4개·`_portrait` 연결 확인. PIL 합성 미리보기로 4직군 모두 밑면이 테두리 밑에 숨는 것을 확인했다.
- 미실행: 플레이 화면에서 실제 카드 확인, M2.

## 후속: 확인창 복원 (같은 날)
- 문제: 지목 확인이 별도 창이 아니라 카드 위 문구로 떴다. 사용자는 의도하지 않았다고 했다. 원인은 커밋 `fe05296`(메시지에는 Enter·페이드·강조만 적힘)이 2026-10-28 기록에서 "설명 후 결정 대기"였던 인라인화 묶음까지 함께 넣은 것이다.
- 사용자 결정: 원래 확인창 방식으로 되돌린다. 확인창 왼쪽 아래 선원 그림(`vt_sailor`)은 뺀다.
- 되돌린 것: `fe05296` 이전의 확인창. 보드, 제목, 대상·남은 기회 문구, 회색 경고 문구, 취소·확정 버튼이다. 확인창이 열린 동안 숫자 키와 카드 선택은 막힌다.
- 함께 뺀 것: 인라인 묶음 전체(카드 문구 `_prompt`, 다른 카드 흐리게, 차감될 투표용지 깜빡임, 빨간 경고, 같은 카드 다시 클릭해 확정).
- 유지한 것(10-28 사용자 승인): 확인창 Enter 확정·ESC 취소, 창 열기 페이드인, 현재 지목 배지 금색과 확대.
- `IdentityActionScreenSketchSetup`(마피아 정체 확인 창)의 `vt_sailor`는 요청 범위 밖이라 그대로 두었다.
- 검증(실행): Unity 컴파일 오류 0, 프리팹 재빌드 성공. 프리팹을 읽어 Confirm/Board 자식(Title·Target·Count·Warning·Cancel·Accept, Sailor 없음), 확인창 필드 5개 연결, 시작 시 비활성, Card1에 Prompt 없음을 확인했다. 플레이 확인은 미실행.

## 후속: 4인 빌드 실행 (같은 날)
- 실행: 일반 빌드(`BuildOptions.None`, 씬 `JH04Offline`→`JH07Lobby`→`JH04TestScene`, 출력 `Builds/JH04/TheSecretOfLightHouse.exe`) `Build Finished, Result: Success`. 빌드 DLL에 `AssignDisplayName`·`_navigatorPortrait`·`_confirmTarget`이 있고, 제거한 `_pendingPrompt`는 없다.
- 실행: 960×540 창 4개를 띄웠다(로그 `Builds/JH04/logs/vote1~4.log`, 시작 시 Exception·error 0). `JH04Offline`의 `_startPlayerCount`는 1이라 접속한 전원이 준비하면 시작한다.
- 미실행: 접속·닉네임·투표 화면 확인은 사용자가 창에서 직접 한다.

## 후속: 초상화가 테두리를 덮는 문제 (같은 날)
- 문제: 빌드 창에서 흉상 가장자리가 액자 테두리를 살짝 덮었다(사용자 스크린샷).
- 원인: Unity `Mask`는 마스크 그래픽의 알파가 0보다 조금만 커도 안쪽으로 친다(알파 클립 0.001). 이전 마스크는 경계를 흐리게 처리해서 옅은 알파가 테두리 안쪽까지 번져 있었다.
- 수정:
  - `vt_portrait_mask.png`: 흐림 없이 딱 떨어지는 경계로 바꾸고 테두리 안쪽보다 더 줄였다. 축소도 Nearest로 했다.
  - `vt_portrait_border.png`(새 파일): 액자에서 안쪽을 뺀 테두리 이미지다. 빌더가 `PortraitBorder`로 흉상 위에 한 번 더 그린다. 마스크 경계가 어긋나도 테두리 선은 가려지지 않는다.
- 검증(실행): 4배 확대 합성 미리보기(알파 0 초과=안쪽 규칙)로 정비공·항해사 가장자리를 확인했다. Unity 컴파일 후 프리팹 재빌드, `PortraitFrame` 자식이 `PortraitMask`(Mask)→`PortraitBorder` 순서인 것을 확인했다. 일반 빌드 `Succeeded errors=0`(11:53), 960×540 창 4개를 띄웠고 시작 로그 Exception 0.
- 미실행: 창에서 실제로 보이는 모습 확인(사용자).

## 남은 일
1. (완료) 이미지 임포트·`.meta` 생성·프리팹 재빌드.
2. 로비에서 닉네임을 정하고 2인 이상으로 시작해 카드·HUD·알림 이름을 확인(M2).
3. 직군별 플레이어 배정(SpawnGamePlayer가 프리팹 하나만 생성)은 별도 작업이다. 배정 전에는 모든 카드가 같은 직군으로 나온다.
4. 직군 공개/비밀 결정을 문서에 남길지 정한다.
