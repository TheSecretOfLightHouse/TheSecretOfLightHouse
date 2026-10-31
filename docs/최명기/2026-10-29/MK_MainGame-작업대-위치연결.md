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

## 2026-10-31 후속 — MainGame 기준 생성 시설·선착장 연결
- 사용자 요청: MainGame을 참조해 작업대와 선착장 연결을 MK_MainGame에 반영. 요청한 MK 씬 수정 범위로 진행, 공용 World 코드/원본 MainGame/프리팹은 수정하지 않았다.
- 기존 MK는 고정 작업대·등대·Dock 및 고정 배 스폰을 사용했다. MainGame은 MainIslandFacilitiesBuilder와 MainIslandPlayerSpawns를 순차 맵 빌드에 포함해 생성 섬의 작업대/등대 앵커 및 플레이어·배 스폰을 사용한다. 위치 불일치 방지를 위해 이 구성을 채택했다. 관련 N10의 본편 연결 후속이며 게임 규칙 변경 없음.
- Unity Editor API로 MK LocalMapBuilders에 두 컴포넌트를 추가하고 MainGame의 프리팹/배정 구성을 복사, 모든 씬 참조를 MK의 IslandWorld·ModuleInteractionHandler·Voting·작업대 화면3개·등대 화면·최후전투 HUD·BoatSpawnPoints로 재연결했다. CompositeLocalMapBuilder의 세 번째 단계로 시설 생성기를 등록했다.
- InventoryConnection은 고정 Workbench 대신 시설 생성기를 참조한다. 기존 Workbench/Lighthouse/Dock/DevTowerTools/DevWorkbenchTools는 LegacyMainIslandFacilities_Disabled 아래 비활성으로 보존해 중복 시설·구형 선착장을 막았다. 등대는 시설 생성기 구성에 필요한 연동이며 기존 UI는 생성 시설에 Bind된다.
- 메인 섬 MainIslandAppearance 프리팹의 IsValid=true, 선착장 DockZone 2개, 플레이어/배 지점 각4개 확인. 두 씬의 MapStateNet은 동일한 MapGenerationConfig와 각 씬 자체 LocalMapBuilders를 참조한다. 런타임 BindMainIsland에서 배 위치와 작업대 연결을 갱신한다.
- 실제 검증: Unity ready/stopped 확인, 저장 후 씬 재로드. 시설 필수 참조 누락0·빌드 단계3·인벤 facilities 연결true·기존시설 비활성/자식5개·네트워크 sceneId 중복0·dirty=false 확인. 저장 전 교차 씬 참조0 확인. additive 로드 시 Mirror가 바꾼 sceneId는 원래 HEAD 값으로 Editor API 복구했고 최종 diff에 sceneId 변경 없음. MainGame/MK_MainOffline 디스크 변경 없음, 조사용 씬을 닫아 기존 MK_MainOffline 단독 상태로 복원했다.
- 기존 Lighthouse의 Missing Script 1개는 비활성 보존 영역에 남아 있다(10/29 기록과 동일). 씬 저장으로 현재 코드의 _isCarriedHudShown 기본값 직렬화가 추가됐다. diff --check는 Unity가 생성한 빈 m_Name/m_EditorClassIdentifier의 후행 공백만 보고했으며 YAML 직접 수정은 하지 않았다.
- 변경 파일: Assets/00.Scenes/Myeongki/MK_MainGame.unity, 이 원문, POINTER.md. C# 변경 없음. 기존 조명탄/에셋/설정/폰트 미커밋 변경 보존.
- 상태: 구현됨(미검증). 실제 접속/생성 완료→선착장 4인 배 배치→작업대 E 열기/제작·모듈 인벤 연결→호스트/원격 M2는 미실행. 다음은 MK_MainOffline에서 새 판을 시작해 이 경로 확인.

## 2026-10-31 후속 — 배 모듈 테스트 자원 자동 지급
- 사용자 요청: 배 모듈 전체 테스트를 위해 인벤토리에 제작 자원을 넉넉하게 지급. 현재 Editor stopped 상태이므로 MK_MainGame 새 판 참가 시 지급하도록 설정했다.
- 기존 DevWorkbenchResourceSeeder를 별도 활성 루트 DevBoatModuleTestResources에 추가하고 나무30·돌10·철10·섬유10·유리10·수정10을 설정했다. 서버 ActorJoined에서 각 참가자 인벤토리에 TryAdd하며, MK 테스트 씬에만 적용. 런타임 코드/레시피/본편 MainGame 수정 없음.
- 실제 확인한 4종 총 비용: 나무20·돌10·철9·섬유6·유리5·수정1. 기본 8칸·스택10 용량을 고려해 80개로 채웠다. 기존 InventoryData.TryAdd로 6종 전부 추가 성공/UsedSlots=8 확인, 실제 레시피4개를 읽어 비용 합계 충족 확인. 에디터 저장 성공.
- 실제 판 참가 후 서버 지급·UI 표시·제작/M2는 미실행. 다음은 MK_MainOffline에서 새 판을 시작해 확인. 테스트가 끝나면 DevBoatModuleTestResources를 비활성화하면 자동 지급을 중단할 수 있다.
