# 시민·마피아 승리 화면 이미지 UI

- 날짜: 2026-10-29
- 요청: 시민·마피아 승리 때 텍스트 알림 대신 제공된 승리 UI(심볼·제목·문구·확인 버튼)로 배치.

## 수행
- `output/ui-victory-2026-10-28/components`의 투명 PNG 8개를 `Assets/03.Images/VictoryScreen/vs_*.png`로 복사(Unity가 .meta 생성).
- `Tower/View/VictoryScreen.cs` 추가: `TowerMatchNet.Phase`가 `Ended`가 되면 `Outcome`에 맞는 진영 심볼·제목·문구를 켜고 1.2초 뒤 페이드·상승 등장. 확인 버튼 또는 Enter로 닫힘. 열려 있는 동안 커서 잠금 해제, 닫으면 복원. 늦게 들어온 클라이언트는 연출 없이 바로 표시.
- `Workbench/Editor/VictoryScreenSetup.cs` 추가: 메뉴 `Lighthouse/Tower/Build Victory Screen Prefab`(스프라이트 Import 설정 + `Assets/02.Prefabs/UI/Tower/VictoryScreen.prefab` 생성), `Use Victory Screen In Open Scene`(씬 배치·`_match` 연결). Canvas sortingOrder 110.
- `TowerMatchEffects.ShowEnd`: 종료 문구 타이틀("등대에 불이 켜졌다 — 탈출하라"/"등대는 어둠에 잠겼다") 제거. ScreenNotice 정리·어두워지는 페이드·종료음은 유지.
- `TowerMatchEffectsSetup.UseInOpenScene`: VictoryScreen 프리팹이 있으면 함께 배치.

## 결정 이유
- 결과 판정은 서버 SyncVar(`Phase`, `Outcome`)를 그대로 읽어 네트워크 계약 변경 없음.
- 확인 버튼 동작은 기획에 정의가 없어 "화면 닫기"로만 둠(로비 복귀 등은 미결정).

## 검증
- Editor.log 기준 스크립트 재컴파일 완료, `error CS` 0건.
- 처음 `Use Victory Screen In Open Scene` 오류는 프리팹 미생성 때문. MCP 재연결 뒤 프리팹 빌드 성공, `JH04TestScene`에 배치·`_match`=Lighthouse 연결 확인(씬 저장은 사용자에게 맡김).
- 사용자 빌드 시험에서 UI 미표시 → 원인: 씬 미저장으로 빌드에 VictoryScreen 없음. `JH04TestScene` 저장(VictoryScreen 추가 + 선반 작업의 `_isRackMode` 필드 제거분) 후 `Builds/JH04` 재빌드 성공(오류 0), 960×540 창 4개 실행(로그 `Builds/JH04/log1~4.txt`).
- 실제 승리 화면 표시는 사용자 4인 시험에서 확인 대기.

## 문구 교체(같은 날)
- 사용자가 보낸 새 투명 시트(1774×887, RGBA)로 8개 구성요소 전부를 다시 잘라 `vs_*.png`를 덮어씀(.meta·GUID 유지). 문구: 시민 "등대를 밝히고 탈출했습니다", 마피아 "탈출은 저지되었습니다". 원본·조각은 `output/ui-victory-2026-10-28/components-v3/`.
- 새 조각은 픽셀 크기가 제각각이라 `VictoryScreenSetup.Picture`를 픽셀 크기 대신 목표 높이에 맞추고 비율 유지로 변경(심볼 220, 제목 130, 문구 56, 버튼 104, 확인 글자 60).
- 컴파일 오류 0, 프리팹 재빌드, 1920×1080 배치 모의 합성으로 두 진영 화면 확인. 빌드 재생성·실제 플레이 확인은 안 함.

## 남은 일
- 에디터에서 `Build Victory Screen Prefab` → 대상 씬에서 `Use Victory Screen In Open Scene` → 씬 저장.
- 시험 패널의 시민/마피아 승리 강제로 M0 확인, 레이아웃 간격 조정.
- 확인 버튼 후 흐름(로비 복귀 등) 결정.
