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

## 다른 컴퓨터 재개 문서 정리
- 2026-10-30 사용자 요청으로 [인수인계](../../../handover/박태욱/2026-10-30/소환연출-메인씬-작업재개.md)에 최종 방식·변경/미추적 에셋·Git/SVN 전달·재생/통합 검증·다음 우선순위를 정리하고 pointer에 연결했다. 문서 작업만 수행했으며 커밋/푸시 및 테스트 씬 삭제는 하지 않았다.
