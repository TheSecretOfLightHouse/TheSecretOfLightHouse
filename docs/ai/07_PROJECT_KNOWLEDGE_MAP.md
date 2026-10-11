# 프로젝트 지식 맵

규칙 우선순위와 v4·문제 대장 안내는 [AI 작업 문서](README.md)를 따른다.

실제 코드·설정의 진입점을 찾기 위한 지도다. 아래 기존 문서 경로는 구현 탐색용이며 구규칙을 우선하지 않는다. 파일 존재와 동작 검증은 구분하며, 구현 근거와 미검증 항목은 [구현 로드맵](../05-implementation-roadmap.md)을 함께 확인한다.

## 기능별 진입점

경로는 저장소 루트 기준이다. 아래는 기능별 탐색 경로다. 개별 파일 이동 결과는 [아키텍처](../04-architecture.md)와 실제 작업 트리를 확인한다. 이 표는 Play Mode·멀티플레이 동작 검증 완료를 뜻하지 않으며 실제 런타임은 미검증이다. 각 기능 안의 Rules·Net·Server·View는 해당 기능 담당자가 함께 책임진다.

| 찾는 것 | 공유 문서 | 실제 진입점 | 확인할 것 |
|---|---|---|---|
| 게임 규칙·미정 항목 | [기획서](../design/references/game-design-v4.md), [시스템 개요](../01-system-design.md) | `docs/design/references/game-design-v4.md` | 시민 3 vs 마피아 1, 4인 호스트, 확정·가안·미정 구분 |
| 기술 방향 | [기술설계서](../03-technical-design.md), [아키텍처](../04-architecture.md) | `Assets/01.Scripts/` | 기존 구현을 조사하고 후속 계획과 구분 |
| 첫 실행·패키지 | [Unity 시작](../unity/UNITY_START_HERE.md) | `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json` | Unity 6000.0.83f1, 실제 패키지 버전 |
| 코드 스타일 | [코딩 컨벤션](../coding-convention.md) | 현재 변경 파일 | 기존 스타일 유지 |
| 폴더·어셈블리 | [아키텍처](../04-architecture.md) | `Assets/01.Scripts/`, `Lighthouse.Runtime` | 단일 런타임, Unity.InputSystem·Mirror 참조. 구조 변경과 런타임 검증은 구분 |
| 담당자·업무 경계 | [코드 소유권](../CODE_OWNERSHIP.md) | 기능별 담당·계정·수정 합의 | 다른 담당 영역 수정은 [AGENTS.md](../../AGENTS.md)의 담당 경계 확인을 따른다 |
| 공통 계약 | [코드 소유권](../CODE_OWNERSHIP.md), [아키텍처](../04-architecture.md) | `Assets/01.Scripts/00.Common/` | 공통 계약 조율과 기능 담당의 사용 범위 |
| 연결·스폰 등록·재접속 | [지훈 시스템 설계](../design/jihun-system-design.md), [네트워크 권위](../unity/NETWORK_AUTHORITY.md), [협업](../unity/SERVER_CLIENT_WORKFLOW.md) | `Assets/01.Scripts/01.Network/` (`LighthouseNetworkManager`, `ConnectionDirectory`, `NetworkActor`) | 호스트 1 + 원격 클라이언트 3, 실제 지원 계약과 검증 상태. 구현 기록은 [담당자별 작업 §6](../06-owner-work-plan.md#6-처음-함께-맞출-일과-작업-기록) |
| 바다·날씨·시간·섬·월드 배치 | [아키텍처](../04-architecture.md), [코드 소유권](../CODE_OWNERSHIP.md) | `Assets/01.Scripts/02.World/` | 파밍 스포너 배치와 플레이어 채집 행동 구분, Ghost 등장·경로·연출과 힌트 판정 구분 |
| 상호작용 라우팅·검증·기록 | [지훈 시스템 설계](../design/jihun-system-design.md), [코드 소유권](../CODE_OWNERSHIP.md) | `Assets/01.Scripts/03.Interaction/` | Relay·Router·Validator·기록·GhostShipHints |
| 등대·작업대·모듈 | [지훈 시스템 설계](../design/jihun-system-design.md), [기획서](../design/references/game-design-v4.md), [코드 소유권](../CODE_OWNERSHIP.md) | `Assets/01.Scripts/04.Lighthouse/` | 모듈·부식 낙하·등대 포함, 기획값 변경 없이 구현 근거 확인 |
| 플레이어·배·능력·인벤토리 | [아키텍처](../04-architecture.md), [코드 소유권](../CODE_OWNERSHIP.md) | `Assets/01.Scripts/05.Player/` | 배 조작·내구도·채집 행동, 이동된 파일의 정확한 위치는 아키텍처에서 확인 |
| 몬스터 | [코드 소유권](../CODE_OWNERSHIP.md) | `Assets/01.Scripts/06.Monsters/` | AI·스폰 실행과 월드 배치 경계 |
| 마피아 | [기획서](../design/references/game-design-v4.md), [코드 소유권](../CODE_OWNERSHIP.md) | `Assets/01.Scripts/07.Mafia/` | 투표·심해 및 비밀 정보 수신 경계 |
| 시스템·UI·개발 도구 | [코드 소유권](../CODE_OWNERSHIP.md) | `Assets/01.Scripts/08.Systems/`, `09.UI/`, `99.Dev/` (같은 루트) | Systems·UI는 최명기, Dev는 관련 기능 담당 공유 |
| 개발 씬 | [씬·에셋 안전](../unity/SCENE_AND_ASSET_SAFETY.md) | `Assets/00.Scenes/SampleScene.unity`, `Assets/00.Scenes/Myeongki/Player.unity`, `Assets/00.Scenes/Jihun/JH01TestScene.unity`(빌드 0번, 임시) | 씬 소유·역할·빌드 포함 여부는 실제 설정에서 확인 |
| 에셋 데모 | [데모 가이드](../unity/DEMO_GUIDE.md) | `Assets/ThirdParty/_Demo/` | 실제 로컬 체크아웃 존재와 공유 절차 확인 |
| 검증·재현 | [검증 플레이북](03_VERIFICATION_PLAYBOOK.md), [멀티플레이 테스트](04_MULTIPLAYER_TEST_PROTOCOL.md) | 실제 테스트 씬·빌드·로그 | 호스트 1 + 원격 클라이언트 3, 비밀 정보 수신 경계 |
| AI 작업 운영 | [AI 문서](README.md), [AGENTS.md](../../AGENTS.md) | `docs/ai/`, `.agents/skills/`, `.claude/skills/` | 범위·승인·실제 담당·인수인계 |

## 확인 기준

- 코드가 있다는 이유로 기능·시각·네트워크 검증을 완료로 쓰지 않는다.
- 담당자·계정·세부 업무 경계는 [코드 소유권](../CODE_OWNERSHIP.md)만을 기준으로 확인한다. 김민성의 AX·문서·자동화 역할을 게임 코드 담당으로 확대하지 않는다.
- 리슨 호스트는 서버 판정과 로컬 클라이언트를 함께 실행한다. 별도 서버 전용 폴더나 `UNITY_SERVER` 분리 구조를 가정하지 않는다.
- 숨겨야 할 역할·투표·소환 출처는 화면뿐 아니라 수신 데이터 경계까지 확인한다.
- 수치의 설계값과 실행값이 다르면 각각의 근거를 기록하고 기획을 임의 변경하지 않는다.
- 공유 문서는 개인 로그에 의존하지 않고 자체적으로 재현 가능한 근거를 제공한다.
- 새 문서·진입점·구현 상태가 확인되면 해당 행을 갱신한다.
