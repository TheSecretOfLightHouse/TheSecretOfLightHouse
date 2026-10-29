# MK_MainGame 작업대 위치 연결
작성자: 최명기
작성일: 2026-10-29
갱신일: 2026-10-29
상태: 구현됨(미검증)
브랜치·기준 커밋: feature/myeongki / b00c4aac

## 요청과 목표
눈앞의 섬 작업대에서 상호작용 안내가 나오지 않는 원인을 확인하고, 사용자 요청에 따라 MK_MainGame의 기능용 작업대를 해당 위치에 맞춘다.

## 진행 과정과 결정 이유
- 실제 Play에서 캐릭터 (-5.41, 0.75, 0.96), 기능용 Workbench (2.50, 2.20, 2.50), 거리 약 8.18m와 UI의 범위 밖 상태를 확인했다. 화면의 섬 작업대에는 기능 컴포넌트가 없었다. 최초 스폰 위치의 3.12m만으로 가까이 붙으라고 안내한 판단을 정정했다.
- MainGame은 MainIslandFacilitiesBuilder가 섬 anchor에 기능용 작업대를 생성하고 화면을 Bind하는 구조다. MK_MainGame은 고정 작업대를 참조한다. 관련 공통 검증 계약은 문제 대장 N10(부분 결정·본편 연결 후속)이며 이번 원인은 씬 배치 불일치다. 게임 규칙 변경은 없다.
- 사용자가 MK_MainGame 수정 범위를 명시적으로 허용했다. Play를 종료하고 기존 MK_MainOffline의 미저장 변경을 보존한 채 MK_MainGame을 additive로 열었다.
- Unity 에디터 API로 기존 Workbench를 등대 섬 프리팹 MainIslandPresentation.WorkbenchAnchor의 위치 (-5.60, 1.266753, 1.65), 회전 identity에 맞췄다. 기존 네트워크 객체와 UI 참조를 유지했다.
- 섬에 이미 작업대 외형이 있으므로 기능용 작업대의 렌더러를 비활성화해 중복 외형을 피했다. 프리팹 원본이나 World 코드는 수정하지 않았다.

## 변경 파일과 결과
- Assets/00.Scenes/Myeongki/MK_MainGame.unity: 작업대 위치 및 렌더러 override 저장.
- 에디터 저장으로 현재 코드의 기본 직렬화 필드 _lobbyReturnSeconds=10, _facilities=None, _devDamage=100이 추가되고 폐기된 _isRackMode가 제거됐다. 추가 동작 수정은 하지 않았다.
- 이 원문과 최명기 POINTER.md.

## 검증
- 실행: 저장 후 좌표 일치, renderer 전부 숨김, 작업대 UI 3종의 _workbench와 _target 참조 일치 확인. 이전 플레이어 좌표에서 새 대상까지 약 0.883m로 범위 3m 안이다.
- 실행: 대상 씬 git diff --check 통과. MainGame 및 MK_MainOffline의 디스크 diff 없음. C# 변경 없음.
- 기존 Lighthouse 객체의 Missing Script 1개가 확인됐다. 이번 diff에 스크립트 참조 변경은 없으며 범위 밖으로 보존했다.
- 미실행: 수정 후 실제 접속·E 입력·제작·원격 M2. 좌표와 참조 확인을 실제 플레이 통과로 확대하지 않는다.

## 남은 일과 차단 조건
- MK 로비에서 다시 접속해 섬 작업대 앞 E 안내와 열기·닫기를 확인하고 원격 M2로 제작 경로를 확인한다.
- 이번 변경은 현재 섬 프리팹 anchor에 맞춘 고정 배치다. 향후 anchor가 바뀌면 MK 씬도 다시 맞춰야 한다.

## 다음 행동
MK_MainOffline에서 Play 후 4인 준비를 거쳐 작업대 상호작용을 확인한다.

## 관련 원문·인수인계
- [MainGame UI 이식](MainGame-UI-이식.md)
