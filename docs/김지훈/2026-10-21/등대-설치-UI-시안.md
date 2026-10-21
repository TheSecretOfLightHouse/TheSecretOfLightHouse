# 등대 설치 UI 이미지 시안

작성자: 김지훈
작성일: 2026-10-21
갱신일: 2026-10-21
상태: 완료(이미지 생성·육안 확인)
브랜치·기준 커밋: `feature/jihun`, `5a8a4f3`

## 요청과 목표

최신 v3는 작업대 참조 이미지의 갈흑색 카드, 두꺼운 미색의 불규칙한 테두리, 평면 노란 강조, 키캡, 소문자 글꼴과 모듈 아이콘 형태를 따르는 등대 설치 UI 시안이다. 중앙 등대 모양 카드에 4행·8슬롯, 우측 carrying·gear·install [F], 하단 3/8 진행 막대, 우상단 [F] install·[ESC] close를 배치한다. floor 1의 frame·gear와 floor 2의 frame은 설치, floor 2의 빈 gear는 노란 테두리와 here 화살표, 상층 4칸은 희미한 해당 모듈 아이콘과 작은 미색 잠금으로 표현한다. 렌즈는 참조의 청록색 유리를 유지한다.

v1·v2는 우측 등대 카드·상단 진행 막대·하단 2칸 핫바를 사용한 이전 시안이며 보존한다. v3는 최신 요청의 중앙 배치로 구분한다.

## 진행 과정과 결정 이유

- 2026-10-21: 사용자가 기록 담당자를 김지훈으로 확인했다. 전체·담당자 pointer와 8슬롯 시험안 원문을 읽고 현재 미커밋 변경을 확인했다.
- `imagegen` 스킬의 내장 `image_gen` 모드로 첨부 이미지를 스타일 참조하여 생성했다. 최초 결과의 진행 막대가 길어 보여 노란 채움 길이만 3/8로 줄이도록 한 차례 편집했다.
- 독립 육안 검수에서 비가 강하고 카드에 미세한 명암이 남은 점을 확인했다. 빗줄기를 줄이고 카드 바탕을 평평하게 만드는 추가 보정 후 최종 v2를 선택했다.
- `review-design-code-gaps` 스킬로 좁게 대조했다. `TowerLayout_JH04Trial.asset`에는 요청과 같은 4행·8슬롯이 시험값으로 존재하며, `TowerLayout`·`TowerNet`의 아래층 우선 장착 구조에서 해당 상태를 표현할 수 있다.
- 대장 V11은 모듈만 투입 결정·층별 배치와 환산 미정, W01은 부분 결정, W03은 손에서 장착 코드 반영·실행 미검증 상태다. 확정 모듈 결정 문서에도 층별 배치·순서·환산은 미정으로 남아 있다. 이번 요청은 이미지 제작 기준으로만 기록했다.
- 요청한 등대 카드와 F/G 핫바의 런타임 구현·바인딩 검증은 이번 이미지 작업에 포함하지 않았다.
- 2026-10-21 v3: 사용자가 기록 담당자를 김지훈으로 확인했다. 최신 요청에서 참조 2장을 언급했으나 현재 메시지에서 실제 확인한 첨부는 작업대 이미지 1장이었다. 작업대 이미지를 스타일·모듈 아이콘 참조로 지정하고, 중앙 등대·우측 카드·하단 막대·우상단 키 힌트 배치는 사용자의 상세 설명을 기준으로 내장 image_gen에서 새로 생성했다. 이전 시안의 배치·흰색 렌즈 변환·핫바는 적용하지 않았다.
- v3 좁은 기획·코드 대조: TowerLayout·TowerNet의 아래층 우선 장착·설치 개수 진행도로 요청 상태를 표현할 수 있다. V11·W01·W03의 미정·미검증 상태를 확정 규칙이나 실행 완료로 바꾸지 않았으며, 이번 요청은 시각 시안 기준으로만 기록했다.

## 변경 파일과 결과

- [최신 v3 이미지](images/lighthouse-install-ui-workbench-v3.png): 1672×941 PNG. 중앙 등대·우측 carrying·하단 진행 막대·우상단 키 힌트. [v3 최종 생성 프롬프트](lighthouse-install-ui-workbench-v3-prompt.txt), 내장 image_gen 사용.
- [이전 v2 이미지](images/lighthouse-install-ui-v2.png): 1672×941 PNG. v1은 진행 막대 보정 단계의 중간 시안이다.
- 이 작업 원문과 담당자 pointer 완료 이력에 생성 결과를 기록했다.
- 기존 씬·다른 작업자의 미커밋 변경은 보존했다. Unity 에셋·프리팹·코드를 수정하지 않았다.

