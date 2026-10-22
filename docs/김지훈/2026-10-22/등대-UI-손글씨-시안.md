# 등대 UI 손글씨 시안 v4·v5·v6

작성자: 김지훈
작성일: 2026-10-22
갱신일: 2026-10-22
상태: 완료(이미지 생성·육안 확인)
브랜치·기준 커밋: `feature/jihun`, `5fb2dee`

## 요청과 목표

기존 [등대 UI v3](../2026-10-21/images/lighthouse-install-ui-workbench-v3.png)의 배치·문구·슬롯 상태를 유지하면서 작업대 참조에 가까운 손글씨 형태로 다시 만든다.

후속 v5는 사용자의 "등대 모양" 요청에 따라 중앙 등대 카드 외곽·지붕·등실 윤곽을 더 자연스러운 손그림 형태로 보정한다.

최신 v6는 "등대의 모양 자체"를 다시 만들어 달라는 후속 요청에 따라 통통한 몸통·둥근 밑변·넓은 비대칭 지붕으로 등대 카드의 전체 비율과 실루엣을 재설계한다.

## 진행 과정과 결정 이유

- 2026-10-22: 현재 미커밋 변경과 담당자 pointer를 확인했다. 기존 시안 원문은 이미지 직접 링크로 정리된 상태여서 복원하지 않고 이번 후속 요청을 별도로 기록했다.
- 내장 `image_gen`에서 v3를 편집 대상, 사용자가 첨부한 작업대 이미지를 손글씨·테두리 스타일 참조로 지정했다.
- 균일한 인쇄체 느낌을 줄이기 위해 글자 높이·너비·기울기·간격과 획에 작은 차이를 요청했다. 카드·슬롯·키캡·구분선·막대의 외곽선도 조금 더 손으로 그린 느낌으로 보정했다.
- 문구·4행 8슬롯·설치 3개·빈 활성 칸 1개·잠긴 상층 4개·모듈 종류와 배치를 유지하도록 지정했다. 이미지 스타일 보정이며 실제 UI 구현·한국어 표시·입력 규칙을 바꾸는 요청으로 확대하지 않았다.
- 2026-10-22 v5: 내장 image_gen에서 v4를 편집 대상으로 사용했다. 등대 양옆과 밑변의 자연스러운 굴곡, 좌우의 작은 비대칭, 지붕·어깨·꼭지의 둥근 연결을 요청했다. 글씨·아이콘·슬롯 상태·배치·배경 보존을 지정했다.
- 2026-10-22 v6: v5를 참조하여 내장 image_gen으로 전체 형태를 새로 만들었다. 통통한 둥근 몸통, 완화된 층계형 돌출, 큰 비대칭 지붕과 둥근 어깨·꼭지를 지정했다. 내부 4행 8슬롯 내용과 주변 카드·진행도·키 힌트는 유지했다.

## 변경 파일과 결과

- [최신 v6 PNG](images/lighthouse-install-ui-doodle-v6.png): 1672×941, 등대 카드의 몸통·지붕·밑변 비율을 새로 만든 손그림 형태 시안.
- [이전 v5 PNG](images/lighthouse-install-ui-freehand-v5.png): 1672×941, 등대 카드 외곽을 더 비대칭이고 둥근 손그림 형태로 보정한 시안.
- [이전 v4 PNG](images/lighthouse-install-ui-handwritten-v4.png): 1672×941, 손글씨와 손그림 외곽선을 강화한 시안.
- 이 작업 원문과 담당자 pointer 완료 이력 1행.
- 기존 v3 이미지와 런타임 UI 적용 기록·코드·씬·프리팹은 보존했다.

## 검증

