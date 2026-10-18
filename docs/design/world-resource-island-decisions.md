# 자원·섬·맵 생성 기획 결정과 코드 구현 정리

작성일: 2026-10-17 · 담당: 강민서(World) · 범위: `Assets/01.Scripts/02.World/{Harvest, Islands, MapGeneration}`와 `Assets/02.Prefabs/World/Data/MapGeneration/`

이 문서는 맵 생성·자원·섬 작업(T13~T17) 중 정한 기획 결정과 그 결정을 구현한 코드를 한곳에 모은 것이다. 게임 전체 기획은 [제공된 게임 기획 v4](references/game-design-v4.md)와 [승패·투표 결정](win-vote-decisions.md)이 기준이며, 이 문서는 v4가 비워 둔 부분을 World 담당이 채운 내용이다. 새 결정이 v4와 충돌하면 이 문서의 해당 결정이 우선하고, 충돌 항목은 6장에 따로 적었다.

## 0. 읽는 방법

상태 표기는 팀 어휘를 따른다.

| 표기 | 뜻 |
|---|---|
| 확정 | 팀에 공유했거나 사용자가 결정한 값이다 |
| 가안 | 첫 테스트용 값이며 플레이 테스트로 조정한다. 에셋(SO) 값만 바꾸면 된다 |
| 미정 | 결정되지 않았다. 담당과 협의가 필요하다 |
| 파일 확인 | 코드·에셋이 저장소에 있음을 확인했다 |
| 실행 검증 | 에디터에서 실제로 실행해 결과를 확인했다 |

검증 범위: 아래 표의 에셋 값과 열거형·타입 정의는 2026-10-17에 저장소의 파일을 직접 읽어 확인했다(파일 확인). T13의 실행 검증(200개 시드, 재시도·실패 경로, 클라이언트 일치)은 작업 중에 에디터에서 직접 실행했고, T16·N3·N4·T17의 실행 검증은 각 작업 기록 기준이며 이 문서를 쓰면서 다시 실행하지는 않았다.

## 1. 자원

### 1.1 자원 목록 (확정, 팀 공유 완료)

| ID | 이름 | 희귀도 | 외형 범주 | 공통 자원 | 기본 매장량 (초기/최대/하루 회복률) | 나오는 곳 |
|---|---|---|---|---|---|---|
| 1 | 돌 | Common | Rock | 예 | 30 / 40 / 0.5 | 모든 섬 |
| 2 | 나무 | Common | Tree | 예 | 30 / 40 / 0.5 | 모든 섬 |
| 3 | 섬유 | Uncommon | Plant | 아니오 | (섬 표에서 정한다) | 숲 섬 |
| 4 | 철 | Uncommon | Ore | 아니오 | (섬 표에서 정한다) | 광맥 섬 |
| 5 | 유리 | Uncommon | Sand | 아니오 | (섬 표에서 정한다) | 모래 섬 |
| 6 | 수정 | Rare | Crystal | 아니오 | (섬 표에서 정한다) | 수정 섬 |

- 연료는 없다. v4 4.4·8.3의 "연료" 문구는 이 결정과 다르다(6장).
- 구리는 넣지 않았다. 필요해지면 `Ore` 범주 자원으로 에셋 하나만 추가하면 된다.
- 기본 자원(돌·나무)은 어느 섬에서든 구할 수 있고, 특화 자원은 그 자원을 주로 내는 섬에서만 나온다.
- 희귀한 자원일수록 먼 해역에 적게 배치하고, 기본 자원은 곳곳에 많이 배치한다(2장, 3장의 규칙으로 구현).

### 1.2 렌즈와 유리 (확정)

| 항목 | 결정 |
|---|---|
| 유리 | 채집하는 **원자재**다. 모래 섬·난파선 계열에서 나온다 |
| 렌즈 | 자원이 아니라 **모듈 이름**이다(등대 활성화 모듈 후보). 섬에서 렌즈를 캐지 않는다 |
| 이유 | 렌즈는 가공품이라 섬에서 채집하기 어색하고, 유리가 자원으로 자연스럽다. 작업대는 "자원 → 모듈" 한 단계라 중간 가공품을 따로 두지 않았다 |

### 1.3 자원 ID 규약 (확정)

