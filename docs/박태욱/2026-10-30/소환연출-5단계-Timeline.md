# 소환 연출 5단계 Timeline
작성자: 박태욱
작성일: 2026-10-30
상태: 12초 연출 1차 구성 및 로컬 재생 확인, 메인 전투 연결/원격 검증 대기

## 구현
- 기존 SummonCinematic 표시 씬을 실제 12초 Timeline으로 교체했다. Placeholder 자산은 보존했지만 Director는 새 `SummonCinematic.playable`을 참조한다.
- 카메라: 0~2.4초 수면 가까운 등장 → 2.4~5.2초 섬 전체 → 5.2~9.8초 등대 감기 근접 → 9.8~12초 전체 마무리. 한 카메라의 애니메이션으로 컷과 샷 내 이동을 구성했다.
- 등대 감기는 기존 LighthouseTentacle_Emerge를1.2초에 시작하고 끝난 자세를 유지한다. 클립 본 움직임/길이를 늘려 메쉬를 변형하지 않았다.
- 주변 촉수5개는0.45초부터0.55초 간격으로 등장한다. 기존 Idle의 리깅 파동을 이용한3.4초 등장 클립에 수면 아래에서 올라오는 위치 곡선을 추가했다. 스케일은 유지한다. 등장 완료 후 CinematicIdle로 연결한다.
- 주변 촉수 기반은 수면 아래에 약간 묻히도록 표시 씬의 배치를 조정했다. 메인 전투 배치는 수정하지 않았다.
- 각 등장에 ControlTrack 물보라5개, 자체 생성 PCM 음원3종(저음·물소리·감기는 소리)을 AudioTrack3개로 배치했다. 새 소리의 음색/음량은 시각 피드백에 따라 조정 가능한1차본이다.
- 차가운 조명과 등실의 따뜻한 빛, 별도 바다 재질·물보라 파티클 텍스처/재질을 구성했다. 기존 메인 재질/구매 원본은 수정하지 않았다.
- 표시 섬에 남아 있던 MainIslandPresentation/IslandCoastWaveView/DockZone을 제거해 부두와 월드 등록 부작용을 차단했다. 최종 Collider/NetworkIdentity/Rigidbody 및 Lighthouse 게임플레이 MonoBehaviour는0개다.

## 코드·설정
- TowerBattleSettings의 기본 연출 길이를12초로 변경했다. 현재 검색된 TowerBattleSettings_JH04Trial 설정 에셋도12초로 맞췄다. 피해/공격 주기 수치는 수정하지 않았다.
- SummonCinematicView는 처음 서버 시각에 맞춰 seek한 뒤 UnscaledGameTime으로 연속 재생한다. 0.1초보다 큰 시각 오차만 보정한다. 매 프레임 seek하여 Timeline 오디오 스케줄을 방해하지 않도록 바꿨다. 서버는 기존 공통 종료 시각을 유지한다.
- 씬/Timeline/애니메이션/재질/설정은 Unity MCP와 에디터 API로 편집했다. 직렬화 YAML을 직접 수정하지 않았다.

## 주요 에셋
- 씬: `Assets/00.Scenes/TaeWook/SummonCinematic.unity`.
- Timeline: `Assets/05.Animations/Boss/SummonCinematic.playable`.
- 새 클립: SummonCinematic_Camera.anim, WhipTentacle_Emerge.anim, WhipTentacle_CinematicIdle.anim.
- 공용 효과: `Assets/06.Textures/Boss/Summon_FoamParticle.asset`, `Assets/07.Materials/Boss/Summon_Foam.mat`, SummonCinematic_Sea.mat.
- 음원: `Assets/08.Audio/Boss/Summon_DeepRumble.wav`, Summon_WaterRise.wav, Summon_CoilCreak.wav.

## 실제 확인
- 에디터 샘플링0/2/4/8/11.9초: 주변 촉수 로컬Y -11.2 → -5.992 →0.8로 상승, 이후 유지. 등대 끝 본은 수면 아래에서 등대 상부로 진행하고 최종 감기 자세에 도달한다.
- 카메라 로컬 좌표/월드 오프셋 및2·8·11.5초 직접 카메라 렌더 확인. 캡처는 Captures/Summon_Timeline_02.png, _08.png, _11.png.
- Play에서 실제 Director 12초 재생 및15개 출력 확인. 2초 지점 물보라 입자 수74/42/3/0/0으로 순차 등장 확인.
- 연속 재생 약2.049초에 DeepRumble/WaterRise의 AudioSource.isPlaying=true 확인. CoilCreak는 그 시점에 아직 시작 전이다. 최종 청감/믹싱과 원격 음향은 별도 확인 필요.
- 주변 등장 클립 loopTime=false, 씬 길이와 서버 설정12초 일치 확인. 재생 종료 후 편집 모드 복귀.
- 코드 재컴파일 오류는 발견하지 않았다. 도메인 재로딩 중 ShaderGraph 패키지의 SerializableTexture.OnBeforeSerialize에서 domain-backup 오류2개를 관찰했다. 패키지를 수정하지 않았으며 재생/렌더 결과와 구분한다.
- git diff --check에는 Unity가 저장한 씬의 빈 m_WarningMessage/m_Name/m_EditorClassIdentifier 끝 공백3개가 보고됐다. YAML 직접 편집 금지에 따라 생성 결과를 유지했다.

