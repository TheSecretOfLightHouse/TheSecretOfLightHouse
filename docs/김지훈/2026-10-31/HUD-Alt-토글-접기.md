# 오른쪽 버튼·오른쪽 위 키 안내를 Alt 토글로 접기 (JH04 시험)

작성일: 2026-10-31

## 요청과 결정 (사용자, 2026-10-31)
- 오른쪽 정체 확인·크툴루 소환·투표 버튼과 오른쪽 위 키 안내를 평소에는 숨긴다.
- 접힌 상태에는 **Esc(나가기)와 Alt(안내)** 두 줄만 남긴다. Alt는 **토글**이다.
- 단축키(V 투표, Z 정체 확인, X 소환)는 **펼쳐져 있을 때만** 동작한다.
- 자동 펼침: 소환 권리 10초 동안 고정, 투표 잠김·남은 투표/확인 횟수 변화 시 2초, 로컬 플레이어 첫 등장 시 3초.
- 투표·작업대·등대·인벤토리 등 게임 화면이 열려 있으면(`SessionScreen.IsGameplayOverlayOpen`) Alt를 무시한다.
- 메인 씬은 건드리지 않는다. JH04Offline → JH07Lobby → JH04TestScene 빌드로 확인하고, 메인 이식은 사용자가 나중에 한다.

## 수행
- 신규 `Assets/01.Scripts/09.UI/Hud/HudReveal.cs`
  - 공유 프리팹(`InGameHud`, `VotingScreen_Sketch`, `IdentityActionScreen_Sketch`)은 메인 씬도 쓰므로 수정하지 않았다. 필요한 것은 실행 중에 붙인다.
    - Alt 줄: `hud_key_alt` 이미지와 ExitLabel 복제 TMP("안내" / 펼침 시 "안내 닫기")를 2번째 줄(지도 자리)에 만든다. Esc 키는 `SessionLeaveButton`이 붙어 있어 복제하지 않는다.
    - 지도·소지품·능력 줄은 한 줄 아래로 내려 `RevealHints`(CanvasGroup) 아래로 옮긴다.
    - 투표 버튼 `Hud`, 정체 확인 `Buttons`에 CanvasGroup을 붙여 투명도와 클릭 가능 여부를 조절한다. 두 화면이 `_hud.SetActive`를 매 프레임 직접 정하므로 SetActive와 겹치지 않게 알파로만 숨긴다.
    - 정체 확인·소환 버튼에 투표 버튼의 V 키캡을 복제해 Z·X를 단다.
  - Z·X는 버튼의 `onClick`을 대신 누른다. 정체 확인 화면(`IdentityActionScreen`, 박태욱 CODEOWNERS)은 수정하지 않았다.
  - `HudReveal.AllowsShortcuts`: 컴포넌트가 없는 씬(Main)에서는 true라 기존 동작이 그대로다.
- `VotingScreen.cs`: V로 여는 것만 `HudReveal.AllowsShortcuts`일 때로 제한한다(열린 창을 V/Esc로 닫는 것은 그대로).
- 신규 `Assets/03.Images/InGameHud/hud_key_alt.png`: `hud_key_tab.png`의 글자를 지우고 Arial Bold로 "Alt"를 그렸다(PIL). 가져오기 설정은 `hud_key_tab`과 같게 맞췄다.
- `JH04TestScene.unity`: 루트에 `HudReveal` 오브젝트를 추가하고 참조를 연결했다(MCP, 추가 로드 후 저장). 이 씬에는 작업 전부터 다른 미커밋 변경이 있었고 그대로 함께 저장됐다.
- 빌드 설정: 작업 전부터 JH04Offline·JH07Lobby·JH04TestScene만 켜진 상태(미커밋)였다. 그대로 두었다.

## 검증
- Unity 컴파일 error 0, 씬 참조 연결 확인.
- Windows 빌드 성공(`Build Finished, Result: Success`): `Builds/JH04HudReveal/TheSecretOfLightHouse.exe`, 씬 JH04Offline → JH07Lobby → JH04TestScene.
- 빌드 실행·플레이 확인은 아직 하지 않았다.