| 규칙 | 내용 |
|---|---|
| 시작 번호 | 1부터 시작한다. 0은 "배정 없음"이다(`ResourceId == 0`) |
| 불변 | 한 번 쓴 ID는 이름이 바뀌어도 바꾸지 않고, 삭제한 뒤에도 재사용하지 않는다 |
| 중복·0 방지 | `ResourceCatalog` 검증이 중복 ID와 0 이하를 오류로 막는다 |
| 전송 | 인벤토리·작업대·네트워크에는 `ResourceId`(int)와 수량만 간다. `NodeState`는 `ushort` 두 개(자원 ID, 남은 양)로 전송한다 |

### 1.4 외형 범주와 자원의 관계 (확정)

채집 지점 오브젝트는 **외형 범주**만 갖고, 어떤 자원이 나올지는 섬 아키타입 표와 시드가 정한다. 자원 배분의 단일 출처는 아키타입 표다(지점 프리팹이 자원을 직접 지정하지 않는다).

| 외형 범주 (`HarvestSpotCategory`) | 값 | 현재 연결된 자원 | 비고 |
|---|---|---|---|
| None | 0 | - | 범주 미지정. 자원 정의에서 오류다 |
| Rock | 1 | 돌 | |
| Tree | 2 | 나무 | |
| Ore | 3 | 철 | 구리 같은 광석 자원을 에셋 추가만으로 붙일 수 있다 |
| Plant | 4 | 섬유 | |
| Sand | 5 | 유리 | |
| Crystal | 6 | 수정 | |

- 자원은 자기 범주와 같은 지점에만 배정된다. 나무 지점에서 철이 나오는 일은 없다.
- 현재는 범주와 자원이 거의 1:1이지만, 같은 범주를 쓰는 자원을 에셋 추가만으로 늘릴 수 있게(예: 석회암·화강암을 `Rock`으로) 범주를 분리했다.

### 1.5 매장량과 회복

| 항목 | 결정 | 상태 |
|---|---|---|
| 매장량 정의 | 섬 아키타입의 자원 표(`IslandResourceEntry` + `DepositSpec`)가 가진다 | 확정 |
| 현재 매장량 | 서버의 `ResourceDeposit`이 지점별로 가진다. 원격 클라이언트에는 `IslandStateNet`(SyncList)이 변경분만 보낸다 | 구현됨 |
| 초기량 편차 | 아키타입의 `시드 편차`(가안 ±10%)만큼 시드로 정한다 | 가안 |
| 하루 회복 | 일차가 바뀔 때 `IRegenPolicy`가 일부만 회복한다. 전체 리셋이 아니다 | 확정 |
| 회복 방식 | `ProportionalRegenPolicy`: 하루 회복량 = `round(최대 × 하루 회복률)`, 최대량을 넘지 않는다. 여러 날을 건너뛰면 일수만큼 누적한다 | 가안 |
| 회복량 0 반올림 | 비율이 0보다 큰데 `최대 × 비율 < 0.5`이면 하루 회복량이 0이 되어 영원히 회복하지 않는다. 현재 에셋 값은 모두 1 이상이라 문제없다 | 미정(수치 조정 후 검증기 규칙 추가를 검토) |
| 채집 결과 | `TryConsume(지점, 요청량)`은 남은 양 안에서 차감하고 `ConsumeResult`(자원 ID, 실제 지급량)를 돌려준다. 실제로 1 이상 줬을 때만 성공이다 | 확정 |
| 요청량 | 기본량 × 직군 배율의 계산은 플레이어 담당이 한다. 맵은 직군을 모른다 | 확정 |

### 1.6 자원 하한 (가안)

맵 전체에서 그 자원을 주는 섬이 최소 몇 개 이상이어야 하는지의 하한이다. 서버 검증 단계에서만 확인하고, 부족하면 시드를 바꿔 다시 만든다.

| 자원 | 최소 섬 수 |
|---|---|
| 섬유 | 1 |
| 철 | 2 |
| 유리 | 2 |
| 수정 | 1 |

- 매장량 합이 아니라 "섬 수" 기준이다. 지점 수가 섬 프리팹(T15)에 있어서 지금은 총량을 계산할 수 없다. 매장량 기준 하한은 후속이다.

## 2. 섬

### 2.1 섬 종류 (아키타입) 표

