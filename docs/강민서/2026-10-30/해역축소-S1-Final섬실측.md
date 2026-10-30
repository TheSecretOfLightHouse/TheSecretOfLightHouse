# 해역축소 S1 — Final 섬 실측
작성자: 강민서 작업 / 작성·검증 AI  
작성일·갱신일: 2026-10-30  
상태: 완료(검증됨), 프리팹 정적 실측 한정  
브랜치·기준 커밋: feature/minseo / 47e36bf9aaf6002db148a66554f70f59f557ee66

## 요청과 목표
[해역 설계](../../design/sea-size-and-map-data-plan.md)와 [S0 결과](해역축소-S0-기준확보.md)를 읽고 S1 Final 6종/사용 Variant의 육지·배치 경계, 루트 기준 오프셋, 부두를 실측했다. S2~S7, 반경·간격·해역·자원·몬스터 설정 변경, 커밋·push·PR·SVN 업로드는 수행하지 않았다.

## 진행 과정과 결정 이유
- S0 원문·인수인계·pointer를 읽고 실제 브랜치/HEAD·staged/unstaged 상태를 확인했다. S0 입력 SHA-256 501개 모두 일치([검사](SeaS1/s0-input-check.json)). MainGame·WorldSessionGame 저장 씬을 OpenPreviewScene/ClosePreviewScene의 try/finally로 읽고 저장하지 않았다.
- Unity CLI 1.0.0-beta.15 최신 확인, Unity 6000.0.83f1 ready. 최초 WaterF3 Play/clean, 후속 Edit/clean이 관측됐으나 본 작업은 Play 시작·중지·오브젝트 생성·씬 저장을 호출하지 않았다. 다른 세션의 실행을 검증 성과로 계산하지 않는다.
- 프리팹 에셋은 activeInHierarchy가 false이므로 루트까지 activeSelf를 순회해 활성 경로를 판정했다. 실제 설정의 LighthouseIsland+Archetypes에서 Variant를 전부 열거했다. Final 폴더 검색 6개와 일치하며 모두 Variant0·Weight1·루트 위치/회전0·배율1이다. Terrain0, 지원되지 않는 Renderer0, Mesh 읽기 fallback0.
- 실제 생성은 IslandSpawnPlanner→IslandInstanceSet.Create이며 yaw를 적용하고 별도 크기 변경은 없다. 두 저장 씬의 IslandWorld lossyScale=(1,1,1). 모든 Final에는 IslandCoastWaveView가 있어 WorldSessionGame의 추가 CoastalShelf 생성 분기는 실행되지 않는다. 이는 소스·저장 참조 확인이며 본편 런타임 인스턴스를 생성해 검증한 것은 아니다.
- 읽기 전용 [실측 질의](SeaS1/measure-query.cs)를 eval_file로 실행하고 CLI로 반복해 원시 결과를 보존했다. 프로젝트 런타임 C#는 수정하지 않았다. Assets 밖의 질의는 재현 기록이다.
- 부두 목록 검수용 PowerShell에서 foreach 뒤 pipe 문법 오류 1회가 있었고 ForEach-Object로 바로잡아 재실행했다. 측정 질의는 컴파일 diagnostics 없이 성공했다.

## 경계 포함·제외 기준
| 구분 | 이번 측정 기준 | 용도·한계 |
|---|---|---|
| 육지 | 활성 Ground 하위 MeshRenderer의 메시 정점, 잠긴 외곽 포함 | 시각 지형의 전체 XZ; 걷기 가능 면적이나 수면 교차선 아님 |
| 고체 | 활성 경로의 enabled·비trigger Collider 전부 | 높이로 제외하지 않아 보수적. MeshCollider는 sharedMesh 정점, Box는 8꼭짓점, Sphere/Capsule은 보수 상자 |
| 부두 시각 | 활성 MeshRenderer 중 Dock/Pier/Jetty 경로 | 실제 포함 경로를 전수 검수: 데크·기둥·부두 소품이며 배우·배 혼입 없음 |
| 배치 경계 | 육지 + 고체 + 부두 시각의 합집합 | 육지·고체·부두 겹침 방지용 루트 중심 원. 배 크기·선회·파도·접안 여유는 미포함 |
| 접안 예약 | 배치 경계 + 활성 DockZone trigger | trigger는 물리 장애물이 아니므로 별도 기록. 통로 유효성/실제 접안 성공 보장 아님 |
| 제외 | 비활성 경로·disabled, 일반 trigger, 충돌체 없는 일반 식생/장식, 파도/포말 효과 | 포함 여부와 이유를 객체별 records에 보존. 고체 식생/소품 Collider는 포함 |