## 검증

- 실행(v3): 생성 이미지와 독립 육안 검수에서 중앙 4행·8슬롯, 설치 3개·활성 빈칸 1개·잠긴 상층 4개, 노란 슬롯 테두리 총 4개, here 화살표, 우측 carrying·gear·install [F], 하단 3/8, 우상단 [F] install·[ESC] close를 확인했다. 갈흑색 카드·굵은 미색 테두리·노란 강조와 모듈 형태·청록 렌즈가 유지된다. 막대는 육안으로 약 3/8이며 정확한 픽셀 비율이나 원본 아이콘의 픽셀 단위 동일성은 검증하지 않았다.
- 실행(v3): System.Drawing으로 실제 PNG 크기 1672×941 확인. 기본 생성 경로에서 작업 기록의 images 폴더로 복사한 뒤 SHA256 일치를 확인했다. 원문·pointer·프롬프트·이미지 상대 링크와 변경 범위를 정적으로 확인했다.
- 실행(v1·v2): 생성 이미지 육안 확인. 위부터 lamp room·floor 3·floor 2·floor 1 순서, 총 8슬롯, 노란 테두리 3개·점선 빈 슬롯 5개, floor 2 gear의 화살표·install [F], 3/8 문구, 하단 gear [F]·빈칸 [G]를 확인했다.
- 실행: 진행 막대 길이 보정 후 다시 육안 확인했다. 보정 수치는 생성 프롬프트 기준이며 픽셀 단위 비율을 자동 측정한 것은 아니다.
- 실행: 독립 검수에서 8슬롯·층 순서·문구·핫바에 누락이 없음을 확인했다. 추가 보정본에서 빗줄기가 줄고 카드 바탕의 명암이 완화된 것과 기존 슬롯·문구 유지 여부를 다시 육안 확인했다. 완전한 단색 여부를 픽셀 단위로 검증하지는 않았다.
- 실행: System.Drawing으로 최종 PNG 실제 크기 1672×941 확인.
- 실행: 원문·pointer·이미지 상대 링크와 변경 범위 정적 확인.
- 미실행: Unity 컴파일·플레이·멀티플레이. 정적 이미지 시안만 생성했으므로 런타임 동작 검증 결과는 없다.

## 남은 일과 차단 조건

이미지 제작 작업의 남은 실행 단계는 없다. 사용자 스타일 검토·채택은 대기한다.

## 다음 행동

사용자의 후속 요청에 따라 시안 세부 보정 또는 UI 구현 범위를 정한다.

## 관련 원문·인수인계

- [8슬롯 시험안](../2026-10-19/JH04-등대-시험안-8슬롯-등실재점화.md)
- [모듈 결정](../../design/lighthouse-module-decisions.md)
- [설계 문제 대장](../../07-design-issue-register.md)

## v1·v2 생성 프롬프트

내장 `image_gen` 사용. 첨부 이미지를 스타일 참조로 지정했다.