섬 종류는 `IslandArchetype`(ScriptableObject) 하나로 정의한다. 새 섬 종류는 에셋 하나를 추가하면 된다.

| ID | 이름 | 등장 구역 | 선택 가중치 | 맵당 최소 | 맵당 최대 | 같은 유형 최소 거리 | 섬 반경 | 주력 자원 (초기/최대/하루 회복률) | 상태 |
|---|---|---|---|---|---|---|---|---|---|
| 1000 | 등대 섬 | (중앙 고정, 구역 아님) | 0 | 0 | 0 | 0 | 25 | 없음 | 확정(위치), 가안(반경) |
| 101 | 일반 섬 | 내해·중간·외해 모두 | 3 | 0 | 제한 없음 | 0 | 18 | 없음(돌·나무만) | 가안 |
| 102 | 숲 섬 | 내해·중간 | 2 | 1 | 3 | 150 | 20 | 섬유 20 / 30 / 0.4 | 가안 |
| 103 | 광맥 섬 | 중간·외해 | 1.5 | 2 | 4 | 200 | 20 | 철 15 / 25 / 0.3 | 가안 |
| 104 | 모래 섬 | 중간·외해 | 1.5 | 2 | 4 | 200 | 18 | 유리 15 / 25 / 0.3 | 가안 |
| 105 | 수정 섬 | 외해 | 1 | 1 | 2 | 250 | 20 | 수정 8 / 12 / 0.2 | 가안 |

- 최대 수량 칸의 0은 "제한 없음"이다.
- 아키타입 ID는 1부터 시작하고 중복될 수 없다. 등대 섬만 1000번대를 쓴다.
- 모든 섬은 돌·나무를 공통 자원으로 가진다. 공통 자원은 아키타입 표에 적지 않고, `공통 자원` 표시가 켜진 자원이 모든 섬의 해당 범주 지점에 자동 배정된다.
- 섬 외형은 아키타입이 가진 프리팹 변형 목록(변형별 가중치)에서 시드로 고른다. 현재는 시험용 `IslandPlaceholder` 프리팹 하나만 쓴다(실제 섬 프리팹은 T15 본체).

### 2.2 "희귀할수록 멀리, 적게" 구현 방식 (확정)

| 의도 | 구현 |
|---|---|
| 희귀 자원은 먼 해역에 | 아키타입의 등장 구역 제한(수정 섬은 외해만, 철·유리는 중간 해역부터) |
| 희귀 자원은 적게 | 맵당 최대 수량(수정 1~2개, 광맥·모래 2~4개)과 선택 가중치(수정 1, 일반 3) |
| 월드마다 최소 1개 이상, N개 미만 | 맵당 최소·최대 쿼터. 쿼터를 먼저 채우고 나머지를 가중 추첨한다 |
| 기본 자원은 곳곳에 많이 | 일반 섬(가중치 3, 수량 제한 없음)과 공통 자원이 모든 섬에서 나온다 |
| 같은 자원 섬은 여러 개, 위치는 랜덤 | 포아송 디스크로 위치를 뿌리고 쿼터 안에서 종류를 배정한다 |

### 2.3 같은 유형 섬 최소 거리 (확정, 팀원 의견 반영)

같은 아키타입 섬끼리 아키타입별 최소 거리 이상 떨어뜨린다.

| 항목 | 결정 |
|---|---|
| 기준 | 같은 **아키타입**(자원 기준이 아니다. 같은 자원을 내는 서로 다른 아키타입이 생기면 그때 자원 기준으로 확장한다) |
| 성격 | 하드 제약이다. 최소 수량 단계에서 만족하는 슬롯이 없으면 서버가 시드를 바꿔 다시 만든다. 나머지 채우기 단계에서는 그 자리에 다른 아키타입이 배정된다 |
| 일반 섬 | 0으로 둔다. 일반 섬은 모든 슬롯을 채우는 안전판이라 거리를 주면 재시도가 크게 늘어난다(일반 섬 거리 60은 200개 시드 중 61개가 재시도, 90은 133개) |
| 일반 섬끼리 간격 | 구역 설정의 섬 최소 거리(내해 45, 중간 50, 외해 55)로 조절한다 |
| 효과(200개 시드 측정) | 100m 이내에 같은 유형이 붙는 경우: 숲 50/195, 광맥 34/200, 모래 33/200, 수정 5/83 → 모두 0 |