- 실행: v3·v4 육안 비교 및 독립 검수. 층 이름·carrying·install·here·3/8·키 힌트가 유지되고 글씨의 획·기울기·높이가 더 불규칙해진 것을 확인했다.
- 실행(v5): v4·v5 육안 비교 및 독립 검수. 몸통 양옆·밑변의 굴곡과 어깨·지붕·꼭지의 둥근 변화가 보이며, 등대 형태·굵은 미색 선·갈흑 채움이 유지된다. 상단 내부 구분선도 조금 더 물결친 형태로 변했다. 문구·8칸 상태·모듈·자물쇠·배치에 명확한 누락은 없다.
- 실행(v5): System.Drawing으로 1672×941 확인. 복사본과 생성 원본의 SHA256 일치 확인. 원문·pointer·이미지 상대 링크 확인.
- 실행(v6): v5·v6 육안 비교 및 독립 검수. 넓은 비대칭 지붕·둥근 꼭지·통통한 몸통·둥근 밑변으로 실루엣이 뚜렷하게 달라졌다. 층계형 돌출이 부드러운 굴곡으로 완화됐다. 몸통은 아래로 넓어지는 비율이 조금 남으며 완전한 평행 옆선은 아니다.
- 실행(v6): 4행 8칸(설치 3·활성 빈칸 1·잠김 4), 모듈 종류·carrying·install F·here·3/8·우상단 키 힌트에 명확한 누락이 없다. System.Drawing으로 1672×941, 복사본·생성 원본 SHA256 일치를 확인했다. 상대 링크와 pointer 연결을 확인했다.
- 실행: 4행·8슬롯, 장착 3개·활성 빈칸 1개·잠긴 칸 4개, 우측 운반 카드·하단 막대·우상단 키 힌트와 모듈 종류·형태가 유지되는 것을 확인했다.
- 실행: System.Drawing으로 1672×941 확인. 기본 생성 경로에서 복사한 PNG의 SHA256 일치 확인. 기록의 상대 링크·pointer 연결과 변경 범위 정적 확인.
- 미검증: 아이콘·배경의 픽셀 단위 동일성, 진행 막대의 정확한 픽셀 비율. 육안 확인만 수행했다.
- 미실행: Unity 컴파일·플레이·멀티플레이. 정적 이미지 제작이며 런타임 변경은 없다.

## 남은 일과 다음 행동

이번 이미지 제작의 남은 실행 단계는 없다. 후속 요청이 있으면 세부 보정 또는 Unity 적용 범위를 정한다.

## 관련 원문

- [기존 등대 UI 적용 기록](../2026-10-21/등대-설치-UI-적용.md)

## v4 생성 프롬프트

내장 `image_gen` 사용. 입력 1은 등대 v3 편집 대상, 입력 2는 작업대 손글씨 스타일 참조다.

```text
Use case: style-transfer
Edit IMAGE 1 (the existing lighthouse UI screenshot). IMAGE 2 (workbench screenshot) is ONLY the reference for the handmade UI lettering and outline character.

User request: remake the existing lighthouse concept so it looks more like the reference's hand-drawn / handwritten mockup. This is a visual refinement, not a layout redesign.

PRIMARY CHANGE — HAND LETTERING:
Replace all foreground UI lettering in Image 1 with visibly hand-lettered lowercase writing. Match the warm, blunt, casual hand lettering of Image 2, and make the handmade character clearer than the current overly uniform lettering in Image 1. Soft rounded felt-tip strokes, gently uneven character widths/heights, natural spacing, tiny baseline variations and slight individual letter tilts. Simple friendly upright handwritten print, NOT cursive or calligraphy. Letters must remain easily readable. Repeated letters should not look like identical computer-font glyphs. Cream/off-white words on dark cards; near-black "install" lettering on the yellow button. F and ESC remain uppercase inside keycaps, but also hand drawn. Numerals in "3/8" must also feel handwritten.

SECONDARY CHANGE — SUBTLE HAND-DRAWN CONTOURS:
Make card outlines, slot outlines, separators, arrow, progress-bar outline and keycap borders slightly more naturally wobbly and hand drawn, in the same confident thick off-white stroke as the workbench reference. Gently irregular rounded corners and small variations in stroke pressure. Preserve the existing outline thickness, placement and dimensions approximately. No chalk dust, no sketch hatching, no loose construction lines. Keep dark brown-black card fills and flat yellow accents.

STRICT INVARIANTS:
Keep Image 1's entire background scene, player, old stone lighthouse, night, rain, dark sea, lighting, dimming and blur unchanged. Keep the large lighthouse-shaped UI card centered, the carrying card to its right, progress bar below the tower, and key hints in the top right. Keep the same crop and composition.
Keep all existing module icons and tiny locks unchanged in design, shape, color and position: cream dome-cage frame, cream cog gear, teal disc lens, small lantern light core. Do not redraw or simplify module designs.
Keep EXACTLY 4 rows and 8 slots, in this top-to-bottom order:
"lamp room" — faint lens and lantern, both locked;
"floor 3" — faint frame and gear, both locked;
"floor 2" — installed frame at left with yellow outline, EMPTY right gear slot with yellow outline, "here" and small arrow pointing to it;
"floor 1" — installed frame and gear, both with yellow outlines.
Exactly 3 installed module icons, 1 empty active target, 4 dim locked slots, 4 yellow slot outlines.
Keep "carrying", the gear, yellow "install" button, and its F keycap in the right card.
Keep top-right F keycap + "install", ESC keycap + "close".
Keep the thin progress bar at 3/8 (37.5%) filled yellow, and the exact text "3/8".

Exact text to preserve: "lamp room", "floor 3", "floor 2", "floor 1", "here", "carrying", "install", "3/8", "install", "close"; keycaps "F", "F", "ESC".
Do not add words, slots, HUD elements or a bottom hotbar. No navy UI, no paper, no red, no colorful new icons, no glossy/beveled controls, no comic speech bubbles.
The final result should clearly feel hand lettered and hand drawn while retaining every piece of content and the same layout of Image 1.
```

