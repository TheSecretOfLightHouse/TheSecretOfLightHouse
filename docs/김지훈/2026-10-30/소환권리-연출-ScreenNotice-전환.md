# 소환 권리 연출 정리와 ScreenNotice 전환

작성일: 2026-10-30

## 요청

크툴루 소환 권리의 초록 연출과 소환 버튼 아래 게이지 바를 없애고, 정체 확인 버튼 위에 따로 만든 알림 줄 대신 공용 `ScreenNotice`로 알린다.

## 수행

- `SummonRightCue.cs`(07.Mafia, 2026-10-29 김지훈 작성분)
  - 제거: 초록 글로우(`_glow`), 화면 가장자리 초록 플래시(`_vignette`), 남은 시간 바(`_timer`·`_timerFill`), 마지막 초 숫자(`_seconds`), 자체 알림 줄(`_line`·`_lineText`)과 이들을 그리던 런타임 텍스처.
  - 알림은 `ScreenNotice.Show`로 보낸다: 권리 획득 "바다가 응답했다 — 지금 크툴루를 소환할 수 있다"(Gold), 권리 소멸 "소환 권리가 사라졌다"(Warning), 소환 성공 "크툴루가 깨어났다 — 최후 전투 시작"(Warning).
  - 유지: 소환 버튼 맥동(마지막 3초에 빨라짐, 색 없음), 권리 획득 효과음. 남은 초는 `IdentityActionScreen`의 상태 줄("소환 가능 N초")이 계속 보여 준다.
- `IdentityActionScreenSketchSetup.cs`(에디터 빌더)에서 같은 부품 생성을 제거해 다시 빌드해도 돌아오지 않게 했다.
- `IdentityActionScreen_Sketch.prefab`: `Buttons/SummonGlow`·`SummonTimer`·`SummonSeconds`, `SummonCue/Vignette`·`Line` 삭제. `Buttons` 높이를 바 자리만큼 줄여 빌더 값(183.6)과 맞췄다. 에디터 API로 수정.
- `MainGame.unity`: `ScreenNotice.prefab` 인스턴스를 루트에 추가. 이전에는 JH04에만 있어 MainGame에서 `ScreenNotice.Show`가 아무것도 띄우지 않았다(등대 장착 알림 포함). MainGame만 추가로 열어 저장하고 닫았으며, 열려 있던 JH04Offline의 저장 안 된 변경은 건드리지 않았다.
- 정체 확인 거절 안내(`Notice`, 확인 버튼이 있는 판자 상자)와 실패 공개 배너(`InnocentRevealBanner`)는 범위 밖이라 그대로다.

## 확인한 사실

- `IdentityActionScreen.cs`·`VotingScreen.cs`의 미커밋 변경(소환 확인창 생략 등)은 이 작업이 만든 것이 아니며 보존했다.
- 07.Mafia는 박태욱 영역이지만 수정한 연출 코드는 김지훈이 만든 부분이며 사용자 직접 지시다. 공유 필요.

## 검증

- 실행함: 스크립트 컴파일 error 0, 프리팹 참조(`_summonButton`, `_audio`) 유지 확인, MainGame 씬 diff가 ScreenNotice 인스턴스 추가만인지 확인.
- 실행 안 함: Play·마피아 시점 정체 확인 성공·소환·권리 소멸 알림 실제 표시, 4인.

## 남은 일

- Play에서 마피아로 정체 확인 성공 → 알림 띠 표시 확인.
- 박태욱에게 07.Mafia 연출 변경 공유.

## 후속 (2026-10-30): 알림 문구와 심장박동 맥동

- 요청: 정체 확인 성공 시 공용 알림창(프레임+등대 아이콘, 모듈 장착 알림과 같은 `ScreenNotice`)으로 소환 가능을 알리고, 소환 버튼에 맥박 연출만 간략히.
- 알림은 이미 `ScreenNotice`(기본 아이콘 `fb_icon_lighthouse`)로 나가고 있어 문구만 "정체 확인 성공 — 지금 크툴루를 소환할 수 있다"로 바꿈.
- `SummonRightCue`의 사인파 맥동을 심장박동(강한 박 + 0.2초 뒤 약한 박, 각 0.12초, 나머지는 정지)으로 교체. 평소 1.2회/초, 마지막 3초 3회/초. 새 직렬화 필드 `_beatSeconds`·`_secondBeatDelay`·`_secondBeatStrength`(기본값 사용, 프리팹 미수정).
- 권리 소멸 알림은 유지.
- 검증: VS Code 진단 0. 다른 세션이 Unity MCP 사용 중이라 Unity 컴파일·Play 미실행.
- 남은 일: Unity 컴파일 확인, 마피아로 정체 확인 성공 → 알림·맥박 확인.