## 3. 구역과 맵 배치

### 3.1 구역 (가안, 첫 테스트용)

중앙의 등대 섬을 기준으로 동심원 3개 고리로 나눈다. 고리 반경은 맵 전체 배율(`RadiusScale`)로 한 번에 조정할 수 있고, 섬 크기와 최소 거리에는 곱하지 않는다.

| 구역 | 안쪽~바깥쪽 반경 | 섬 수 (최소~최대) | 섬 최소 거리 | 암초 수 | 암초 최소 거리 | 암초 반경 |
|---|---|---|---|---|---|---|
| 내해 | 70 ~ 150 | 4 ~ 6 | 45 | 0 ~ 0 | 25 | 6 |
| 중간 해역 | 150 ~ 300 | 5 ~ 8 | 50 | 3 ~ 6 | 25 | 6 |
| 외해 | 300 ~ 450 | 5 ~ 8 | 55 | 6 ~ 10 | 25 | 6 |

- 항해 거리 기준(맵 크기와 낮 시간 안의 항해 거리)은 배 속도가 확정되기 전이라 판단하지 못했다(미정). 반경은 SO 값과 배율 하나로 조정한다.
- 겹침 방지는 검증기가 강제한다: 구역의 섬 최소 거리는 최대 섬 반경의 2배 이상이고, 내해 안쪽 반경은 등대 섬 반경 + 최대 섬 반경 이상이다.

### 3.2 구역 해금 (확정 비율, 구현됨)

| 구역 | 해금 시점 | 계산 |
|---|---|---|
| 내해 | 처음부터 | 1일차 |
| 중간 해역 | 전체 일수의 1/3 | `ceil(비율 × 전체 일수)` |
| 외해 | 전체 일수의 2/3 | `ceil(비율 × 전체 일수)` |

- 비율은 `DayCycleConfig`의 `_regionUnlockRatios`(0.3333, 0.6667)에 있다. 기본 설정은 5일 × 낮 300초·밤 120초이며, 이 값 기준 해금 일차는 중간 해역 2일차, 외해 4일차다.
- 해금 상태는 `RegionGate`가 일차·전체 일수·비율로 모든 기기에서 계산한다(네트워크로 보내지 않는다).
- 잠긴 구역 경계는 투명 벽 대신 파도·번개·밀어내는 외력의 **폭풍벽**으로 막는다.

### 3.3 생성 파이프라인 (확정)

| 순서 | 단계 | 하는 일 | 서버만 |
|---|---|---|---|
| 100 | 구역 배치 | 등대 섬을 중앙에 두고 구역 고리를 계산한다 | |
| 200 | 섬 위치 | 구역별 포아송 디스크로 후보점을 뽑고 섞은 뒤 필요한 개수만 쓴다 | |
| 300 | 섬 종류 배정 | 제약이 큰 아키타입(등장 구역이 적은 것)부터 맵당 최소 수량을 채우고, 남은 슬롯은 무작위 순서로 가중 추첨한다 | |
| 400 | 섬 외형 | 프리팹 변형과 회전을 시드로 정한다 | |
| 500 | 암초 | 구역별로 섬과 겹치지 않게 뿌린다 | |
| 800 | 자원 하한 검증 | 1.6의 하한을 확인한다 | 예 |
| 900 | 항로 검증 | 장애물(섬·암초·등대 + 선박 여유 거리)을 격자로 막고 등대에서 모든 섬에 도달할 수 있는지 BFS로 확인한다 | 예 |
| (섬 생성 시점) | 채집 지점 배정 | 섬마다 지점의 번호와 자원을 정한다. 규칙은 `HarvestIslandSetup`(난수 스트림 `HarvestAssignment`)으로 구현했으나, 맵 생성 결과에서 섬 프리팹을 만드는 코드(T14·T15)가 아직 없어 실제 맵 흐름에는 연결되지 않았고 샌드박스 섬에서만 실행했다 | |