## 남은 일
- 빌드로 확인: 접힘(Esc·Alt 두 줄), Alt 토글, 접힌 상태에서 V/Z/X 무반응, 첫 등장 3초 펼침, 투표 후 2초 펼침, 마피아 정체 확인 성공 후 소환 10초 고정, 창이 열린 동안 Alt 무시.
- 메인 이식: MainGame에 `HudReveal` 오브젝트를 같은 참조로 추가(사용자 진행 예정).
- `IdentityActionScreen`을 직접 고치지 않고 버튼 클릭을 대신 누르는 방식이라 박태욱 영역 코드 변경은 없다. 이식 전에 Z·X 동작을 박태욱에게 공유한다.

## 후속 (2026-10-31): 최후 전투 겹침 해소, 수리 보상을 키 안내 줄로

- 사용자 보고(빌드 화면): 최후 전투의 등대 내구도 띠가 오른쪽 위 키 안내와 겹친다. 수리 보상은 별도 버튼(C 키캡 달린 판)이 아니라 키 안내에 한 줄 더 추가하는 뜻이었다.
- `HudReveal.cs`
  - 최후 전투 회피: `_avoidRects`(FinalBattleHud `Hud/Battle`의 Shield·HealthTitle·HealthValue·HealthBar·HealthGoal) 중 하나라도 켜져 있으면, 그 아래 끝에서 `_avoidGap`(16)만큼 떨어지도록 키 안내 전체(Esc 줄·Alt 줄·접힌 줄 묶음)를 아래로 내린다. 두 캔버스 모두 Overlay라 월드 모서리를 화면 픽셀로 비교한다. 띠가 키 안내보다 왼쪽에서 끝나면(넓은 화면) 내리지 않는다. 띠의 낙하·흔들림을 따라가도록 `LateUpdate`에서 초당 900 단위로 움직인다. y만 바꾸므로 x를 정하는 다른 컴포넌트와 겹치지 않는다.
  - 수리 보상 줄: 접힌 줄 묶음 안 Q 줄 자리에 `C 수리 보상` 줄(`hud_key_c` + ExitLabel 복제 TMP)을 만들고 Q 줄은 한 칸 더 내린다. 순서는 Esc·Alt·M·Tab·C·Q(배 탑승 시)다. 줄은 `RepairRewardScreen`의 버튼 묶음이 켜져 있을 때(판 진행 중)만 보인다.
  - `RepairRewardScreen`의 버튼 묶음은 항상 투명·클릭 불가로 두고 C 키캡 복제는 없앴다. C 키는 그대로 펼친 동안 버튼의 `onClick`을 대신 눌러 창을 연다.
  - 줄 높이를 Esc·2번째 줄에서 처음 한 번만 재도록 `_rowStep`으로 저장했다(접은 뒤 다시 재면 두 배가 된다).
- `RepairRewardScreen.cs`: 머리 주석만 갱신했다.
- 신규 `Assets/03.Images/InGameHud/hud_key_c.png`: `hud_key_m.png`의 글자를 지우고 Arial Bold로 C를 그렸다. 가져오기 설정은 `hud_key_m`과 같다.
- 씬: MainGame·JH04TestScene의 `HudReveal`에 `_exitKey`·`_narrowKey`(MapKey)·`_rewardKeySprite`·`_lastHints`(AbilityLabel·AbilityKey)·`_avoidRects`를 연결하고 저장했다. JH04TestScene에는 `RepairRewardScreen`이 없어 C 줄이 생기지 않는다. 열려 있던 JH04Offline의 저장 안 된 변경은 건드리지 않았다.
- 검증: Unity 컴파일 error 0. Main 흐름 빌드 결과는 아래 참고. 최후 전투에서 실제로 겹치지 않는지와 C 줄 표시는 플레이로 확인해야 한다.
- 빌드: Main 흐름(MainOffline → MainLobby → MainGame) `Builds/MainHudFix/TheSecretOfLightHouse.exe` 성공(18:12). 플레이 확인은 아직 하지 않았다.
- 18:16 재빌드(알림 본인 전용 후속 포함) 후 창 4개 실행. 로그 `Builds/MainHudFix/logs/client0~3.log`.