메시 정점은 자식 변환을 거쳐 루트 원점·실제 배율 기준으로 계산한다. AABB min/max와 중심 오프셋은 XZ 축 정렬 경계값이며 원의 중심이 아니다. 원 반경은 포함된 모든 점의 max(sqrt(x²+z²))다. 중심을 AABB 중심으로 옮겨 반경을 줄이지 않았다. 메시 삼각형/Box 내부는 꼭짓점 원 안에 포함되며 Sphere/Capsule 상자는 보수 경계다. MeshCollider의 convex 처리·메시의 미사용 정점이 있으면 실제 점유보다 커질 수 있다.

전 시각 요소 전체를 배치 경계로 합치지 않았다. 경계 밖 제외 항목은 General Palm_Right_Leaning LOD3, Sand Palm_Left LOD0~3, Crystal Palm_LeftHero LOD3이며 잎 LOD 메시다. 가장 큰 Sand 시각 반경8.637m는 배치7.662m와 구별한다. 이는 시각적 잎 겹침까지 금지하는 정책이 아니다. 정책 변경이 필요하면 S3에서 별도 검토한다.

## 6종/Variant0 실측표
단위 m. 아래 표는 소수 셋째 자리 표시이며 계산·검증에는 [원시 JSON](SeaS1/measurement-verified.json)의 정밀값을 사용한다. 접안 포함 반경은 max(배치 반경, DockZone 반경)이다. 설정값은 변경하지 않았다.

| 섬 | 육지 X×Z(m) | 배치 X×Z(m) | 배치 중심 X/Z(m) | 육지 r(m) | 배치 r(m) | 접안 포함 r(m) | 설정 r(m) |
|---|---|---|---|---:|---:|---:|---:|
| Island_Lighthouse | 24.500×18.700 | 24.502×22.373 | 0.101/-1.587 | 12.454 | 14.854 | 18.253 | 33 |
| Island_General | 13.945×8.920 | 14.000×9.935 | 0.052/-0.358 | 7.054 | 7.079 | 9.366 | 33 |
| Island_Forest | 12.850×10.492 | 12.850×10.850 | -0.125/-0.093 | 6.575 | 6.575 | 10.409 | 34 |
| Island_Ore | 13.750×9.750 | 13.850×10.154 | 0.075/0.023 | 6.931 | 7.218 | 10.640 | 34 |
| Island_Sand | 13.564×6.729 | 14.538×8.214 | -0.288/-0.543 | 7.017 | 7.662 | 8.043 | 32 |
| Island_Crystal | 14.507×11.750 | 14.615×12.042 | 0.104/0.186 | 7.427 | 7.515 | 10.053 | 32 |

육지 중심 X/Z: Lighthouse 0.100/0.250, General 0.025/0.149, Forest -0.125/0.025, Ore 0.025/0.225, Sand 0.100/0.199, Crystal 0.050/0.225. 배치 AABB min/max·높이·원시 중심·객체별 경계와 포함/제외 근거는 JSON groups/records에 있다. 접안 예약 반경을 미터 단위로 올림하면 등대19·일반10·숲11·광맥11·모래9·수정11이지만 이는 실측 포괄 예시이며 새 설정값이나 항해 통로 여유 확정안이 아니다.

## 부두 7곳
좌표는 섬 루트 기준 X/Y/Z(m). LandingSurfaces는 실제 DockZone 참조 경로다.

