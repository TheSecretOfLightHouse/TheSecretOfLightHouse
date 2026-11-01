# develop 병합
작성자: 최명기
작성일: 2026-10-31
갱신일: 2026-10-31
상태: Git 병합 완료·Unity 실행 검증 대기
브랜치·기준 커밋: feature/myeongki, 7f71c12a → eee10464

## 요청과 목표
현재 브랜치에 최신 develop을 병합하고 기존 미커밋 작업을 보존한다.

## 진행 과정과 결정 이유
- origin/develop fetch 완료. 기존 추적/미추적 변경은 codex-before-develop-merge-2026-10-31 stash로 보관 후 병합했다.
- PlayerInputReader: E 해제 이벤트 정리와 develop의 ClearInput을 함께 유지했다.
- PlayerWreckRecovery: 조명탄 상태 처리와 develop의 RecoveryTime 기반 복구/재시도 시간을 유지했다. 조명탄의 복구 기한 비교도 같은 시간 기준으로 맞췄다.
- 병합 커밋 eee10464 생성 후 stash apply. BoatController의 연출 입력 잠금과 기존 대포 발사 후 이동 정지를 함께 유지하여 복원 충돌을 해결했다.
- 복원된 기존 변경은 미스테이징 상태로 유지했다. 기존 작업은 병합 커밋에 포함하지 않았다. stash는 복구용으로 남겼으며 push하지 않았다.

## 변경 파일과 결과
- 충돌 해결: PlayerInputReader.cs, PlayerWreckRecovery.cs, BoatController.cs.
- develop의 기존 변경을 병합했으며 Unity YAML 수동 편집은 하지 않았다.
- 본 기록과 담당자 POINTER.md 갱신.

## 검증
- origin/develop이 HEAD의 조상임을 git merge-base --is-ancestor로 확인(종료0).
- 미해결 Git 충돌 없음. Assets/01.Scripts와 docs의 충돌 표시 검색 결과 없음.
- 직접 해결한 3개 코드의 git diff --check 통과.
- develop에서 들어온 Unity 직렬화 파일 등에 기존 공백 경고가 있어 전체 staged diff --check는 통과하지 않았으며 해당 파일은 수정하지 않았다.
- Unity 프로세스 존재 확인. 이번 작업에서 에디터 컴파일·Play·다인 테스트는 실행하지 않았다.

## 남은 일과 차단 조건
Unity 컴파일과 연출 중 입력 잠금·난파 복구·대포 이동 정지 회귀 확인 필요.

## 다음 행동
사용자가 기존 미커밋 작업을 검토하고 Unity에서 확인한다. stash 삭제·push는 별도 요청 시 수행한다.
## 2026-10-31 추가 develop 병합
- 사용자 요청으로 feature/myeongki에 로컬 origin/develop(ce20fbc4)을 병합. 병합 커밋 288e1f9e, push 없음.
- 미커밋 9개를 stash 메시지 codex-before-develop-merge-2026-10-31-current로 보관 후 병합. Git 병합 자체는 자동 완료.
- stash apply 중 MainGame.unity와 Island_Lighthouse.asset의 Unity merge driver가 실패. 두 파일은 병합된 HEAD 버전을 유지하고, 기존 로컬 수정은 stash에 보존했다. 두 파일의 로컬 수정은 작업 트리에 재적용하지 않았다.
- 나머지 7개 변경은 unstaged로 복원. stash 삭제 없음.
- 검증: git ls-files -u 출력 없음, origin/develop이 HEAD 조상임 확인. Unity 컴파일/Play/M2는 실행하지 않음.
- 다음: 필요하면 MainGame 선택창 크기/아이콘과 등대섬 로컬 프리팹 참조를 stash와 비교해 Unity 에디터에서 재적용.

## 2026-10-31 두 파일 양쪽 변경 통합 복원
- 사용자 요청: develop 변경과 stash의 기존 수정 모두 유지. 전체 파일 복원은 실행하지 않았다.
- MainGame은 병합된 씬을 Unity 에디터로 열어 EmergencyRepairSelectionPanel 너비 800/690/154, InventoryChestScreen의 나무/돌/섬유 아이콘 3개를 재적용·저장했다. Unity 저장으로 신규 직렬화 기본값 및 정리된 prefab override 등도 diff에 포함된다.
- 등대섬 데이터의 동일 _prefab 필드는 develop FFinalBindings 프리팹과 로컬 NaturalFire 프리팹이 경합했다. 어느 한쪽을 버리지 않도록 Island_Lighthouse_IntegratedFire.prefab variant를 Unity API로 만들었다. develop FFinalBindings/Island_Lighthouse.prefab을 상속하고, 기존 로컬 모닥불 연출 3개를 복사했다. develop 모닥불 위치에 맞게 오프셋(-5.10,0.25,-1.30)을 적용하고 기존 큐브 불꽃4개를 비활성화했다. Island_Lighthouse.asset은 새 통합 variant를 참조한다. 두 원본 프리팹은 수정하지 않았다.
- 검증: 저장 성공, 선택창/아이콘 참조 재조회, variant 기반 경로 확인, develop Transform1475개+추가3개=1478개 확인, 연속불꽃/연기 활성 확인. Git 미해결 충돌 없음. stash 백업 유지, 커밋/push 없음.
- 작업 중 첫 씬 저장 시 Unity 크래시가 발생해 재시작 후 단계별 재적용·저장 성공. 임포트로 새로 바뀐 무관한 meta5개는 작업 전 HEAD 내용으로 복구했다. 기존 사용자 meta 변경은 유지.
- 미검증: 실제 Play에서 MainGame UI와 통합 등대섬 위치/모닥불 외형, 다인 실행. 기존의 두 파일 stash에만 보관·재적용 대기 상태는 이번 결과로 대체한다.

