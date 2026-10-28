# develop 병합과 로컬 작업 보존
작성자: 최명기
작성일: 2026-10-28
갱신일: 2026-10-28
상태: 파일 병합 완료·Unity 실행 검증 미실행
브랜치·기준 커밋: feature/myeongki, fae7bd89 → origin/develop 36d5fcff

## 요청과 목표
- 로컬 수정과 origin/develop 변경을 모두 보존하며 병합한다. 사용자가 병합 대상과 담당자를 확인했다.
- 후속 사용자 요청에 따라 Unity CLI 업데이트와 에디터 실행 검증은 수행하지 않고 파일 병합까지만 진행한다.

## 진행 과정과 결정 이유
- 시작 상태는 병합 진행 중이 아니라 미커밋 변경에 의한 병합 시작 거절이었다.
- 수정·미추적 파일 129개를 저장소 밖에 복사하고 SHA-256 목록을 남겼다. 이어 `recovery-before-origin-develop-2026-10-28` stash로 미추적 파일과 meta까지 보존했다.
- 원격 fetch 후 origin/develop을 fast-forward로 반영했다. 새 작업 커밋이나 병합 커밋은 생성하지 않았다.
- stash를 삭제하지 않고 apply했다. PlayerController.cs와 배 프리팹 4개에서 충돌이 발생했다.
- PlayerController는 원격의 월드 준비·로컬 물리 제어와 로컬의 난파 분리·부력 전환 준비를 함께 유지했다. 중복 using을 제거하고 FixedUpdate에서 월드 준비 갱신 후 난파 부력을 갱신하도록 병합했다.
- 기존 Git 병합 드라이버는 확장자 없는 임시 파일의 후속 병합 도구를 찾지 못했다. 원본·양쪽 버전을 `.prefab` 확장자로 별도 추출해 프로젝트 버전의 UnityYAMLMerge로 병합했다. 프리팹 YAML을 수작업으로 편집하지 않았다.
- 원격 VFX 패키지와 VFXManager 설정은 로컬과 같아서 별도 선택 없이 유지됐다.
- 기존 작업의 게임 규칙·수치·외부 원본·씬은 추가로 수정하지 않았다. 원격에서 이미 커밋된 변경은 그대로 인수했다.

## 변경 파일과 결과
- `Assets/01.Scripts/05.Player/Control/PlayerController.cs`: 충돌 구간 병합.
- `Assets/01.Scripts/05.Player/Boat/BoatController.cs`: Git 자동 병합 결과 검토, 로컬 난파 처리와 원격 준비 상태 처리 보존.
- `Assets/02.Prefabs/Player/{Boat,TankerBoat,captainBoat,gardenBoat}.prefab`: SmartMerge 결과 반영. 원격 부력 설정과 로컬 쉴드·난파 연출 변경 보존.
- 나머지 기존 로컬 작업과 새 파일은 보존. 이 기록과 담당자 pointer를 갱신한다.

## 검증
- 원본 129개 파일 누락 0. 줄바꿈을 정규화해 비교한 결과, 위 코드 2개·프리팹 4개 외 123개는 원래 로컬 내용과 동일했다(기록 갱신 전 검사).
- 프리팹 4개 각각 `로컬 → 병합 결과`의 추가·삭제 행이 `공통 원본 → 원격`의 추가·삭제 행과 정확히 일치했다. SmartMerge 종료 코드 0, 충돌 보고 없음.
- manifest와 packages-lock JSON 파싱 성공, 양쪽 VFX Graph 버전 17.0.4 확인.
- 코드·프리팹 충돌 마커 제거 확인. 프리팹 생성 직렬화의 빈 m_Name/m_EditorClassIdentifier 행 8개는 git diff --check에서 기존 로컬 작업에 있던 공백을 보고하므로 YAML을 수작업 정리하지 않았다.
- 최종 확인: Git 미해결 인덱스 0개·스테이징 0개·검사 범위 충돌 마커 0개. origin/develop이 HEAD에 포함됨을 확인했다. 빈 필드 행 끝 공백을 제외한 diff 검사 통과, 이 기록의 상대 링크 4개 존재 확인.
- Unity 컴파일·에디터 프리팹 로드·Play·M2 검증은 사용자 요청에 따라 미실행. 파일 병합 검증과 게임 동작 검증을 구분한다.

## 남은 일과 차단 조건
- Unity에서 임포트·컴파일과 배 4종의 쉴드/난파/부력 연결, 월드 준비 후 입력 해제를 확인한다.
- 기존 작업의 Play/M2 미검증 사항은 각 원문을 따른다. 병합으로 해당 검증이 완료된 것은 아니다.
- 커밋·푸시 미실행. 정상 동작 확인 전 보관 stash를 삭제하지 않는다.

## 다음 행동
- 사용자가 에디터 검증을 진행한 뒤 기존 변경과 병합 결과를 검토한다.

## 관련 원문·인수인계
- [작업 pointer](../POINTER.md)
- [난파 Player 분리와 부력 전환 준비](난파-Player분리와-부력전환준비.md)
- [경비병 쉴드 4색 연출](경비병-쉴드-4색연출.md)
- [항해사 항로 빛 유도선](항해사-항로-빛유도선.md)

## 2026-10-28 두 번째 병합
- 사용자 재요청으로 origin/develop을 다시 fetch하고 7c42cdf4에서 3a0a6f58까지 fast-forward했다. 커밋·푸시와 Unity 업데이트·실행 검증은 수행하지 않았다.
- 이번 원본 로컬 파일 136개를 별도 복사·SHA-256 목록으로 보존하고 `recovery-before-origin-develop-2026-10-28-second` stash를 만들었다. 이전 stash와 이번 stash 모두 삭제하지 않았다.
- 충돌은 BuoyancyBody.cs와 배 프리팹4개였다. 로컬의 `BuoyancyObject : BuoyancyObject` 자기 상속 선언은 원격의 `BuoyancyBody : BuoyancyObject`로 해결했다. 기존 부력 구현 이름 변경 의도는 원격에 이미 반영돼 있다.
- 원격의 BoatShipBody.cs → WaterBody.cs 이름 변경을 유지했다. 관련 로컬 타입 참조 변경은 원격과 일치해 중복 적용되지 않았다. 원격 삭제 파일을 임의 복원하지 않았다.
- 배 프리팹4개는 UnityYAMLMerge로 모두 종료코드0 병합. 로컬→결과의 추가/삭제 행이 원본→원격과 정확히 같음을 비교했다. YAML 수작업 편집 없음.
- 검증: 원본136개 누락0(BoatShipBody는 WaterBody로 대응), 내용 차이는 이름 변경 코드1개·선언 충돌 코드1개·프리팹4개뿐이며 나머지130개 로컬 내용 보존. 런타임 C#에서 이전 BuoyantObject/BoatShipBody 참조 검색 결과0. 행 끝 빈필드 공백 제외 diff 검사 통과. 충돌 마커 없음, 충돌 인덱스는 해제하고 미스테이징 상태로 정리했다.
- 남은 검증은 Unity 컴파일·프리팹 로드·실제 배 부력/쉴드/난파·M2이며 이전 사용자 요청대로 이번에도 미실행이다.
