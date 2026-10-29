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