- 서버는 이슈가 없을 때까지 `시드 + 시도 횟수`로 최대 8번 다시 만든다. 클라이언트는 서버가 확정한 시드로 검증 단계 없이 한 번만 만든다.
- 모든 난수는 직접 만든 `SeededRandom`(플랫폼·버전과 무관)만 쓰고, 단계마다 독립 스트림(`Fork`)을 써서 단계를 끼워 넣어도 다른 단계 결과가 밀리지 않는다.
- 서버는 시드만 배포하고 각 클라이언트가 같은 맵을 계산한다. 어긋남은 레이아웃 해시와 콘텐츠 해시로 확인한다(5장).

## 4. 모듈 (제안만, 미정)

모듈과 레시피의 목록은 상호작용 담당(작업대·모듈)과 플레이어 담당(인벤토리)이 정할 항목이라 World 담당이 확정하지 않았다. 코드에도 모듈·레시피 정의가 아직 없다(저장소 검색 결과 없음). 아래는 자원 6종을 기준으로 제안한 후보이며 확정이 아니다.

| 구분 | 모듈 후보 | 재료 후보 |
|---|---|---|
| 배 업그레이드 (개인) | 선체 보강 | 나무 + 철 |
| | 돛 | 나무 + 섬유 |
| | 방수 외판 | 섬유 + 돌 |
| | 탐조등 | 유리 + 철 |
| 등대 활성화 (공유) | 렌즈 모듈 | 유리 + 철 + 수정 |
| | 광원 코어 | 수정 + 유리 |
| | 기어 장치 | 철 + 섬유 |
| | 기반 프레임 | 돌 + 나무 + 철 |

- 등대 핵심 재료(유리·수정)가 먼 해역에 몰려 있어서 외해 탐험의 동기가 생긴다.
- 연료가 없으므로 광원 코어는 수정 + 유리, 탐조등은 유리 + 철로 정했다.
- 레시피 ID·버전·슬롯·제작 중 소유 같은 작업대 계약은 상호작용 담당의 설계를 따른다. 맵은 인벤토리·작업대에 `ResourceId`와 수량만 넘긴다.

## 5. 식별·동기화 규칙

| 항목 | 결정 | 상태 |
|---|---|---|
| 지점 번호 | `SpotId = 섬 번호 << 16 \| 지점 번호`. 섬 번호는 생성 순서(등대 섬이 0), 지점 번호는 섬 프리팹의 `HarvestSpot` 계층 순서(비활성 포함)다. 프리팹 자식 순서를 바꾸면 번호가 바뀐다 | 확정 |
| `SpotId`의 용도 | 상호작용 요청의 서버 대상 식별(종류, `SpotId` → 서버 대상 번호)과 맵의 채집 판정에만 쓴다. 인벤토리·작업대에는 가지 않는다 | 확정 |
| 채집 지점의 네트워크 | 네트워크 객체가 아니다. 각 PC가 시드로 같은 지점을 생성한다 | 확정 |
| 근처 지점 탐지 | 각 PC에서 콜라이더로 `HarvestSpot` 컴포넌트를 읽는다(`HarvestSpot.Active` 목록 제공). 조회 API(`QueryNearby`)는 만들지 않는다 | 확정 |
| 서버 검증 | 클라이언트가 보낸 `SpotId`를 믿지 않고 서버가 등록된 지점인지, 거리·남은 양이 맞는지 확인한다 | 확정 |
| 서버 전용 값 | 서버 전용 시드(스포너·날씨)는 클라이언트에 보내지 않는다. 맵 시드와 두 해시만 `MapStateNet`으로 보낸다 | 확정 |
| 일치 확인 | 레이아웃 해시(섬·암초 배치)와 콘텐츠 해시(자원·아키타입·구역 설정)를 비교한다. 해시는 에셋 순서와 무관하게 ID 순으로 계산한다 | 구현됨 |
| 해시 불일치 시 | 서버가 `MapLayout`을 직접 보내는 폴백은 확장 지점만 열어 두었고 구현하지 않았다 | 후속 |
| 판 시작 조건 | 모든 클라이언트가 자기 맵의 해시를 서버에 제출해 일치해야 월드 준비 완료로 본다(`IWorldReadyGate`) | 구현됨 |

## 6. v4와 다른 점·열린 항목