## v5 생성 프롬프트

내장 `image_gen` 사용. 입력은 v4 편집 대상 한 장이다.

```text
Use case: precise-object-edit
Edit this existing lighthouse UI concept screenshot with ONE TARGETED CHANGE: the OUTER LIGHTHOUSE-SHAPED CARD SILHOUETTE should look more naturally HAND DRAWN. The user specifically means the shape of the lighthouse card, not the handwriting.

REDRAW ONLY THE CENTRAL CARD'S OUTER CONTOUR AND SMALL ROOF/LANTERN-HOUSE CAP:
Keep its current overall size, position and recognizably tall tapering lighthouse silhouette, but replace the overly regular architectural geometry with a slightly wonky, warm hand-doodled lighthouse shape, as if drawn freehand with a thick cream felt-tip pen in one confident pass. Make the two long sides subtly asymmetric, gently bowing instead of perfectly straight tapered lines. Use soft uneven turns and gently rounded transitions rather than rigid mechanical steps. The broad bottom edge may have a very slight hand-drawn slant and bow. Draw the small lighthouse roof as a softly uneven, slightly off-center freehand roof with rounded joins; its little finial can lean a touch. Lantern-house edges/posts and the upper shoulders should feel loosely hand drawn and not perfectly mirrored. Use modest, tasteful asymmetry: still a clean and readable lighthouse UI, not an exaggerated cartoon or a melted shape. The result must visibly read as a hand-drawn lighthouse silhouette rather than just a vector outline with noise. Preserve comfortable space around all labels and slots, never move the outline across them.
Keep a continuous thick off-white/cream perimeter, naturally imperfect in direction and slight stroke pressure, with the same dark brown-black card fill and small warm-yellow windows in the cap. Do not use scribbled double contours, chalk texture, hatch marks or construction lines.

STRICT PRESERVATION:
Do not change ANY lettering, icons, small locks, row separator positions, slot borders or their positions. Keep the current handwriting exactly as shown.
Keep all 4 rows, labels, exactly 8 slots and all states:
top "lamp room" with faint locked teal lens + lantern;
"floor 3" with faint locked dome cage frame + gear;
"floor 2" with installed frame on left and EMPTY yellow-outlined gear target on right, plus "here" arrow;
bottom "floor 1" with installed frame + gear.
Keep exactly 3 installed modules, one active empty slot, four locked slots, four yellow slot outlines.
Keep the entire right "carrying" card with gear, yellow "install" button and F keycap unchanged.
Keep the 3/8 progress bar and lettering below the lighthouse unchanged, 37.5% yellow fill.
Keep the upper-right F install and ESC close keycap hints unchanged.
Keep the background game scene, raincoat player, real stone lighthouse, lighting, rain, dark sea, dimming, blur, image crop and composition unchanged.
No new icons, slots, text, materials, paper notes, navy UI, red, glow, bevels, glossy effects or background edits.
Only the central lighthouse CARD'S outer shape and roof/cap contour should become more hand drawn. Everything else stays the same.
```

