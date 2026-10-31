# 유령선 E 안내·장부 미동작, 등대 장착 알림 미표시 수정

작성일: 2026-10-31

## 증상 (사용자 빌드 확인)
1. 배에 탄 채 유령선 근처에서 "E 유령선 확인"이 안 뜨고 E를 눌러도 장부가 안 열림.
2. MainGame에서 등대 모듈 장착 알림(ScreenNotice: 등대 아이콘+판+문구+게이지)과 층 완성 "수리 완료 — 보상 해금" 알림이 안 뜸. JH04TestScene에서는 보였음.

## 원인
1. 모든 배 프리팹(Boat·TankerBoat·captainBoat·gardenBoat)에 `InteractionTarget`(기본 `RepairBoat`, 사거리 3, Default 레이어)이 있음. `InteractionSelector`는 사거리 안 가장 가까운 대상을 고르므로 탑승 중에는 내 배가 항상 선택되어 유령선(사거리 20)이 선택되지 못함 → 프롬프트 미표시, E는 내 배 수리 요청으로 감. 레이어·장부 프리팹 연결·MainGame 배치는 정상.
2. 2026-10-30에 넣은 `TowerScreen.HideOtherScreens`가 등대 창이 열려 있는 동안 ScreenNotice·TowerMatchEffects 캔버스까지 꺼서, 장착 시 나온 알림이 보이지 않는 채 시간(2.6초)만 흐르고 사라짐. MainGame에는 ScreenNotice 1개·TowerMatchEffects 1개가 활성 상태로 정상 배치됨을 확인.

## 수정
- `InteractionSelector.FindNearest`: 로컬 플레이어가 타고 있는 배(`PlayerController.CurrentBoat`)와 그 자식의 대상은 E 후보에서 제외. 배 수리는 소지품 화면 버튼이 직접 `RepairBoat`를 보내므로 영향 없음. 육지에서 배 근처 E 수리는 그대로.
- `TowerScreen.HideOtherScreens`: `ScreenNotice`·`TowerMatchEffects`가 붙은 캔버스는 숨기지 않음(정렬 90으로 등대 창 80 위에 표시).

## 다른 알림 확인 (코드·배치 기준)
- ScreenNotice 호출처: 등대 장착·층 완성·마지막 1칸·모듈 완성·최후 전투 시작·등대 붕괴(TowerMatchEffects), 유령선 장부 이미 읽음(GhostShipHintPopup), 소환 권리 획득·소멸(SummonRightCue), 마피아 투표 불가(VotingScreen). 모두 MainGame의 ScreenNotice 하나로 표시되며, 숨김 문제는 등대 창이 열린 동안에만 있었음.

## 검증
- Unity 컴파일 error 0. 빌드·Play 미실행.

## 남은 일
- 빌드로 배 탑승 중 유령선 E 안내·장부, 등대 장착·층 완성 알림 확인.
- 배 E 대상 사거리 3 안에 다른 사람 배가 있으면 그 배가 유령선보다 먼저 선택될 수 있음(드묾).

## 빌드 (2026-10-31 00:54)
- 실행 중이던 빌드 창을 닫고 `메인씬빌드`에 일반 빌드 성공(Editor.log `Build Finished, Result: Success`, 산출물 00:53 갱신 확인), 창 4개(960x540, `client0~3.log`) 실행.
- 포함: 이 문서의 두 수정, Q 줄 탑승 시만 표시, 키 안내·E 안내 TMP 통일, 등대 창 왼쪽 이동·Esc 위치 통일까지 프리팹 반영분 전부.
