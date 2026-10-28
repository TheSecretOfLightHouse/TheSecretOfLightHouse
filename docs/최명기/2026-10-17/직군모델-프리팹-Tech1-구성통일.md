# 직군 모델 프리팹 Tech1 구성 통일
작성자: 최명기
작성일: 2026-10-17
갱신일: 2026-10-17
상태: 구현됨(미검증)
브랜치·기준 커밋: feature/myeongki / cc774c0 + 기존 작업 트리

## 요청과 목표
- Tech 1은 보존하고 Farm 1·Captain 1·Tanker 1에 같은 컴포넌트와 설정을 적용하되 각자의 모델과 애니메이션 리그를 사용한다.

## 진행 과정과 결정 이유
- 대상 3개는 루트 Transform과 모델·뼈대만 있었다. Tech 1의 현재 로컬 구성을 기준으로 삼았고 기존 사용자 변경을 보존했다.
- 관련 기획의 직군 세부는 R17에서 계약 확인 필요 상태이며 Tech 1의 직군 능력도 미할당이다. 이번 요청은 프리팹 구성·표현 연결로 한정하고 직군 능력이나 게임 규칙을 추가하지 않았다.
- 사용자 진행 지시 후 Unity CLI 최신 버전 업데이트를 실행하고 원본 프로젝트 경로를 명시해 Unity 6000.0.83f1 에디터에 연결했다.
- Unity ModelImporter API로 세 FBX를 Human/CreateFromThisModel로 재임포트했다. 각자의 유효한 Humanoid Avatar를 생성했다.
- PrefabUtility·EditorUtility.CopySerialized·SerializedObject로 Tech 1과 같은 루트 25개 컴포넌트(Transform 포함), 카메라 피벗, 입력·이동·물리·인벤토리·채집·네트워크 설정을 연결했다. 내부 객체 참조를 대상 프리팹으로 다시 연결하고 Animator Avatar는 각 모델의 것으로 지정했다.
- 모델·재질·스킨과 뼈대는 대상 원본을 보존했다. 루트 위치·회전·스케일은 Tech 1 기준으로 맞췄다. 씬과 런타임 코드는 저장·변경하지 않았다. YAML 직접 편집 없음.
- Mirror assetId는 Tech 1 값을 복제하지 않고 대상 GUID로 계산했다: Farm 1=1588987481, Captain 1=1520491425, Tanker 1=3114722876.

## 변경 파일과 결과
- Assets/02.Prefabs/Player/Farm 1.prefab
- Assets/02.Prefabs/Player/Captain 1.prefab
- Assets/02.Prefabs/Player/Tanker 1.prefab
- Assets/04.Models/Player/Farm.fbx.meta
- Assets/04.Models/Player/Captain.fbx.meta
- Assets/04.Models/Player/Tanker.fbx.meta
- 이 원문과 담당자 pointer. 기존 신규 프리팹·모델·meta는 미추적 상태였고 커밋·스테이징하지 않았다.

## 검증
- 임시 에디터 스크립트 컴파일 성공, 세 프리팹 저장 성공.
- 세 프리팹 모두 Tech 1과 루트 컴포넌트 타입 집합 일치, 전체 하위 Missing Script=0, 끊어진/외부 preview 객체 참조=0.
- 각 모델 Avatar isValid/isHuman=true. NetworkAnimator.animator와 NetworkTransformReliable.target이 각각 대상 Animator/루트를 참조함을 확인.
- 격리한 프리팹에서 Animator.Rebind 후 Movement/Speed=1로 0.25초 평가했고 세 모델 왼쪽 허벅지 회전 변화가 약 12.60도로 확인됐다. 실제 플레이·화면 검증을 대신하지 않는다.
- Tech 1 SHA256 전후 일치: 1D91709CC9FDD987AB11C2A973F44E8A2A4A945AADBC51E97CD675B85E7CC6E0.
- console_status: compilationFailed=false, compiling=false. 콘솔 오류5/경고1이 남아 전체 무오류로 판정하지 않았다. 최근 오류 중 3개는 컴포넌트 복사 도중 PlayerController가 NetworkIdentity보다 먼저 추가될 때 발생한 OnValidate 기록이다. 최종 저장본의 NetworkIdentity 존재 및 참조 검증은 통과했고 검증 실행 중 추가 오류는 없었다. 앞선 Inspector 예외도 버퍼에 남아 있다.
- Play Mode 입력·이동·점프·시각 품질과 M2 원격 동기화는 미실행.

## 남은 일과 차단 조건
- 세 프리팹을 실제 플레이어로 사용하는 씬에서 이동·점프·채집 및 호스트/원격 애니메이션 시험 필요.
- 공용 NetworkManager의 플레이어 선택·등록 방식은 이번 요청에서 변경하지 않았다.
- 사용자 후속 요청으로 작업에 사용한 저장소 밖 임시 MatchPlayerPrefabs.cs·VerifyPlayerPrefabs.cs를 삭제했다. 두 파일 부재를 확인했고 저장된 프리팹·Avatar 설정과 기존 게임 C# 변경은 보존했다.

## 다음 행동
- 각 프리팹으로 플레이 시 조작과 모델 표시를 확인하고, 네트워크 사용 시 M2 회귀를 수행한다.

## 관련 원문·인수인계
- [기존 픽업·생성기 연결](픽업-내용물-동기화.md)

## 2026-10-25 공통 컴포넌트 최신화
- 사용자 요청: 이름에1이 붙은 플레이어 프리팹의 공통 구성을 현재 Tech 1과 맞추고 누락 컴포넌트 추가.
- MCP 확인: Play 꺼짐·컴파일 중 아님. Captain 1/Farm 1/Tanker 1에는 PlayerPassiveController, PlayerWreckRecovery, InteractionTarget, PlayerRescuePrompt가 공통 누락.
- Unity PrefabUtility/CopySerialized/SerializedObject로 세 프리팹에4종 추가, 기존 공통 컴포넌트 설정 동기화·내부 객체 참조 대상별 재매핑. 카메라 피벗/앵커 로컬 변환 동기화. Animator 공통 설정만 맞추고 모델·Avatar·NetworkIdentity·직군별 패시브 참조 유지. Tech 1의 시험용 NavigatorPassive를 일괄 복제하지 않음.
- 실제 검증: 저장 후 재조회에서 세 프리팹 공통 누락0, Missing Script0, Tech 1 내부 잘못된 참조0, 카메라 피벗 자기 참조 정상, 각 Avatar isValid/isHuman=true. assetId Captain1520491425/Farm1588987481/Tanker3114722876 유지.
- Tech 1 SHA256 전후 동일: 96C02A1AD8083A8FE273C195595E543847E5BBD25073E803000138FD63B685E1. 기존 사용자 C#/Tech 1/Build Profile 변경 보존. YAML 직접 편집·씬 저장·커밋 없음.
- 변경 파일: Assets/02.Prefabs/Player/Captain 1.prefab, Farm 1.prefab, Tanker 1.prefab 및 본 기록/담당자 pointer.
- 남은 일: 세 프리팹 직군 패시브 슬롯은 미할당 유지. 채집가 GathererPassive20 연결·실제 스폰/입력/구조/M2 검증은 별도 후속. 이번 Play·다인 실행 미실행.