| 섬 / DockZone | 로컬 위치 | 연결 착지면 |
|---|---|---|
| Lighthouse / DockZone_Left | -6.400 / 0.500 / -9.500 | Dock_Left_Inner, Dock_Left_Outer |
| Lighthouse / DockZone_Right | 12.000 / 0.500 / -3.700 | Dock_Right |
| General / DockZone | -2.500 / 1.080 / -4.350 | Dock_LandingSurface |
| Forest / DockZone | 3.300 / 0.880 / -4.160 | Dock_LandingSurface |
| Ore / DockZone | -5.630 / 0.860 / -2.240 | Dock_LandingSurface |
| Sand / PalmDock/DockZone | -3.100 / 0.800 / -2.500 | PalmDock/Dock_LandingSurface |
| Crystal / DockZone | 2.880 / 0.410 / -4.780 | Dock_LandingSurface |

7곳 모두 활성·enabled, 착지면 참조 누락0. trigger의 center/size/회전에서 계산한 점유 경계는 records의 dockTrigger, 부두 구조물 경계는 dockVisual이다. 각 Zone transform.forward=(0,0,1), yaw=0°지만 DockZone 코드는 위치 포함 검사와 LandingSurfaces만 사용한다. **Zone.forward를 실제 바다 진입 방향으로 해석하면 안 된다.** 전용 항로 진입 앵커·접안 방향 계약은 이번에 만들지 않았다. S2는 실제 부두 형상·출항/접안 궤적을 확인해 경로를 정한다. 로컬 점의 월드 좌표는 island.Position + Quaternion.Euler(0,yaw,0) × localPoint, 벡터는 같은 회전만 적용한다.

## 기획·코드 대조와 영향
- v4 8.1~8.3의 해금·랜덤 섬·동일 월드와 시스템/기술설계, 최신 승패 결정의 미변경 범위를 유지했다. 해역 축소는 사용자 설계를 따르되 이번은 실측만이다.
- 확인한 불일치: 설정 Radius32~34m는 최신 실제 점유 반경보다 크다. 이를 유지한 공통 슬롯 최소 간격은 실제 육지 크기보다 크게 잡힌다. 이것만으로 최적 간격이나 짧은 항해 달성을 판정할 수 없다. S2 배·파도·부두 측정 후 S3에서 조정한다.
- Radius 공유: MapGenerationConfig.MaxIslandRadius→IslandZoneMargin/GetIslandMinSpacing, MapContext.TryGetRadius→RouteValidationStep, MapLayoutAssembler→IslandPlacement.Radius, WalkBoundaryBaker.Enqueue→WalkMaskBuilder 샘플 영역, ShoreDropLocator→ShoreLineSearch 탐색 최대 거리, MapContentHasher에 소비된다. **배치 반경만 작게 하려다가 같은 필드를 바꾸면 보행 굽기 범위·해안 탐색 거리·해시도 바뀐다.** S3에서 용도 분리 필요 여부와 보행/부두/난파 회귀를 확인해야 한다. 아직 필드 분리나 수치 변경을 승인·실행한 상태가 아니다.
- 기존 IslandFootprintCheck는 비활성 포함 Renderer bounds의 축별 최대 edge를 쓰며 부두 트리거를 따로 다루지 않는다. 회전에 안전한 원 반경의 근거로 그대로 쓰지 않고 별도 질의로 실제 최대 XZ 거리와 포함 정책을 기록했다. 기존 검사 수정 없음.
- W14는 계약 확인 필요: 실측 원은 공급·해금별 왕복·접안 접근성을 증명하지 않는다. W17은 일반 몬스터 안전지대 부분 결정/Play·M2 잔여, W18·R18은 검증 가설 상태를 유지한다. 실측으로 해결 처리하지 않는다. S0 다양성138/200 기존 실패도 미수정이며 이번 재생성 검증은 하지 않았다.

