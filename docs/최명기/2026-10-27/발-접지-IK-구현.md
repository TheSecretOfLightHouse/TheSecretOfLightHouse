# 발 접지 IK 구현
작성자: 최명기
작성일: 2026-10-27
갱신일: 2026-10-27
상태: 사용자 요청으로 철회·원복
브랜치·기준 커밋: feature/myeongki / 54efe84

## 요청과 목표
현재 속도3/4.5를 유지하면서 발 접촉 구간 고정, 지형 높이/각도 보정, 작은 골반 보정, 점프/낙하/탑승 해제를 구현한다. 사용자 승인 범위는 먼저 한 캐릭터에서 작게 적용해 비교하는 것이다. 진행 중 사용자가 테스트를 직접 하겠다고 지시하여 추가 자동 동작 테스트를 중단했다.

## 진행 과정과 결정 이유
- 기존 PlayerAnimationDriver에는 발 IK 콜백이 없고 공통 Animator Base Layer IK Pass도 꺼져 있었다. 이동/속도 보정은 앞서 철회됐으므로 그대로 유지했다. N07 이동 소유자 권위·NetworkAnimator 계약 변경 없음.
- 신규 PlayerFootGrounding은 Animator와 같은 오브젝트에서 OnAnimatorIK를 사용한다. 캐시한 PhysicsScene의 비할당 Raycast로 자기 충돌체/트리거/충돌 제외 쌍·레이어/급경사를 제외한다.
- Unity Animator.leftFeetBottomHeight/rightFeetBottomHeight와 IK 목표 회전으로 발바닥을 계산한다. 현장 Tanker 오프셋은 약0.08228m. 발 본 높이를 발바닥으로 취급하지 않는다.
- 애니메이션 발의 상대 전후 이동과 높이로 접촉 구간을 추정한다. 접촉 중에는 지형 Transform 상대 좌표로 고정하고, 발을 들거나 고정거리0.1m 초과·45도 이상 회전·0.75m 이상 순간이동 때 고정을 해제한다. 다리 도달 범위도 별도로 제한한다.
- 지면 높이 최대보정0.16m·발 각도 최대30도·골반 최대0.06m로 제한한다. 골반은 목표 발까지 다리가 닿는 데 필요한 하강도 고려한다. 씬/루트/충돌체 위치를 움직이지 않고 애니메이션 bodyPosition만 보정한다.
- Movement 상태 밖 또는 전환/점프/낙하/탑승/난파/지면 없음에서는 IK 가중치0과 고정 초기화. 원격 표현도 복제된 Animator 상태와 로컬 지형으로 계산하며 네트워크 요청을 추가하지 않는다.
- 현 사용 캐릭터 Tanker 1 한 종에만 컴포넌트 추가. 공통 PlayerAnimator Base Layer IK Pass 활성화. 다른 직군에는 컴포넌트를 붙이지 않았다. 프리팹은 Editor API 저장, 기존 assetId·배 참조 보존 확인.

## 변경 파일과 결과
- Assets/01.Scripts/05.Player/Animation/View/PlayerFootGrounding.cs 및 Unity 생성 .meta
- Assets/02.Prefabs/Player/Tanker 1.prefab: 컴포넌트 추가만 수행. 기존 변경 보존.
- Assets/05.Animations/Player/PlayerAnimator.controller: Base Layer IK Pass0→1.
- docs/03-technical-design.md, 본 원문, 담당자 POINTER.md.

## 실제 검증과 한계
- 신규 코드 및 최종 보완 후 Unity 컴파일 failed=false/errors=[]/compilationFailed=false. diff와 .meta 존재 확인. 마지막 연결 확인은 attached=true, assetIdPreserved=true, speed=3, runMultiplier=1.5, playing=false, scenes=1.
- 초기 PreviewScene 검사에서 IK 활성0이어서 통과로 보지 않았다. 이후 실제 Play의 격리 물리 씬과 순수 FBX 모델(공용 네트워크·게임플레이 스크립트 없음)로 검사했다.
- 초기 구현180프레임에서 IK 가중치 활성180회이나 고정유지0회. Tanker 실제 다리 길이 약0.12747m로 목표가 도달 제한에 계속 걸렸다. 최저 메시 평균절대 지면오차 약0.0483→0.0336m였으나 이 초기안의 개선을 최종판 통과로 주장하지 않는다.
- 이후 골반을 다리 도달 가능 높이에 맞춰 먼저 낮추고 도달 원점을 중복 보정하지 않도록 수정했다. 추가 실행 검사는 에디터 프레임/연결 지연으로 결과 미확보. 사용자가 테스트를 맡겠다고 해 중단했다. 최종판 실제 고정·시각·점프/경사/M2는 미검증이다.
- 여러 MPP 복제 에디터가 열려 있어 본 프로젝트 경로를 확인했다. 샌드박스가 discovery 파일을 읽지 못해 승인된 unity status로 실제 본 프로젝트만 확인. 다른 담당 씬/복제 체크아웃은 수정하지 않음.
- Application.runInBackground는 확인 시 원래 true였으며 true 유지. Auto-tick은 원래 활성 상태, session-only16ms로 갱신. Play 종료로 임시 테스트 씬/오브젝트/콜백을 정리했고 최종 sceneCount1 확인. 씬 저장 없음.

## 남은 일·다음 행동
사용자가 Tanker로 평지 보행·회전·정지·경사·점프·승하선을 확인한다. Tanker의 PlayerFootGrounding 컴포넌트 활성 체크로 전후 비교 가능. 현재 속도에 비해 짧은 보폭이므로 완전한 미끄러짐 제거를 보장하지 않고, 다리가 늘어나기 전 고정을 해제하는 정책이다. 사용자 피드백 전 다른 직군 확대·추가 속도/모션 조정·자동 테스트는 진행하지 않는다. 커밋/푸시 없음.

## 관련 원문
- [현장 관찰·접지 수정](플레이어-충돌-초기조사.md)
- [속도 조정 철회](보행-발미끄러짐-보정.md)

## 사용자 이상 피드백 후 원복
- 사용자가 이상 동작을 보고하고 원복 요청. Tanker의 PlayerFootGrounding 제거, 공통 Animator Base Layer IK Pass=false 복구, 신규 스크립트와 meta를 AssetDatabase.DeleteAsset으로 함께 제거했다.
- 걷기3/달리기4.5·기존 클립·이전 접지/경사 수정·기존 배 연결은 유지했다. 추가 조정/동작 테스트 없음. 씬 저장/커밋/푸시 없음.
- 도구 응답 timeout 뒤 디스크에서 스크립트 부재·프리팹의 스크립트 GUID 참조 부재·IK Pass0 확인. 재컴파일 failed=false/errors=[]/compilationFailed=false. 사용자 체감 문제는 미해결이며 이번 IK는 채택되지 않았다.