## 2026-10-31 등대섬 develop 버전 채택
- 최신 사용자 결정: 해당 작업은 본인 작업이 아니므로 모닥불 변경을 제외하고 develop 등대섬을 사용.
- Unity API로 Island_Lighthouse.asset을 develop FFinalBindings/Island_Lighthouse.prefab에 연결하고 저장. 이번에 만든 IntegratedFire.prefab과 meta는 AssetDatabase.DeleteAsset으로 제거. 기존 NaturalFire 원본은 미수정.
- git diff --exit-code origin/develop -- 해당 데이터 파일 종료0, 신규 variant/meta 부재 확인. MainGame 수정 및 다른 기존 변경·stash 유지. Play 미실행.


## 2026-10-31 최신 develop 재병합과 MainGame 최소 복원
- 사용자 요청에 따라 최신 origin/develop(0b28ce80)을 병합, 커밋 e42198d1. MainGame만 별도 stash(codex-maingame-before-develop-0b28ce80) 백업하고 나머지 로컬 변경 유지.
- Unity 에디터 저장 중 재크래시로 UnityYAMLMerge 전용 도구 사용. 최초 백업의 원본/로컬 수정과 최신 HEAD 씬을 3-way 병합, 종료0.
- 최종 MainGame diff는 선택창 너비800/690/154 추가와 자원 아이콘3개 교체만 존재. 이전 에디터 저장의 불필요한 직렬화 변경은 제외. develop 변경 유지, 모닥불 변경 제외 유지.
- Git 미해결 충돌 없음, origin/develop 조상 확인. MainGame은 unstaged 상태이며 다시 병합할 때 겹치는 수정이 있으면 커밋/보관이 필요함. stash 삭제/push 없음. Unity 실행/Play는 크래시로 미검증.

## 2026-11-01 MainGame·빌드 설정 양쪽 변경 병합
- 사용자 요청으로 origin/develop a5c76ea7을 feature/myeongki에 --no-commit --no-ff 병합. 커밋/push 없음. MERGE_HEAD가 남는 커밋 대기 상태다.
- 작업 전 로컬 변경 15개를 stash 4e8719f6fc87c80fc99e2aecf02b87f29b8f79e5(codex-before-maingame-develop-merge-2026-11-01)에 백업, 삭제하지 않았다.
- stash 복원에서 MainGame/EditorBuildSettings 충돌. MainGame은 원본·로컬·develop을 확장자 있는 임시 파일로 추출하고 UnityYAMLMerge 6000.0.83f1로 병합(종료0). YAML 직접 편집 없음. 도구에 따른 따옴표·줄바꿈 정규화 포함.
- 빌드 설정은 Unity API로 로컬 목록 순서/활성을 유지하고 develop 신규 JH04Offline을 추가(15개·중복 없음). 활성 Build Profile과 globalScenes에 반영했다. 개발 씬이 시작 씬인 기존 로컬 설정을 보존했다.
- 검증: MainGame additive 로드 성공, 루트59/Transform973/Missing Script0, 선택창800/690/154 확인 후 저장 없이 닫음. 자원 아이콘3개 GUID 보존 정적 확인. Play·다인·빌드 실행 없음.
- 기존 무관한 로컬 변경은 보존. 이번 병합 결과 검토 후 사용자가 커밋할 수 있다. 에디터 CLI 업데이트는 실행하지 않음.
## 2026-11-01 병합 커밋 완료
- 후속 사용자 MERGE_HEAD 오류 해결 요청에 따라 준비된 병합 커밋 01e46ee3(chore: develop 병합과 메인 씬 로컬 변경 통합)을 완료했다. 앞 절의 커밋 대기 상태는 해소됨.
- develop 병합분과 통합한 MainGame/EditorBuildSettings만 기존 index 범위로 커밋. 무관한 로컬 변경과 작업 기록은 미커밋 보존, stash 유지, push 없음.
- 검증: MERGE_HEAD 없음, develop a5c76ea7이 HEAD 조상, 미해결 충돌/스테이징 잔여 없음. staged diff --check는 Unity 직렬화 파일 등의 후행 공백 경고로 통과하지 않음. 이번 Play/빌드 추가 실행 없음.

## 2026-11-01 등대섬 충돌 재해결 — develop 기준 양쪽 추가 보존
- 사용자 요청: develop을 기준으로 본인 변경도 추가. feature/myeongki의 MERGE_HEAD는 origin/develop e798888430cc67046773816a7d82c37bae14780d이며 로컬 develop 참조와 다르므로 실제 병합 대상 사용.
- 최초 develop 전체 채택 후 후속 요청에 따라 공통 조상·HEAD·MERGE_HEAD를 UnityYAMLMerge 6000.0.83f1에 전달, 종료0으로 양쪽 변경 병합. YAML 수동 편집 없음.
- 결과: develop의 LighthouseBeacon 추가 유지, HEAD의 Council_OpenSeaChart에 SharedMapTable 추가도 보존. develop 대비 정확히 컴포넌트 추가 13줄만 존재함을 diff로 확인.
- 해당 프리팹만 충돌 해결로 스테이징. 나머지 자동 병합 결과·기존 미커밋 변경 보존. 커밋/push 없음.
- 검증: 전용 병합 도구 종료0, 충돌 마커 없음. Unity 에디터 로드·Play·다인 실행 미실행. 남은 일은 Unity 참조/기능 확인 및 병합 커밋.