| 항목 | v4 또는 기존 기록 | 이 문서의 결정 | 상태 |
|---|---|---|---|
| 연료 | v4 4.4·8.3에 "연료"가 다시 나온다 | 연료 없음 | 확정(v4 문구 갱신 필요) |
| 자원 종류 | v4는 "목재, 석재, 금속, 연료 등" 예시 | 돌·나무·섬유·철·유리·수정 6종 | 확정 |
| 자원 지정 위치 | 초기 설계는 지점 프리팹이 자원을 지정 | 지점은 외형 범주만, 자원은 아키타입 표가 정한다 | 확정 |
| 모듈 목록·레시피 | v4와 이전 기획 모두 미정 | 후보만 제안 | 미정 |
| 맵 크기·항해 거리 | v4 13장 미정 | 반경은 가안, 배 속도 확정 후 판단 | 미정 |
| 매장량 회복량 | v4 "회복량 미정" | 비율 기반 가안. 0 반올림 문제는 검증기 규칙으로 막을 후보가 있다 | 가안 |
| 특수 섬·유령선·표류 잔해의 네트워크 물체 여부 | 미정 | 해당 일감(T24·T25·T34) 착수 때 정해 알린다 | 미정 |

## 7. 코드 구현 정리

### 7.1 열거형

모든 열거형 멤버에 값을 명시했다(직렬화·네트워크·`[Flags]` 값이 순서 변경으로 밀리지 않게 하기 위함).

| 열거형 | 위치 | 값 | 용도 |
|---|---|---|---|
| `ResourceRarity` | `Harvest/Rules` | `Common = 0`, `Uncommon = 1`, `Rare = 2` | 자원 희귀도 |
| `HarvestSpotCategory` | `Harvest/Rules` | `None = 0`, `Rock = 1`, `Tree = 2`, `Ore = 3`, `Plant = 4`, `Sand = 5`, `Crystal = 6` | 채집 지점 외형 범주(1.4) |
| `SeaZone` (`[Flags]`) | `Ocean/Rules` | `None = 0`, `Inner = 1 << 0`, `Middle = 1 << 1`, `Outer = 1 << 2`, `All = Inner \| Middle \| Outer` | 구역. 아키타입의 등장 구역은 플래그 조합이다 |
| `MapGenerationStepId` | `MapGeneration/Rules` | `ZoneLayout = 1`, `IslandSlots = 2`, `IslandAssignment = 3`, `IslandAppearance = 4`, `Reefs = 5`, `ResourceFloors = 6`, `Route = 7`, `HarvestAssignment = 8` | 단계별 난수 스트림(`Fork`) 번호표. 이미 쓴 값은 바꾸지 않는다 |
| `MapChecksumResult` (`: byte`) | `MapGeneration/Rules` | `Match = 0`, `ContentMismatch = 1`, `LayoutMismatch = 2`, `BuildFailed = 3` | 클라이언트 맵 해시 비교 결과 |

### 7.2 데이터 구조 (ScriptableObject와 직렬화 클래스)

| 타입 | 위치 | 주요 필드 | 설명 |
|---|---|---|---|
| `ResourceDefinition` (SO) | `Harvest/Rules` | `_id`, `_displayName`, `_icon`, `_rarity`, `_spotCategory`, `_isUbiquitous`, `_defaultDeposit`, `_pickupPrefab` | 자원 하나. 맵·플레이어·UI가 같은 에셋을 참조한다 |
| `ResourceCatalog` (SO) | `Harvest/Rules` | `_resources[]` | 전체 자원 목록. `ID 순 정렬 조회`, ID 조회 제공 |
| `DepositSpec` (struct) | `Harvest/Rules` | `_initialAmount`, `_maxAmount`, `_dailyRegenRate`(0~1) | 매장량 사양. `IsValid`는 최대량 > 0, 0 ≤ 초기량 ≤ 최대량 |
| `IslandArchetype` (SO) | `Islands/Rules` | `_id`, `_displayName`, `_zones`, `_weight`, `_minPerMap`, `_maxPerMap`, `_minSameTypeDistance`, `_radius`, `_seedVariance`, `_variants[]`, `_resources[]` | 섬 종류(2.1) |
| `IslandVariant` | `Islands/Rules` | `_prefab`, `_weight` | 섬 외형 프리팹 변형 |
| `IslandResourceEntry` | `Islands/Rules` | `_resource`, `_weight`, `_deposit` | 섬의 주력 자원 표 항목 |
| `ZoneConfig` | `MapGeneration/Rules` | `_zone`, `_innerRadius`, `_outerRadius`, `_minIslands`, `_maxIslands`, `_minIslandDistance`, `_minReefs`, `_maxReefs`, `_minReefDistance`, `_reefRadius` | 구역 설정(3.1) |
| `ResourceFloor` | `MapGeneration/Rules` | `_resource`, `_minIslandCount` | 자원 하한(1.6) |
| `MapGenerationConfig` (SO) | `MapGeneration/Rules` | `_lighthouseIsland`, `_resources`, `_archetypes[]`, `_zones[]`, `_resourceFloors[]`, `_radiusScale`, `_maxBuildAttempts`, `_routeCellSize`, `_shipClearance` | 맵 생성 설정의 루트. 생성기는 이 에셋 하나만 받는다 |

