# 대포 Synty 발사 효과 교체
작성자: 최명기
작성일: 2026-10-29
갱신일: 2026-10-29
상태: 구현됨(미검증)
브랜치·기준 커밋: feature/myeongki / 0d3773bd

## 요청과 목표
- Synty PolygonParticleFX의 FX_CannonShot_01로 발사 효과 교체.
## 진행 과정과 결정 이유
- 사용자 담당자 확인, CLI 현재 버전 유지 요청 반영.
- 기존 pointer의 2026-10-26 발사 피드백 원문은 현재 체크아웃에 없음. 기술설계와 BoatCannon/BoatCannonFeedback의 서버 승인 RPC→Fired 재생 연결 확인.
- v4 대포 전투와 대조, 표현 교체에 필요한 추가 결정 없음. R11 몬스터 연결 검증은 범위 밖.
- Unity Editor Prefab API로 배 4종의 기존 섬광/연기를 Synty 중첩 효과로 교체. 기존 섬광 위치·회전 유지, 배율1. _flash에 효과 루트 연결·_smoke 해제, 모든 자식 포함 재생·Play On Awake 해제.
- 원본 Legacy Additive 머티리얼을 게임용 URP 가산 혼합 머티리얼 2개로 대체. 원본 텍스처 null 확인. ThirdParty 원본 보존.
- 사운드·카메라 반동·포탄·서버 판정 코드 유지.
## 변경 파일과 결과
- Assets/02.Prefabs/Player/Boat.prefab, captainBoat.prefab, gardenBoat.prefab, TankerBoat.prefab
- Assets/07.Materials/Player/CannonShot_FX_Default_01.mat 및 meta
- Assets/07.Materials/Player/CannonShot_FX_Material_01.mat 및 meta
## 검증
- 에디터 비Play 상태 저장·재조회: 배4종 모두 입자4개·자동재생false·유효 머티리얼·Missing Script0.
- 격리 Simulate 0.15초에서 입자192~199개 생성, Stop 후 전부 정리 확인.
- git diff --check: 최종 저장 이후 Unity가 생성한 프리팹 override의 빈 value 공백16개 경고. YAML 직접 편집 금지에 따라 수동 정리하지 않음.
- 실제 사격 화면·크기/방향·원격 M2는 미검증. 시각 확인 완료로 보고하지 않음.
- 작업 전 기존 World/Build Profile/Monsters VFX/기록 변경은 수정하지 않음.
## 남은 일과 차단 조건
- 실제 배 사격 화면과 원격 표시 확인.
## 다음 행동
- 실제 플레이에서 크기·방향 확인 후 필요시 조절.
## 관련 원문·인수인계
- [기술설계](../../03-technical-design.md)

## 2026-10-29 크기 축소·둥근 연기 후속
- 사용자 요청으로 배4종 효과 루트 배율0.8(20% 축소), 입자 Scaling Mode를 Shape에서 Hierarchy로 바꿔 입자 크기/이동도 배율을 따르게 함.
- 각진 FX_Sphere_03을 사용하는 연기/폭발3개 시스템을 매끈한 구체 메시(515정점)로 교체. 기존 메시 경계 크기1.22/0.92/1.21을 맞추고 화염 메시는 유지.
- 게임용 CannonShot_RoundSmoke.asset 및 meta 생성. ThirdParty 원본 미수정. 에디터 저장 후 배4종 재조회로 배율/메시3개/Hierarchy 확인. 실제 화면 체감은 미검증.


## 2026-10-29 추가 축소·투명도·지속시간 단축
- 사용자 피드백으로 배4종 루트 배율0.8→0.35(직전 대비 약44%), 시작 알파1→0.3 적용.
- 모든 입자 수명에0.3배 적용: 기본1→0.3초, 화염0.2→0.06초, 폭발0.5→0.15초, 연기3→0.9초. 시스템 Duration0.9초로 통일.
- Unity Prefab API 저장 및 격리1.1초 시뮬레이션 확인. 실제 사격 시각은 미검증.


## 2026-10-31 사진 참고 회색 포연
- 사용자 제공 군함 포격 사진의 짙은 회색 연기가 뭉쳐 터지고 퍼지는 느낌을 요청받아 배4종에 적용했다. 기존 v4 대포 전투/서버 승인 Fired 계약 유지, R11 몬스터 연결과 N07 이동 권위 변경 없음. 앞선 둥근 구체·가산 혼합 방식은 새 표현으로 대체했다.
- 프로젝트에 있던 CannonSmokeSoft.mat 및 CannonParticle 셰이더의 절차적 노이즈 연기를 재사용해 래스터 생성 없이 Billboard 포연으로 변경했다. ThirdParty 원본 수정 없음.
- FX_CannonShot_01: 세계 공간,10개 순간 분출,속도3.5~6m/s,수명0.8~1.4초. Smoke 자식:12개,속도1.5~3.2m/s,수명1.6~2.3초. 무작위 크기/회전·회색 농도/알파 변화·팽창·위로 퍼짐·노이즈/감속 적용. 구체 Blast 방출 해제. Flash는0.055초/크기0.65의 짧은 섬광 유지. 자동재생/반복 없음.
- Unity Prefab API로 Boat/captainBoat/gardenBoat/TankerBoat.prefab 저장. 기존 수평 반동 제거·선체 들림·소리·서버 피해/재장전·포탄 유지. C# 변경 없음.
- 실제 검증: 배4종 연기2층/세계 공간/Billboard/자동재생false 재조회, 셰이더 오류false 확인. 격리0.55초 입자22개 생성·Flash/Blast0개 확인. PreviewScene 렌더에서 불규칙한 회색 연기 덩어리와 부드러운 경계 확인. 첫 렌더는 camera.scene 미지정으로 배경만 나와 수정 후 재렌더했다. 미리보기: ../2026-10-31/대포연기-미리보기.png. 임시 씬/렌더 자원 정리.
- 상태: 구현됨(미검증). 실제 사격 시야·연속 발사·게임 조명·원격 M2는 미실행. 다음은 사용자 실제 사격에서 크기/짙기/지속 확인.

## 2026-10-31 빌보드 철회·입체 메시 연기
- 사용자 실제 화면에서 연기가 납작한 판처럼 보인다고 지적. 앞선 Billboard 적용은 사용자 의도와 불일치했으며 철회했다.
- 배4종 연기2층을 Mesh 모드/CannonShot_RoundSmoke 메시로 교체. 새 CannonSmokeVolume.mat(에디터 생성/meta 포함)의 MeshSmoke 모드를 사용한다. CannonParticle.shader에 세계 공간3축 노이즈·표면 방향 명암/가장자리 투명화·작은 표면 변형을 추가해 UV 평면 마스크 없이 입체 메시를 표시한다. 구체 겹침을 완화하기 위해 경계 알파와 밀도 노이즈를 조정했다. 연기2층 각18입자,3축 무작위 회전. 기존 서버 승인/소리/반동/피해 계약은 유지.
- 검증: 첫 셰이더 컴파일은 HLSL 예약어 point 사용으로 실패, smokePosition으로 수정 후 오류0 확인. PreviewScene 옆/위2시점 렌더에서 두께 있는 덩어리 확인(초기 메시버전), 후속 경계/표면 조정 뒤 셰이더 오류false. 미리보기 대포연기-입체-미리보기.png는 초기 메시버전이며 최종 시각 검증으로 확대하지 않는다.
- 실제 게임 사격/연속발사/낮밤·원격 M2 미검증. 다음은 사용자 시점에서 평면 현상 제거와 연기 외형 확인.