## 검증
- [첫 실측](SeaS1/measurement.json)과 [회전 재실측](SeaS1/measurement-verified.json)의 6종 groups가 모두 동일했다. yaw0/37/90/181/270°에서 전체 배치 점을 회전해 반경을 다시 계산했고 최대 차이는 약0.000001m다. 원형 경계는 yaw에 불변이며 회전한 AABB 폭을 고정 폭으로 오해하지 않는다.
- [검수 JSON](SeaS1/verification.json): 사용 Variant6·부두7·메시 읽기 fallback0·미지원 Collider0. 참조·부모 배율·Terrain·Renderer 누락 검사 결과는 [context](SeaS1/context.json), 재현 질의는 [context-query](SeaS1/context-query.cs).
- 실측 의존 에셋과 메타·저장 씬 1,472개 SHA-256 전후 일치([전](SeaS1/inputs-before.json)·[후](SeaS1/inputs-after.json)). S0 지문501개도 일치했다. 입력 잠금이나 다른 세션의 이후 변경까지 보장하지 않는다.
- 문서 L1 및 에디터 에셋 기하 측정 완료, M0. 본편 L2 Play·물리 접촉·진입 방향·실제 항해·생성 맵의 메시 간 교차·4인·성능·SVN 공유는 미검증이다. 강제 recompile/Test Runner는 실행하지 않았고 scriptCompilationFailed=false만 관측했다.

## 변경 파일과 결과
이 원문, SeaS1 질의·원시값·검수·지문 묶음, S1 인수인계, 설계 S1 행, pointer 해역 항목만 작성/갱신했다. 게임 C#·씬·SO·프리팹·meta 수정 없음. 기존 Clock/Ocean/Mirror·WaterF3·기타 다수 미추적 파일은 이번 성과나 커밋 범위가 아니다.

## 남은 일과 차단 조건
S1 정적 실측의 차단 없음. S2~S7 미착수. 다음은 S2 실제 배·파도·일반/속도 보너스 직군의 항로별 시간과 지도 계약 초안이며 사용자 요청 후 시작한다. Final은 S0 당시와 같은 Git 미추적/SVN 공유 대상 경로다. 다른 PC에서는 Final 의존 에셋과 문서 공유·지문 일치부터 확인한다.

## 커밋 적기와 다음 행동
현재 재현 가능한 실측표·부두·검수 기록이 확보되어 docs 커밋 적기다. 제안 메시지: **docs: 해역 축소 S1 Final 섬 실측과 재개 기록 추가**. S0가 아직 미커밋이므로 S0/S1을 함께 검토해 한 docs 커밋으로 묶거나 S0→S1 순서로 나눌 수 있다. S0 산출물 없이 S1만 다른 PC에 공유하지 않는다. 새 설계 문서 전체와 pointer의 기존 다른 작업 변경을 검토하고 해역 관련 hunk만 선택한다. git add .는 사용하지 않는다. 커밋·push·PR 미실행, 기본 PR 안내 시점은 S4 후다. 프리팹 수정/SVN 업로드 없음.

다음 시작 문구:
> 해역 설계와 S0~S1 결과를 읽고 S2 항해 측정·지도 계약 초안만 진행해줘. 시험 목표와 미확정 결정을 구분하고 결과·재개 기록을 남겨줘. 작성·검증은 AI가 진행해줘.

## 관련 원문·인수인계
[설계11절](../../design/sea-size-and-map-data-plan.md#11-단계별-세션-실행재개-규칙) → [S0](해역축소-S0-기준확보.md) → 이 결과 → [S1 인수인계](../../handover/강민서/2026-10-30/해역축소-S1-인수인계.md).

## 산출물 최종 검수
[재검수 스크립트](SeaS1/verify.ps1)를 저장소 루트에서 실행해 6종·부두7·회전 최대 오차0.000001m·입력1,472개 변경0·관련 문서 상대 파일 링크372개 유효를 확인했다([검수 결과](SeaS1/final-verification.json)). git diff --check는 오류 없이 끝났고 기존 LF→CRLF 안내만 출력됐다. staged 변경 없음·HEAD 유지. 이번 기록의 문서 링크와 정적 실측 검수이며 본편 Play 통과가 아니다.
