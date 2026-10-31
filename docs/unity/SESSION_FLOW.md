# 본편 메인·입장·선원 대기실

2026-10-31 최신 사용자 결정: 첫 버튼 화면·방 생성·방 참가는 같은 씬에서 패널을 전환한다. 선원 대기실은 기존 별도 씬을 유지한다. 기존 UI 구성요소를 재사용하고 메뉴 배경은 제공된 바다·등대 이미지로 통일한다.

## 실행과 씬

본편은 `Assets/00.Scenes/Main/MainOffline.unity`에서 시작한다. 에디터의 Play는 빌드 0번이 아니라 **현재 열려 있는 씬**에서 시작하므로 먼저 MainOffline을 연다. `ProjectSettings/EditorBuildSettings.asset`의 본편 0~2번은 아래 순서다.

| 빌드 순서 | 씬 | 역할 |
|---|---|---|
| 0 | MainOffline | 첫 버튼 화면 ↔ 생성/참가 입력 패널. 생성은 닉네임과 방 생성, 참가는 닉네임·호스트 주소와 참가하기 |
| 1 | MainLobby | 기존 선원 대기실. 4명 전원 준비 시 자동 출항 |
| 2 | MainGame | 기존 게임 씬. 전원 로딩과 시작 게이트 준비 후 판 시작 |

그 뒤의 `Jihun/JH04Offline`, `Jihun/JH07Lobby`, `Jihun/JH04TestScene`도 활성화하여 기존 개발 씬의 Host 경로가 사용할 로비/게임 씬을 찾도록 한다. 이 개발 경로는 기존 통합 입력 화면이며, 새 패널 전환은 MainOffline에서 확인한다.

`SessionTitleView`가 첫 버튼 그룹과 입력 패널의 활성 상태를 전환한다. 생성/참가 선택과 뒤로는 씬을 로드하거나 매니저를 재생성하지 않는다. 매니저의 `offlineScene=MainOffline`, `onlineScene=MainLobby`, `_gameScene=MainGame` 연결은 유지한다. 생성·참가는 기존 StartHost/StartClient 경로를 사용한다. 별도 방 목록·초대 코드·비밀번호 기능을 추가한 것이 아니다.

입력 화면의 뒤로/Esc는 같은 씬의 첫 버튼 그룹을 다시 표시한다. 첫 버튼 화면의 Esc는 종료 확인이다. 접속 중에는 뒤로를 비활성화하고 참가 버튼의 연결 취소 또는 Esc로 연결을 취소한다. 취소·접속 실패는 입력 패널에 남고 닉네임·주소를 유지한다. 대기실 나가기/방 닫기·연결 종료 시에는 기존 세션 종료와 MainOffline 복귀·사유 안내를 사용하며 첫 버튼 그룹을 표시한다. 정상 판 종료 후에는 기존 연결을 유지하고 MainLobby로 함께 돌아간다.

## 원본 UI 재사용

- `Assets/02.Prefabs/UI/Session/SessionScreen.prefab`: 기존 공용 세션 UI. 대기실 배치·버튼·카드는 변경하지 않고 배경만 교체했다.
- `SessionEntryScreen.prefab`: 공용 프리팹의 Variant. 대기실과 세션 연결을 상속하고, 첫 화면의 생성·참가 전용 요소 표시만 분리한다. 본편 MainOffline에서 사용한다. 개발 씬의 기존 통합 입력 화면은 유지한다.
- 첫 버튼 그룹은 `SessionEntryScreen`의 `Title/Menu`이며 기존 생성 버튼과 뒤로 버튼 프레임·피드백을 재사용한다. 생성 340×99, 참가 326×99. 글자 이미지/폰트를 생성형 시안에서 추출하지 않는다. 입력 패널은 `Title/Panel`이다.
- 입력 화면의 생성 340×99, 뒤로 326×99, 참가 198×72, 대기실 준비 337×105, 주소 복사 147×46 등 기존 RectTransform 크기를 유지한다.
- 새 동적 글자와 입력은 저장소의 `Assets/09.Fonts/NotoSansKR/NotoSansKR-Bold.otf`를 사용한다. 본편 Variant는 OS 폰트 대체를 사용하지 않는다.
- `Assets/03.Images/SessionUi/SessionSeaBackground.png`: 사용자 제공 이미지 원본. 공용 메뉴/로딩 배경, 대기실 배경, 새 첫 화면에서 참조한다. 게임 월드 카메라 배경을 덮지 않는다.

## 다른 개발자가 변경을 받을 때

SessionEntryScreen 프리팹과 meta, 배경 PNG와 meta, MainOffline 연결 변경, SessionTitleView/SessionScreen 변경, 공용 SessionScreen 프리팹, 빌드 목록을 함께 반영한다. 에디터 API로 저장된 연결을 Git에서 공유하므로 다른 PC에서 프리팹 빌더 실행·Inspector 수동 연결을 할 필요가 없다. 첫 실행은 MainOffline에서 확인한다. 앞서 추가했던 별도 MainMenu 씬·SessionMainMenu 프리팹·SessionMainMenuView 스크립트는 최신 결정에 따라 제거했다.

씬 경로를 변경할 때에는 매니저의 씬 참조와 빌드 목록을 함께 확인한다. 기존 개발 씬 순서 변경을 본편 설정에 덮어쓰지 않는다. 공유 빌드 목록은 위 본편 순서를 유지한다.

검증은 [검증 플레이북](../ai/03_VERIFICATION_PLAYBOOK.md)에 따라 메인→각 입력→뒤로, 실패·취소, 생성·참가→대기실, 호스트 닫기, 4명 준비와 로딩을 구분해 확인한다. 파일 연결만으로 4인 실행 통과를 의미하지 않는다.
