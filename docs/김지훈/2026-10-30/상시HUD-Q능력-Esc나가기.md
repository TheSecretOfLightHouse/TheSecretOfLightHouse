# 상시 HUD 키 안내에 Q 능력 사용 추가, Esc를 맨 위 "나가기"로

작성일: 2026-10-30

## 요청
상시 인게임 HUD 오른쪽 위 키 안내에 Q(능력 사용)를 추가하고, Esc를 맨 위로 올려 라벨을 "나가기"로 바꾼다.

## 수행
- 이미지 생성(`Assets/03.Images/InGameHud/`, Python PIL)
  - `hud_key_q.png`: `hud_key_e.png`에서 글자를 지우고 Arial Bold로 Q를 그림(119x116, 같은 틀).
  - `hud_label_exit.png`("나가기", 144x61), `hud_label_ability.png`("능력 사용", 201x61): 맑은 고딕 Bold 크림색 + 옅은 번짐. 기존 라벨(손글씨풍)과 글꼴이 달라 임시본.
  - `.meta`는 Unity가 가져올 때 생성, 스프라이트 설정은 `HudSketchLayout.ImportSprites`가 적용.
- `InGameHudSetup.cs`: 키 행을 `AddKeys`로 분리. 순서 Esc(나가기) → M(지도) → Tab(인벤토리) → Q(능력 사용). 행 이름 `Menu`→`Exit`, 신규 `Ability`.
  - 신규 메뉴 `Lighthouse/UI/Update In-Game HUD Keys`: 프리팹을 다시 빌드하지 않고 키 행만 지우고 다시 깔아 손수정분(카드 판 등)을 보존.
- `InGameHud.cs`: 머리 주석만 갱신.
- Q는 `InputActions`의 `Ability` 액션(`<Keyboard>/q`)으로 네 직업 공통.

## 검증
- VS Code 진단 0, 생성 이미지 육안 확인.
- 다른 세션이 Unity MCP 사용 중이라 **Unity 컴파일·메뉴 실행·프리팹 갱신·Play 미실행**. `InGameHud.prefab`은 아직 이전 배치.

## 남은 일
- Unity에서 `Lighthouse/UI/Update In-Game HUD Keys` 실행 → 프리팹 저장 확인.
- Play로 4번째 줄(Q)이 아래 HUD와 겹치지 않는지 확인.
- 라벨 글꼴을 기존 손글씨풍과 맞출 이미지가 생기면 교체.

## 후속 (2026-10-30): 아래 Esc 겹침 제거, 키 안내를 오른쪽 위 구석으로

- 사용자 확인: 메뉴 실행 후 3번째 줄에 `Esc 메뉴`가 Tab 줄 위에 겹쳐 보임.
- 원인: HUD 프리팹에는 4줄이 정상으로 들어 있었음. 겹친 것은 `SessionScreen.prefab`의 예비 나가기 버튼(`LeaveButton`: `hud_key_esc`+`hud_label_menu`)으로, HUD에 `SessionLeaveButton`이 없으면 표시되며 예전 HUD 3번째 줄 Esc 자리에 놓여 있었음.
- `InGameHudSetup.AddKeys`: Esc(나가기) 키캡에 `Button`+`SessionLeaveButton`을 붙여 HUD Esc를 경기 나가기 컨트롤로 등록 → 세션 예비 버튼 자동 숨김. `Key`가 키캡 Image를 반환하도록 변경.
- `HudSketchLayout.KeyRow`: 첫 줄 top 160 → 24(오른쪽 여백과 같은 간격). 등대 창 Esc도 같은 값을 쓰므로 함께 올라감.
- 주의: 최후 전투 띠의 체력 쪽(스케치 x 1376..1566, y 14..146)이 앞쪽 줄 아래에 겹칠 수 있음. 필요하면 최후 전투 중에만 키 안내를 내리는 처리를 추가.
- `InGameHud.cs`는 다른 세션이 TMP 전환 중이라 손대지 않음.
- 검증: 코드 정적 검토만. Unity 컴파일·메뉴 재실행·Play 미실행.
- 남은 일: `Update In-Game HUD Keys` → `Update Sketch Screen Layout` 재실행, Play로 겹침·위치 확인.