## 남은 범위

### 단독 미리보기 보완
- 사용자가 Game 화면/재생이 동작하지 않는다고 보고했다. 기존 씬은 카메라 비활성·Director Manual·playOnAwake=false로 실제 네트워크 전환 전용이었다. 카메라만 켜는 것으로 재생 문제는 해결되지 않았다.
- `99.Dev/Cinematics/SummonCinematicPreview.cs`를 Director에 추가했다. 이 씬만 단독으로 열고 Unity Play에 진입하면 카메라/리스너를 켜고 UnscaledGameTime으로12초 반복 미리보기를 시작한다.
- 네트워크가 실행 중이거나 여러 씬이 로드되어 있으면 자동 미리보기를 하지 않는다. 실제 Additive 전환은 SummonCinematicView가 계속 제어한다.
- 단독 Play 자동 시작 확인: state=Playing, time≈1.46초, UnscaledGameTime, Loop, camera=true. 사용자 확인용으로 반복 재생을 유지했다.
- 6단계 메인 실제 촉수 숨김/최종 자세/HP 타격 정렬은 아직 연결하지 않았다. 표시 연출 완성과 실제 전투 연결을 구분한다.
- 별도 원격·지연 로딩의 실제12초 연출/오디오 동기화 검증은 미실행이다.
- 카메라 컷/밝기/음량은1차 연출로 사용자 피드백에 따라 조정한다. 바다는 표시 전용 단순 재질이며 본편 파도 물리는 사용하지 않는다.


## 2026-10-30 후속: 실제 메인 씬 연출로 전환
- 사용자 요청: 별도 표시 씬 대신 메인 씬의 실제 환경을 사용한다. 브랜치 `feature/taewook`, 확인 기준 커밋 `58875d4`.
- `MainIslandFacilitiesBuilder`가 실제 로컬 섬 루트를 `SummonCinematicView.BindIsland`에 전달한다. 강민서 World 영역의 변경은 이 연결 호출이며 사용자 전환 요청 범위다.
- 새 `MainSceneSummonPresentation`과 `Assets/02.Prefabs/Boss/MainSceneSummonRig.prefab`에 촉수6개·카메라·Timeline·음향·물보라를 묶었다. 섬·바다·조명은 포함하지 않는다. 실제 환경의 카메라 설정을 이어받고, 마지막 자세는 같은 촉수 객체에 유지한다.
- `SummonCinematicView`의 Additive 로드/제거를 제거했다. 서버 준비/재생/복귀/Combat, 보호·입력 잠금·UI 상태 보존은 유지한다. 준비 실패는 개인 연출만 생략하고 공통 전투 시작까지 잠금을 유지한다.
- `TowerBattleSettings`는 씬 경로 대신 연출 프리팹을 참조하고 실제 설정 에셋에 연결했다. `TowerMatchNet`의 불필요한 씬 경로 SyncVar를 제거했다. 기존 `CinematicLoadDeadline`/timeout 필드 이름은 준비 대기 호환성을 위해 유지한다.
- 기존 별도 `SummonCinematic.unity`는 제작 이력으로 보존하고 Build Settings에서 제외했다. `TentacleIslandTest.unity`에는 실제 섬 하위에 새 프리팹과 단독 재생 Preview를 연결하고 옛 테스트 촉수는 비활성 보존했다. 프리팹에는 개발 Preview가 없다. 씬/프리팹/설정은 Unity MCP 네이티브 API로 저장했다.

### 실제 검증
- Unity Play 단독: 씬1개, Director 연속 재생, 실제 섬 하위 rig 확인. `Finish(true)` 후12초 Paused, 연출 카메라 꺼짐, 촉수 SkinnedMeshRenderer6개 활성 유지.
- 임시 KCP 로컬 호스트와 ready 플레이어·Tower fixture: 소환 승인 Preparing → Playing → Combat 확인. Playing에서 입력 잠금=true, 연출 카메라=true, 원래 카메라=false, 씬1개. Combat에서 입력 잠금=false, 연출 카메라=false, 원래 카메라=true, Timeline12초 유지. 두 번째 소환 시퀀스에서도 재생/복귀 확인.
- 검사 직후 콘솔 오류/경고0개. Play 종료로 임시 서버·플레이어·Tower fixture 제거. 테스트 씬은 Edit 모드로 열어 두었다.
- 별도 원격 클라이언트, 실제 본편 맵 생성→소환 승인 전체 흐름, 지연접속·준비 실패의 이번 경로 재검증은 미실행. 로컬 fixture 검증을 본편 통합 완료로 보지 않는다.

