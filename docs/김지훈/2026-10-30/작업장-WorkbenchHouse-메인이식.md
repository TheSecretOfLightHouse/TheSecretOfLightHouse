# 작업장 WorkbenchHouse 프리팹화와 메인 섬 이식

작성일: 2026-10-30 · 브랜치: feature/jihun · 커밋 `bf433a3`(push 안 함)

## 배경·결정
- 사용자 결정: 등대섬 작업장 외형은 김지훈 디자인(JH04 가판대)으로 완전 교체한다. 메인 씬 작업은 김지훈이 맡는다. 강민서에게는 사용자가 직접 알렸다.
- 기존 작업장(`MainIslandAppearance`의 `Workshop_*`)은 소품이 어울릴 수 있어 삭제하지 않고 비활성화만 했다(사용자 결정).
- 묶을 범위: 가판대·작업대 모델·작업대 등불·랜턴(조명 2)·거리 등불(조명). 범선은 제외했다.
- 위치: 메인 등대섬은 생성식이라 씬 배치가 아니라 `MainIslandWorkbench.prefab` 안에 넣었다. `MainIslandFacilitiesBuilder`가 `WorkbenchAnchor`에 생성하므로 위치가 따라간다. 제작 연출의 작업대 참조도 프리팹 안에서 연결된다.

## 변경
| 파일 | 내용 |
|---|---|
| `Assets/02.Prefabs/Lighthouse/WorkbenchHouse.prefab`(신규) | 기준점 = 작업대 루트. 모델 (0,-0.5,0), 가판대 (-0.56,-0.62,-0.37), 랜턴 (-1.24,0.09,0.25), 거리 등불 (-5,-0.61,0.28) |
| `Assets/00.Scenes/Jihun/JH04TestScene.unity` | `Workbench` 아래 낱개 오브젝트를 `WorkbenchHouse` 인스턴스로 교체(월드 위치 유지) |
| `Assets/02.Prefabs/World/MainIsland/MainIslandWorkbench.prefab` | `WorkbenchHouse`(로컬 0)와 `CraftEffects`(로컬 (0,0.19,-0.14), `_workbench` = 루트 `WorkbenchNet`) 추가 |
| `Assets/02.Prefabs/World/MainIsland/MainIslandAppearance.prefab` | `Workshop_*` 23개 비활성화 |

모두 Unity MCP `execute_code`로 에디터 API를 써서 바꿨다(YAML 직접 편집 없음). 세션 시작 때 MCP 도구가 붙지 않아 `http://127.0.0.1:8080/mcp`를 직접 호출했다.

## 검증
- 실행함: 저장 후 프리팹 자식·위치·`_workbench` 참조를 다시 읽어 확인. JH04 저장 후 dirty=false.
- 미실행: Play 확인(JH04 모양 동일 여부, 메인 높이·겹침·밤 조명). 메인은 작업대 루트가 지면 +0.5m라 JH04(+0.61)와 약 0.1m 차이가 있어, 가판대가 살짝 묻힐 수 있다. 거리 등불은 작업대 -x 5m라 메인 지형 밖에 놓일 수 있다.
- 메인 빌드: 이 세션에서 예약한 빌드는 사용자 요청으로 중단했다. 빌드 산출물 확인 안 함.

## 남은 일
- 메인 Play로 작업장 높이·거리 등불 위치 조정
- MainGame에 `InGameHud`(직업 아이콘·이름·배 내구도) 배치. 런타임에 로컬 플레이어·배·작업대를 직접 찾으므로 배치만 하면 된다.
- `VictoryScreen` 배치, `VictoryScreen`·`TowerMatchEffects`의 `_match`를 생성된 등대에서 찾도록 수정
- 유령선 메인 배치: 강민서 섬 레이어 회신 후