## 후속 (2026-10-31): 키 안내·E 안내 글자를 전부 TMP로 통일

- 요청: HUD 4줄 라벨이 이미지(원래 손글씨풍 지도·소지품 vs 생성한 나가기·능력 사용)로 섞여 보임 → 4개 모두 TMP. 작업대·등대·유령선 E 안내 글자도 같은 TMP로.
- 공용 스타일(`HudSketchLayout.Caption`): `NotoSansKR-Bold SDF` + `NotoSansKR-Bold SDF Outline` 머티리얼, 크림색, 한 줄 왼쪽 정렬·세로 가운데. HUD 직업 이름(`RoleName`)과 같은 폰트·머티리얼.
  - 키 줄: 스케치 22px(HUD 캔버스 25.3), 캡션 상자를 키 높이로 맞춰 키 가운데에 글자.
  - E 안내(`HudSketchLayout.BuildPrompt`): 스케치 28px, `hud_key_e` + 캡션, 캡션 폭을 글자에 맞추고 `Prompt` 폭을 키+캡션으로 맞춰 가운데 유지. 단위 배율로 1920 캔버스(유령선 장부)도 1672 캔버스(작업대·등대)와 같은 화면 크기(유령선 쪽은 이전보다 약 15% 커짐).
  - 문구: 나가기 / 지도 / 소지품 / 능력 사용, 작업대 사용 / 등대 사용 / 유령선 확인. 등대 창 Esc 캡션도 TMP "나가기".
- `Lighthouse.Workbench.Editor.asmdef`에 `Unity.TextMeshPro` 추가. 각 빌더(InGameHud·TowerScreen·WorkbenchScreen·GhostShipLedger)가 같은 함수를 쓰도록 수정. 미사용 `HudPicture` 제거.
- 통합 메뉴 `Lighthouse/UI/Update Key Hints And Prompts`(HUD 키 줄 + 등대 창 배치·Esc·E 안내 + 작업대·유령선 E 안내)를 MCP로 실행해 프리팹 4개 갱신.
- 문제와 수정: 꺼진 `Prompt`(작업대·등대)에서 TMP 폭이 11·14로 잘못 측정됨 → 측정하는 동안만 켜고, 폰트에 글자를 먼저 추가하도록 수정 후 재실행(110·136·156).
- 부수 변경: `HudSketchLayout.ImportSprites`가 `hud_label_check_ghostship.png.meta` 가져오기 설정을 HUD 폴더 기준으로 바꿈(이미지는 이제 미사용).
- 검증: Unity 컴파일 error 0, 프리팹 값 확인(텍스트·크기·`SessionLeaveButton`). 빌드·Play 화면 확인 미실행.
- 이미지 라벨(`hud_label_exit`·`hud_label_ability`·`hud_label_map`·`hud_label_inventory`·`hud_label_use_*`·`hud_label_check_ghostship`)은 참조가 없어졌지만 파일은 남김.

## 후속 (2026-10-31): Q 줄은 배 탑승 중에만 표시

- 요청: Q 능력은 배에 탔을 때만 쓰는 기능이므로, 오른쪽 위 Q 줄을 배에 탔을 때만 같은 자리에 표시.
- `InGameHud.cs`: `_boatOnlyHints`(GameObject[]) 추가, 매 프레임 `PlayerController.IsOnBoat`로 켜고 끔(값이 바뀔 때만 SetActive). 시작 시 숨김.
- `InGameHudSetup`: `SetBoatOnlyHints`가 `AbilityKey`·`AbilityLabel`을 연결(Build·UpdateKeys 둘 다). `UpdateKeys`를 MCP로 실행해 프리팹 갱신, 연결 확인.
- 확인한 사실: Q 입력 처리(`PlayerController.ProcessActionRequests`)는 탑승을 검사하지 않음. 액티브 능력 중 탑승 검사는 `GuardShieldAbility`만 있음(정비공·항해사·채집가는 없음). 육지에서 Q를 누르면 일부 직업 능력이 실행될 수 있음 — 최명기(Player) 영역이라 수정하지 않음.
- 검증: Unity 컴파일 error 0, 프리팹 연결 확인. Play 미실행.
