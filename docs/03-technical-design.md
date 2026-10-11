# 등대 기술설계서

> 기획 기준: [게임 기획서 v4](design/references/game-design-v4.md). 이 문서는 기술 계약을 다루며 게임 규칙·수치의 출처가 아니다. 미결 계약은 [설계 문제 대장](07-design-issue-register.md)에서 추적하고 원문의 미정은 임의 확정하지 않는다.

기준일: 2026-10-10(v4 반영). 기존 기획·설정·코드의 기술 경계를 정리한다. 네트워크 기능 전체의 구현 완료를 뜻하지 않는다.

## 1. 목적과 적용 범위

[제공된 v4](design/references/game-design-v4.md)의 4인 호스트-클라이언트 구조와 비밀 정보 보호를 유지한다. v4 4.8은 직군·마피아, 소환 여부, 부식 여부, 미래 날씨, 환영 섬 대상을 당사자에게만 보내는 비밀 정보로 정한다. 백엔드·전용 서버·영구 저장 체계는 현재 확인된 기획 범위에 없다. 도입이 필요하면 요구사항과 기술 방향을 별도로 결정한다.

## 2. 실제 기반

| 항목 | 확인한 값 | 파일 |
|---|---|---|
| Unity | 6000.0.83f1 | `ProjectSettings/ProjectVersion.txt` |
| URP | 17.0.4 | `Packages/manifest.json` |
| 입력 | Input System 1.19.0 | `Packages/manifest.json` |
| 네트워크 | Mirror 96.0.1 | `Assets/Mirror/version.txt` |
| 테스트 패키지 | Unity Test Framework 1.6.0 | `Packages/manifest.json` |

패키지 설치는 프로젝트 테스트 통과를 뜻하지 않는다. 버전 변경 시 실제 파일과 이 표를 함께 갱신한다.

## 3. 실행 구성

리슨 호스트 한 명과 원격 클라이언트 세 명이 한 판을 구성한다. 각 담당자는 자기 기능의 서버 판정·동기화·클라이언트 표현을 함께 구현하며 [담당 경계](CODE_OWNERSHIP.md)를 따른다. 공용 접속 기반과 4인 안정화 테스트는 김지훈이 담당한다. 게임 규칙 판정은 호스트의 서버 측에서, 입력과 표현은 각 클라이언트에서 처리하는 방향이다. 플레이어 제어 코드는 현재 `MonoBehaviour` 기반이며 네트워크 권한 처리가 완료된 것으로 보지 않는다.

## 4. 권위와 통신

| 구분 | 책임 |
|---|---|
| 호스트 서버 | 게임 진행·승패·몬스터 AI·비밀 투표 등 공유 규칙 판정 |
| 클라이언트 | 입력·카메라·UI·연출, 허용된 요청 전달 |
| 네트워크 계약 | 요청과 동기화 데이터. 클라이언트의 결과 주장 자체를 신뢰하지 않음 |

Command 인자의 권한·범위·쿨타임은 서버에서 검증한다. 비밀 투표 결과는 본인과 시스템만 알며 역할 등 비밀 정보는 전체 SyncVar/ClientRpc로 보내지 않는다. 필요한 정보만 해당 대상에게 전달한다. 몬스터의 소환/자연 스폰 여부는 클라이언트에 보내지 않는다. 상세 운영 규칙은 [네트워크 권위](unity/NETWORK_AUTHORITY.md)를 따른다.

호스트 자신은 서버 상태를 가진다. 원격 클라이언트 정보 제한이 호스트 운영자의 메모리 접근까지 막는다는 보장을 뜻하지 않는다.

## 5. 현재 요청·데이터 계약

- `Assets/01.Scripts/03.Interaction/Requests/Net/InteractionRequest.cs`: ActorId, TargetId, Action, Arg0, Arg1, ServerTime을 가진 읽기 전용 구조체다.
- `Assets/01.Scripts/03.Interaction/Requests/Net/InteractionAction.cs`: 상호작용 종류를 정의한다.
- `Assets/01.Scripts/02.World/Harvest/Rules/ResourceDefinition.cs`: 자원 ID·표시명·아이콘·희귀도·픽업 프리팹의 ScriptableObject 정의다.

이 타입의 존재는 요청 전송·검증·실제 데이터 연결의 완료를 의미하지 않는다. 아직 구체적인 전송·실패 응답 계약은 이 문서에서 새로 확정하지 않는다.

송신자와 ActorId의 관계는 JH01에서 다음과 같이 구현했다. 상세 설계는 [지훈 시스템 설계 §5.1·§8](design/jihun-system-design.md)을 따른다.

- ActorId는 서버가 플레이어 생성(`OnServerAddPlayer`) 때 1부터 발급한다. 0은 무효다. 연결 번호(connectionId)·객체 번호(netId)와 분리된 주체 번호다.
- 서버 코드는 Command 인자나 `InteractionRequest.ActorId`처럼 클라이언트가 보낸 ActorId를 신뢰하지 않는다. `LighthouseNetworkManager.Instance.Connections.TryGetActorId(connectionToClient, out actorId)`로 송신자를 조회하거나 `IsSender`로 대조한다.
- 플레이어 객체의 `NetworkActor.ActorId`는 모든 클라이언트에 공개된다. 역할 등 비밀 정보와 연결되지 않는다.
- 타임스탬프(`ServerTime`) 검증과 요청 번호·연결 세대는 JH02에서 정한다. 나간 사람의 ActorId 보존·재결합은 JH07 범위이며 현재는 해제 시 명단에서 제거한다.

## 6. 상태와 실행값

게임 단계, 투표, 점화, 소환과 승패 조건은 [제공된 v4](design/references/game-design-v4.md)가 기준이다. 구현 시 가안 수치와 확정 규칙을 구분한다. 판 길이 값(일수 기본 5일, 낮 5분·밤 2분, 귀항 알림, 해금 비율 1/3·2/3)은 v4 11장대로 설정값 한 곳에서 바꿀 수 있게 둔다. 직군 세부·레시피·회복량 등 미정 항목은 작업 브리프에 입력 대기로 적는다. 문서 재편을 이유로 기본값을 확정하지 않는다.

## 7. 장애·접속과 남은 결정

재접속, 호스트 이탈·이전, 중도 참가, 저장·복구의 실제 통합 방식은 v4에도 정의가 없어 확정하지 않는다(대장 N11~N13). 배 물리 권한(배 주인 클라이언트/서버)도 v4 13장의 결정 필요 항목이다(N07). 해당 기능 착수 전에 요구사항과 실패 경로를 결정해야 한다. 기존 로컬 코드가 이 동작을 제공한다고 가정하지 않는다.

## 8. 검증과 통합

[검증 플레이북](ai/03_VERIFICATION_PLAYBOOK.md)과 [멀티플레이 테스트](ai/04_MULTIPLAYER_TEST_PROTOCOL.md)를 따른다. 입력·승하선·난파의 로컬 동작, 호스트 판정, 원격 관찰, 비밀 정보 전달 범위를 각각 확인한다. 지정 버전 Unity 6000.0.83f1의 컴파일·Play Mode·다인 접속은 확인 전까지 실행 완료로 표시하지 않는다.

## 9. 관련 문서

[아키텍처](04-architecture.md), [서버·클라이언트 협업](unity/SERVER_CLIENT_WORKFLOW.md), [구현 로드맵](05-implementation-roadmap.md).