## v6 최종 생성 프롬프트

내장 `image_gen` 사용. 입력은 v5 시안이며 등대 카드 비율·실루엣을 새로 만드는 참조로 사용했다.

```text
Use case: style-transfer
Create a revised version of the attached game UI screenshot. The user wants the LIGHTHOUSE CARD'S ACTUAL SHAPE AND PROPORTIONS REDESIGNED to fit the casual hand-lettered interface. Previous small contour-wobble edits were not enough. Make a clearly NEW SILHOUETTE.

ART DIRECTION FOR THE NEW CENTRAL LIGHTHOUSE CARD:
A chunky, friendly, simple STORYBOOK LIGHTHOUSE DOODLE, like a coastal lighthouse casually drawn by hand with a thick cream felt-tip marker. The architecture is deliberately naive and warmly imperfect, matching the handwritten words.
- Replace the current narrow-top/wide-bottom stepped trapezoid with a MUCH ROUNDER, BROADER TOWER BODY. Its sides are almost parallel with a soft slight belly and mild asymmetry. Broad rounded bottom corners and a softly curved base make it feel hand-shaped.
- REMOVE ALL THE FLOOR-BY-FLOOR PROTRUDING LEDGES/STEPS from the outer silhouette. Four horizontal rows still exist INSIDE, but the outside is one simple smooth chunky tower, with just small natural irregularities.
- Give the body broad, softly rounded top shoulders leading into a short, slightly off-center LANTERN CHAMBER. The little lantern chamber is squat and simply drawn, with the warm yellow windows.
- Completely redesign the ROOF: a much WIDER, LOW, LOPSIDED ROUNDED TRIANGULAR CAP, extending past the little lantern chamber, a slight jaunty tilt and one small crooked finial. It should be a visibly different silhouette from the narrow neatly centered roof in the reference.
- Use thick blunt OFF-WHITE contours with soft joins and slight hand pressure variation. Use flat dark brown-black fill. No architectural precision, symmetry, rigid straight walls or stacked wedding-cake silhouette. No extra doodles, bricks, decoration, hatching, chalk dust or paper.
- Keep it unmistakably a lighthouse, not a mushroom, bottle, rocket or castle. Make the proportions deliberately charming and handmade, not just the same lighthouse with vibrating outlines.
The new lighthouse should be visibly distinguishable at thumbnail size. Keep it the dominant large central card; it may become a bit wider and softer, and internal content may be gently repositioned to fit. Leave clear separation from the right carrying card and the progress bar below. Do not let the silhouette cross text or slots.

PRESERVE THE EXISTING UI CONTENT AND VISUAL FAMILY:
Keep the current casual handwritten lowercase lettering, thick cream outlines, dark brown-black cards and flat yellow accents.
Exactly FOUR ROWS and EIGHT workbench slots, top to bottom:
"lamp room": faint teal disc lens and small lantern/light core, both with small cream locks.
"floor 3": faint dome cage frame and cream cog gear, both with small cream locks.
"floor 2": installed dome cage frame at left, EMPTY yellow-outlined gear target at right, with "here" and arrow.
"floor 1": installed dome cage frame + installed cog gear, both yellow-outlined.
Thus 3 installed modules, one active empty target and four dim locked slots. Exactly four yellow slot borders. Keep the module designs and muted colors from the existing image.
Preserve right side card: "carrying", gear icon, yellow "install" button, F keycap.
Preserve thin progress bar BELOW the tower: 37.5% yellow fill, text "3/8".
Preserve top right hints: F keycap with "install", ESC keycap with "close".
No new UI content, no hotbar, no new icons, no new text.

PRESERVE BACKGROUND:
Same landscape 16:9 third-person game scene, back of raincoat player in the left foreground, old stone lighthouse at night behind them, rain, dark sea, dimmed slightly blurred background. Keep the real environmental stone lighthouse realistic and unchanged in design; ONLY the foreground UI lighthouse card receives the new doodle form.

Please deliver a polished final concept screenshot with an unmistakably newly designed HAND-DRAWN LIGHTHOUSE SHAPE. Focus on rounded chunky proportions and simple asymmetric roof architecture.
```