에셋 위치는 `Assets/02.Prefabs/World/Data/MapGeneration/`이다.

| 폴더·파일 | 내용 |
|---|---|
| `MapGenerationConfig.asset` | 맵 생성 설정 |
| `ResourceCatalog.asset` | 자원 목록 |
| `ResourceDefs/Resource_{Stone,Wood,Fiber,Iron,Glass,Crystal}.asset` | 자원 6종 |
| `Islands/Island_{Lighthouse,General,Forest,Ore,Sand,Crystal}.asset` | 섬 종류 6종(등대 섬 포함) |

### 7.3 값 타입과 계약

| 타입 | 위치 | 설명 |
|---|---|---|
| `SpotInfo` | `Harvest/Rules` | 지점 조회 결과: `SpotId`, 위치, `ResourceId`, 남은 양 |
| `SpotAssignment` | `Harvest/Rules` | 지점 배정 결과: `ResourceId`, 초기량, 최대량, 하루 회복률. `IsAssigned`는 `ResourceId != 0` |
| `ConsumeResult` | `Harvest/Rules` | 채집 결과: `ResourceId`, `Granted` |
| `NodeState` | `Harvest/Rules` | 네트워크 전송용 한 지점 상태: `ushort ResourceId`, `ushort Remaining` |
| `HarvestSpotIds` | `Harvest/Rules` | `Compose(섬 번호, 지점 번호)`, `GetIslandIndex`, `GetLocalIndex` (`<< 16` 규칙) |
| `IHarvestSpotQuery` | `Harvest/Rules` | `TryGetSpot(spotId, out SpotInfo)`. 조회 전용, 기기당 구현 하나만 등록 |
| `IHarvestSpotRegistry` | `Harvest/Rules` | `TryConsume(spotId, 요청량, out ConsumeResult)`. 서버 전용 |
| `IRegenPolicy` | `Harvest/Rules` | `GetRegenAmount(최대, 회복률, 현재, 일수)`. 회복 방식을 교체하는 확장 지점 |
| `IHarvestIslandRegistrar` | `Harvest/Rules` | 서버가 섬과 지점을 등록·해제하는 계약 |
| `IRegionGate` | `MapGeneration/Rules` | `UnlockedZones`, `GetUnlockDay(zone)` |
| `IWorldReadyGate` | `MapGeneration/Rules` | 모든 클라이언트의 맵 준비 여부(`AreAllReady`)와 변경 이벤트 |
| `IMapGenerationStep` | `MapGeneration/Rules` | `Order`, `IsValidation`, `Execute(MapContext)` |

### 7.4 규칙·알고리즘 클래스

