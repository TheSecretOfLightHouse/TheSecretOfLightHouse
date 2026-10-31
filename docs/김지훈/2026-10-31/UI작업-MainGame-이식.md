# PR #126 이후 UI 작업을 MainGame에 이식

작성일: 2026-10-31 · 담당: 김지훈(UI)

## 요청 (사용자, 2026-10-31)
마지막 PR(#126) 이후 UI 작업(Alt 토글, 유령선 정보창, 좌우 여백 대칭)을 메인 씬에 반영한다. 같은 기간의 본인 역할 카드·Alt 역할 표식도 UI 작업이라 함께 옮겼다.

## 대상과 처리
| 작업 | 메인 반영 방식 |
|---|---|
| Alt 토글 접기·Alt 패널 역할 표식 (`HudReveal`) | MainGame 루트에 `HudReveal` 오브젝트 추가 |
| 본인 역할 공개 카드 (`OwnRoleBanner`, 정렬 74) | MainGame 루트에 `OwnRoleBanner` 캔버스 오브젝트 추가 |
| 좌우 여백 대칭 (`HudEdgeAlign` + FinalBattleHud 위치) | MainGame 루트에 `HudEdgeAlign` 추가, FinalBattleHud 인스턴스 오버라이드로 수리도 x −57.42, 내구도 x +66.60 |
| 유령선 정보창 | 씬 작업 없음. `GhostShipLedger.prefab`만 바뀌었고 MainGame의 `GhostShip` 프리팹 인스턴스가 이를 쓴다 |
| 최후 전투 UI 소환 연출 뒤 표시 | 씬 작업 없음. 코드(`TowerMatchNet`·`FinalBattleHud`·`TowerMatchEffects`) 변경이라 메인에도 적용 |

## 수행
- JH04TestScene과 MainGame을 에디터에 추가로 열고(열려 있던 JH04Offline의 미저장 변경은 건드리지 않음), JH04의 세 오브젝트를 복제해 MainGame으로 옮겼다.
- 복제본의 씬 참조를 같은 경로의 MainGame 오브젝트로 다시 연결했다: HudReveal 16개, HudEdgeAlign 3개(InGameHud·VotingScreen_Sketch·IdentityActionScreen_Sketch 아래, 모두 같은 경로가 존재). 남은 다른 씬 참조 0개 확인.
- FinalBattleHud 이동값은 JH04와 같은 규칙(수리도 왼쪽 끝 = 패널 왼쪽 52.82, 내구도 오른쪽 끝 = 오른쪽에서 52.82)으로 다시 계산했고 JH04와 같은 값이 나왔다.
- 에디터 API로 MainGame 저장 후 닫았다. 프리팹 자체는 수정하지 않았다.
- 씬을 열 때 URP가 `GhostShip` 인스턴스 안 조명에 `UniversalAdditionalLightData`를 자동으로 붙여 함께 저장됐다. 동작 영향 없는 자동 추가라 그대로 두었다.

## 검증
- 실행함: MainGame에 세 컴포넌트 GUID 각 1개, FinalBattleHud x 오버라이드 기록, 다른 씬 참조 0개.
- 실행함: Build Settings를 Main 흐름(MainOffline → MainLobby → MainGame만 켬)으로 바꾸고 `Builds/MainUi` 빌드 성공(16:27), 창 4개(960x540, `client0~3.log`) 실행, 시작 직후 로그 Exception·Error 0.
- 실행 안 함: 빌드 플레이에서 Alt 토글, 역할 카드, HUD 위치, 최후 전투 UI 시점 확인.

## 남은 일
- 빌드 창 4개로 방 만들기·참가 후 플레이 확인(사용자). Build Settings는 이제 Main 흐름이며, JH04 흐름 빌드가 다시 필요하면 JH04Offline·JH07Lobby·JH04TestScene을 켠다.
- Z·X 단축키는 정체 확인 버튼을 대신 누르는 방식이다. 박태욱에게 공유.

## 추가 수정: 일반 빌드에서 소환 연출이 안 뜨고 입력만 막힘
- 증상(사용자, Main 빌드): 개발자 OnGUI "소환 강제" 뒤 시네머신 연출이 안 뜨고 입력만 막힌다.
- 근거: `client0~3.log` 모두 `SummonCinematicView.CaptureScene`에서 NullReferenceException 반복. 같은 로그에 `TowerTrialAutomation` 스크립트 누락 경고.
- 원인: `TowerTrialAutomation`은 `#if UNITY_EDITOR || DEVELOPMENT_BUILD`라 일반 빌드에서 빠지고, MainGame의 해당 컴포넌트가 누락 스크립트로 남는다. `CaptureScene`이 씬 전체 `Behaviour`를 모을 때 이 컴포넌트가 null로 들어와 `behaviour.transform`에서 예외가 난다. `Present`의 try/catch가 이를 연출 생략으로 처리해, 연출은 건너뛰고 입력 잠금만 연출 시간 동안 남았다. 에디터에는 스크립트가 있어 재현되지 않는다.
- 수정: `Assets/01.Scripts/04.Lighthouse/Tower/View/SummonCinematicView.cs` `CaptureScene`에서 null 컴포넌트를 건너뛴다.
- 검증: 컴파일 error 0, `Builds/MainUi` 재빌드 성공, 창 4개 다시 실행. 빌드에서 소환 연출 재생은 아직 확인 전(사용자).
- JH04TestScene의 멈춤은 별개다. JH04에는 섬 연결(`BindIsland`)이 없어 연출 리그가 없고, 대기 시간 동안 입력만 막힌다(이 세션 앞부분 분석, 방법 A/B 결정 대기).