### 남은 일
- 본편/원격 클라이언트에서 실제 섬 바인딩·카메라 환경·UI 복구 확인.
- 등대 HP 피해 시점과 조이기 공격 연결, 주변 촉수 배 공격은 후속6~7단계다. 현재 전환은 등장 연출과 등장 후 객체 유지까지다.
- 2026-10-30 사용자 결정: 연출을 완성하고 테스트까지 마친 뒤 이전 연출 씬과 테스트용 항목을 정리한다. 그때 실제 참조와 필요한 테스트 용도를 확인해 정리 범위를 정한다. 현재 삭제하지 않는다.

## 다른 컴퓨터 재개 문서 정리
- 2026-10-30 사용자 요청으로 [인수인계](../../../handover/박태욱/2026-10-30/소환연출-메인씬-작업재개.md)에 최종 방식·변경/미추적 에셋·Git/SVN 전달·재생/통합 검증·다음 우선순위를 정리하고 pointer에 연결했다. 문서 작업만 수행했으며 커밋/푸시 및 테스트 씬 삭제는 하지 않았다.

## 주변 촉수 배치 조정
- 2026-10-31 사용자 메인 씬 시각 확인: 추가 확대·반경34 배치가 메인 씬에서 잘 보이고 배치도 마음에 든다고 확인했다. 현재 크기·배치를 유지한다. 이 확인은 실제 공격 연결 후 지형 관통 시험이나 원격 검증 완료를 뜻하지 않는다.
- 2026-10-31 추가 확대 요청: 주변5개를 직전 대비1.25배 확대(기본1.625, 작은5번1.21875), 배치 반경34로 변경하고 물보라를 맞췄다. 공격 FBX241시점의 BakeMesh(mesh,true) 정점을 TransformPoint로 변환해 기본 촉수 최대 수평 도달15.002, 작은 촉수11.251을 측정했다. 테스트 섬 Ground_Visual bounds의 보수적 외곽 반경15.642 기준으로 34-15.002-15.642=3.356단위 여유가 남는다. 현재 고정 공격 클립 샘플 기준이며 본편 지형 변경·후속 타겟 이동/루트 이동은 재검증 필요. 등장0~5.1667초63시점 16:9 투영에서 수면 위 정점886,159개 중 화면 밖0개, viewport X0.149~0.833/Y0.045~0.750. 실제 연속 재생·시각 확인과 원격 검증은 미실행. 기존 미커밋 테스트 씬 변경은 저장하거나 되돌리지 않았다.
- 2026-10-31 카메라 이동 후속: 정적인 원경이 밋밋하다는 사용자 피드백으로 등장0~5.2초에 섬 중심을 바라보며30도 회전하는 접근 이동을 추가했다. 수평 반경83→69, 높이49→39로 부드럽게 이동한다. 이후 근접 구간은 유지했다. 16:9·63시점·872,274개 수면 위 메쉬 정점 샘플에서 화면 밖0개, viewport X0.210~0.783/Y0.121~0.706 확인. 실제 연속 재생의 속도감·가림·컷 전환은 사용자 시각 확인 대기다.
- 2026-10-31 카메라 후속 수정: 확대·반경30 배치 이후 등장 구간이 잘린다는 사용자 보고로 `SummonCinematic_Camera.anim`의 0~5.2초 위치·회전을 높은 전체 구도로 변경했다. 5.2초 이후 기존 키는 유지했다. Prefab Contents의 Director를 0~5.1667초 63시점 평가하고 16:9 카메라에 수면 위 촉수 메쉬를 투영한 결과 872,274개 정점 샘플 중 화면 밖0개, viewport X0.290~0.703/Y0.230~0.699였다. 실제 Game 화면의 조명·가림·미적 구도 및 연속 재생은 미검증이다.
- 후속 사용자 요청: 주변 촉수 크기를 직전 대비 1.3배로 확대하고 중심 방향에서 -30/+35/-25/+40/-35도로 분산했다. 섬 중심 반경 30단위에 배치하고 물보라 위치·크기도 맞췄다. 공격 FBX 121시점의 BakeMesh 수평 범위 약 9.23단위(기본 크기)를 측정했다. 확대 시 약12단위, 테스트 섬 Ground_Visual XZ bounds 외곽 반경 약16단위에 여유를 더한 배치다. 샘플 사이의 극값 및 실제 본편 지형·카메라 구도는 미검증이며 물리 충돌 보장이나 공격 사거리 확정이 아니다.
- 2026-10-30 사용자 요청에 따라 `MainSceneSummonRig.prefab`의 주변 촉수 다섯 개를 섬 중심 기준 기존 반경에서 각각 5 Unity 단위 바깥으로 이동하고 부모의 전방을 섬 중심으로 돌렸다. 대응하는 물보라 다섯 개도 함께 이동했다. 등대 촉수와 공용 애니메이션은 변경하지 않았다.
- Unity Prefab API로 저장했고 테스트 씬을 읽어 새 위치·회전과 물보라 위치가 반영되는 것을 확인했다. `git diff --check` 통과. 콘솔에 MCP WebSocket 초기화 관련 메시지 1개가 있었고, 이후 에디터 조회는 성공했다.
- 연출 재생과 모델 앞면·카메라 구도의 시각 확인, 본편 및 원격 검증은 이번에 실행하지 않았다. 주변 촉수의 공격 사거리·안전지대 정책은 별도 결정 대기다.