| 클래스 | 위치 | 책임 |
|---|---|---|
| `HarvestSpot` (MonoBehaviour) | `Harvest/Rules` | 지점 마커. 인스펙터에는 외형 범주만 저장하고 `SpotId`·`ResourceId`는 `Bind`로 런타임에 부여한다. `IsBound`, `Position`, 활성 목록 `Active` 제공 |
| `HarvestSpotAssigner` | `Harvest/Rules` | 지점별로 같은 범주 후보 중 가중 추첨해 자원과 시드 편차를 적용한 초기량을 정한다 |
| `HarvestSpotBinder` | `Harvest/Rules` | 섬의 지점들에 번호와 자원을 부여한다 |
| `HarvestIslandSetup` | `Harvest/Rules` | 배정과 등록을 한 번에 한다. 난수는 `HarvestAssignment` 스트림을 섬 번호로 다시 `Fork`해 섬마다 독립적이다 |
| `ResourceDeposit` | `Harvest/Rules` | 한 섬의 지점별 남은 양. 차감(`TryConsume`)과 회복(`Regenerate`), 변경 이벤트 |
| `ProportionalRegenPolicy` | `Harvest/Rules` | `IRegenPolicy`의 비율 기반 구현(1.5) |
| `HarvestSpotRegistry` | `Harvest/Server` | 서버의 조회·차감·회복 진입점(Facade) |
| `HarvestRegenDriver` | `Harvest/Server` | `DayStarted`를 구독해 마지막 회복일 기준으로 일수를 누적해 회복한다 |
| `HarvestTargetAdapter` | `Harvest/Server` | 상호작용 담당의 비네트워크 대상 어댑터 계약을 구현해 채집 요청을 연결한다 |
| `MapBuilder` | `MapGeneration/Rules` | 단계를 순서대로 실행하고 서버 재시도를 관리한다(`Build(seed, runValidation)`) |
| `MapContext`, `IslandSlot`, `ZoneRing` | `MapGeneration/Rules` | 빌드 중 단계들이 공유하는 상태 |
| 단계 7종 | `MapGeneration/Rules` | `ZoneLayoutStep`, `IslandSlotStep`, `IslandAssignmentStep`, `IslandAppearanceStep`, `ReefPlacementStep`, `ResourceFloorValidationStep`, `RouteValidationStep` |
| `QuotaAssigner`, `FillAssigner`, `SameTypeSpacing` | `MapGeneration/Rules` | 종류 배정의 최소 수량 단계, 채우기 단계, 같은 유형 거리 판정 |
| `RouteGrid` | `MapGeneration/Rules` | 항로 도달성 격자 BFS |
| `WeightedChoice`, `SeededRandom`, `PoissonDiskSampler` | `MapGeneration/Rules` | 가중 추첨, 결정론 난수, 포아송 디스크 |
| `MapLayout`, `IslandPlacement`, `ReefPlacement` | `MapGeneration/Rules` | 생성 결과를 담은 순수 데이터(Unity·Mirror 비의존). 해시는 위치를 1cm 단위로 양자화해 계산한다 |
| `StableHash`, `MapContentHasher`, `MapSeedInfo` | `MapGeneration/Rules` | FNV-1a 해시, 콘텐츠 해시, 시드와 두 해시 묶음 |
| 검증기 | `MapGeneration/Rules` | `ResourceContentValidator`, `ArchetypeContentValidator`, `MapGenerationConfigValidator`, `RouteAndFloorValidator`, `SameTypeDistanceValidator`: 에셋 설정이 잘못되면 생성 전에 오류로 막는다 |
| `RegionGate` | `MapGeneration/Rules` | 일차와 비율로 열린 구역을 계산한다(3.2) |
| `MapStateNet`, `WorldReadyMessage` | `MapGeneration/Net` | 시드·해시 동기화와 클라이언트의 준비 완료 제출 |
| `MapSeedSource`, `MapServerBuilder`, `WorldReadyTracker` | `MapGeneration/Server` | 서버의 시드 결정·빌드·준비 집합 추적 |
| `IslandStateNet` | `Islands/Net` | 섬마다 하나. 지점별 `NodeState`를 `SyncList`로 동기화한다 |

### 7.5 층 구조 원칙

| 층 | 역할 | 규칙 |
|---|---|---|
| `Rules` | 모든 기기가 같이 계산하는 결정론 로직·정의 SO·계약 | Mirror·그래픽을 모른다 |
| `Net` | 동기화 컴포넌트 | 서버는 `Rules`의 상태를 직접 부르지 않고 `Net`에 값을 기록한다 |
| `Server` | 서버만 아는 규칙 | `View`와 서로 참조하지 않는다 |
| `Sandbox` | 샌드박스 씬용 `[ContextMenu]` 검증 | 다른 층이 참조하지 않는다 |

네임스페이스는 `Lighthouse.World.{기능}.{층}`이다. 섬·자원·맵 코드는 단일 어셈블리 `Lighthouse.Runtime` 안에 있고 asmdef 참조를 바꾸지 않았다.