```text
Use case: ui-mockup
Asset type: one landscape 16:9 game UI concept screenshot.
Input image 1: STYLE REFERENCE ONLY. Match its existing rough hand-drawn UI visual language closely; change the scene and replace the UI with the exact new layout below.

SCENE: Third-person rear view of the same sort of stylized raincoat-wearing sailor standing outdoors directly at the base of an old stone lighthouse at night. Show the large weathered stone lighthouse wall and entrance immediately beside/behind the character, with a dark sea visible behind them. Light rain. Preserve the reference's dim, moody game lighting, muted dark sea and restrained warm lamplight. Same stylized 3D game rendering as the reference. The character and lighthouse base occupy the left and middle of the scene. The right is reserved for the UI card. Do not reproduce the workbench, shelf, crafting panels, or left vertical icon menu.

UI STYLE IS STRICT: thick off-white wobbly hand-drawn outlines, dull flat dark brown-charcoal cards, plain white/off-white LINE icons, flat yellow as the ONLY UI accent, small outlined keycap badges, simple rounded handwritten lowercase lettering. Copy the imperfect strokes and blunt unpolished appearance of the reference. No new colors, no colored module art, no gradients, no gloss, no drop shadows, no bevels, no ornamental polish. Scene lighting must not add shading or glow to the flat UI.

RIGHT SIDE: ONE tall card with a continuous thick off-white wobbly outer contour, shaped loosely like a lighthouse: broad base, slightly tapering body and modest narrow lantern-room shoulders/cap at top. Keep the silhouette subtle and layout legible. Inside are EXACTLY FOUR stacked horizontal rows and EXACTLY EIGHT module slots, TWO per row. Tiny labels on the LEFT inside each row. Arrange the rows spatially from TOP TO BOTTOM as:
1. "lamp room": left slot LENS, EMPTY; right slot LIGHT CORE, EMPTY.
2. "floor 3": left slot FRAME, EMPTY; right slot GEAR, EMPTY.
3. "floor 2": left slot FRAME, FILLED; right slot GEAR, EMPTY, this is the NEXT SLOT TO FILL.
4. "floor 1": left slot FRAME, FILLED; right slot GEAR, FILLED.
Thus bottom-to-top order is floor 1, floor 2, floor 3, lamp room.

Module icons: frame is the simple domed cage/frame white line icon seen in the reference, gear is its cogwheel white line icon, lens is its oval lens reduced to plain white linework, light core is its lantern/core reduced to plain white linework.
FILLED slots: exactly THREE slots total, each containing a strong plain white line module icon and a FLAT YELLOW slot outline.
EMPTY slots: exactly FIVE slots total, each with a DASHED off-white slot outline and a FAINT low-opacity white ghost line icon of its corresponding module, visibly empty.
The floor 2 RIGHT GEAR slot is dashed and empty, not yellow-filled. Put a small off-white arrow pointing into that slot, with the exact small lowercase text "install" and an outlined keycap "F" beside it. Fit this instruction into the floor 2 row without obscuring its icon or other rows.

At the TOP INSIDE this card, above the lamp room row: a THIN progress bar with exactly three eighths (37.5 percent) filled flat yellow; remainder flat dark. Small off-white text "3/8". No additional card title.

BOTTOM CENTER: exactly the same TWO-SLOT hotbar visual style and approximate scale as the reference. First slot contains the plain white GEAR icon and a small outlined keycap "F" attached at the lower right. Second slot is EMPTY with a small outlined keycap "G" attached at lower right. Both hotbar borders off-white. No third slot.

Exact visible text only: "lamp room", "floor 3", "floor 2", "floor 1", "install", "3/8", and keycaps "F", "F", "G". All words lowercase; keycaps uppercase. No extra labels, instructions, indicators, HUD elements, title, watermark, or text.
Prioritize exact slot count, correct filled state and reference UI style.
```

## 진행 막대 보정 프롬프트

첫 생성 결과를 편집 대상으로 지정했다.

```text
Edit only the horizontal yellow progress fill at the top of the right-side lighthouse UI card. Preserve the complete image, composition, scene, all lettering, all slots and icons, colors, rain, hotbar and outlines unchanged. There is a thin horizontal rounded progress bar to the LEFT of the text "3/8". Its current yellow fill is too long. Shorten just this yellow fill so it occupies exactly THREE EIGHTHS = 37.5% of the usable inside width of that bar, measured from the bar's inner left edge. The remaining 62.5% of the inside width is the original flat dark card color. Keep the fill flat yellow and the existing thin off-white wobbly bar border. Keep the small "3/8" text unchanged. No other changes.
```

## 비·카드 바탕 보정 프롬프트

진행 막대를 보정한 결과를 편집 대상으로 지정했다.

```text
Refine this image with only these two small corrections required by the original brief:
1) LIGHT RAIN: the scene currently has many long, bright rain streaks. Remove most of those streaks, leaving only sparse thin faint short rain streaks for a clearly light drizzle. Preserve the dark sea, sky, wet stone, character, lighthouse, lantern, composition and all scene lighting unchanged.
2) FLAT UI CARD FILL: make the interior background of the right-hand lighthouse-shaped UI card one single uniform dull dark brown-charcoal color, the same flat color from top to bottom across all rows. Remove subtle gradients, mottling, lighting or shading in that card background only. Keep its thick off-white wobbly outer border, row separator lines, all slots and all icons exactly the same. The UI should look rough and flat, not polished.

CRITICAL PRESERVATION: Do not change any UI arrangement, any text, any keycap, any border shape or color. Preserve exactly 4 rows, exactly 8 slots and their present states: lamp room 2 empty; floor 3 2 empty; floor 2 frame filled and gear empty with arrow and install F; floor 1 frame filled and gear filled. Preserve the corrected thin progress bar 37.5% yellow and the text 3/8. Preserve the bottom gear hotbar F and empty hotbar G. Filled module icons remain plain off-white lines with yellow slot outlines; ghost icons remain faint plain off-white lines with dashed slot outlines. No added colors or extra elements.
```


