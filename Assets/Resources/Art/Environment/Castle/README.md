# 소울 아처 마왕의 성 환경 패키지 — CMP-197

`committed implementation` 단계의 16×16 타일·멀티셀 소품·전투방 검수 패키지다. 산출물은 실제 Unity 맵의 최종 배치 파일이 아니라, 타일 조립·충돌·전투방 경로와 승인 스프라이트 합성을 1배에서 검수하기 위한 아트 인계 자료다.

## 산출물

| 파일 | 규격 | 용도 |
|---|---:|---|
| `castle_tiles.png` | 256×192 RGBA | 16×12 셀(셀당 16×16) 성 타일 아틀라스 |
| `castle_props.png` | 256×192 RGBA | 투명 배경 멀티셀 소품 아틀라스 |
| `castle_collision_guide.png` | 1024×640 RGBA | SOLID·LOW COVER·DECOR 형태 범례와 방 구조 검수 |
| `castle_rooms_preview.png` | 1920×360 RGBA | 640×360 전투방 3종을 가로로 붙인 1배 검수 이미지 |

타일·방 검수 이미지는 바닥과 벽을 판독해야 하므로 캔버스가 불투명하며, 소품 아틀라스는 셀 바깥이 완전 투명하다. 모든 PNG는 정수 좌표 픽셀 드로잉으로 제작했고 안티앨리어싱·반투명 외곽선·자동 리사이즈를 사용하지 않았다.

## 팔레트·제작 도구·출처

사용한 공통 32색 마스터 팔레트의 부분집합은 다음 20색이다.

`#000000 #141416 #2d2f33 #575d65 #8e98a5 #ffffff #e8d5b7 #2b4e8c #497ad3 #82aaff #3d2314 #6a4023 #a87449 #ecd09b #fcdb5d #ffe999 #ffd4c0 #4a1525 #8f1f2e #d9383a`

최종 타일·소품·가이드·방은 로컬 고정 팔레트와 Pillow 정수 픽셀 드로잉으로 제작했다. 방향·재료·붉은 포인트의 시각 기준을 잡기 위해 Image Generation 스킬의 built-in 생성 도구로 `dark demon castle interior, top-down pixel-art reference, charcoal stone, crimson banners/braziers, statuary pillars, royal altar, soul chest, healing statue, soul fountain, three combat-room layouts, no text` 콘셉트 시안을 생성했지만, 생성 시안은 프로젝트 산출물로 복사하지 않았다. 외부 이미지·외부 라이선스 자료는 사용하지 않았다.

전투방에는 승인된 내부 PNG를 그대로 합성했다.

- 레온: `References/Art/CharacterStyleSheetV2/leon.png` 정면 프레임, 32×32
- 방패 해골병: `References/Art/Enemies/Castle/shield_skeleton.png` 첫 32×32 프레임
- 암흑 마법사: `References/Art/Enemies/Castle/dark_mage.png` 첫 32×32 프레임
- 모르칸: `References/Art/Bosses/Castle/morkan_boss.png` 첫 64×64 프레임

## `castle_tiles.png` 좌표표

좌표는 셀 좌상단 `(x,y)`이며 0부터 시작한다. 모든 항목은 1×1 셀이다.

| 좌표 | 이름 | 방향/상태 | 분류 |
|---|---|---|---|
| `(0,0)–(3,0)` | `floor_base`, `floor_var_a/b/c` | 기본·저대비 변형 | DECOR |
| `(4,0)–(7,0)` | `floor_crack`, `floor_rune`, `floor_red_stain`, `floor_edge` | 바닥 변형 | DECOR |
| `(8,0)–(15,0)` | `floor_var_08–15` | 반복 검수 변형 | DECOR |
| `(0,1)–(3,1)` | `wall_n/s/w/e` | 방향별 벽 | SOLID |
| `(4,1)–(7,1)` | `outer_tl/tr/bl/br` | 외곽 모서리 | SOLID |
| `(8,1)–(11,1)` | `inner_tl/tr/bl/br` | 내곽 모서리 | SOLID |
| `(12,1)` | `wall_cap` | 벽 경계 캡 | SOLID |
| `(13,1)–(14,1)` | `stairs_up/down` | 계단 방향 | SOLID |
| `(15,1)` | `royal_threshold` | 왕가 문턱/경계 | LOW COVER |
| `(0,2)–(3,2)` | `door_closed_n/s/w/e` | 닫힌 문 | SOLID |
| `(4,2)–(7,2)` | `door_open_n/s/w/e` | 열린 문 | DECOR, 통과 |
| `(8,2)–(11,2)` | `door_frame_n/s/w/e` | 열린 문 프레임 | SOLID 외곽 + 통과 중앙 |
| `(12,2)–(15,2)` | `threshold`, `chain`, `bloodline`, `rune` | 문·경계 장식 | DECOR |
| `(0,3)–(3,3)` | `wall_cap_n/s/w/e` | 방향별 경계 보조 | SOLID |
| `(4,3)–(7,3)` | `bridge_border` | 계단/통로 경계 | LOW COVER |
| `(8,3)` | `solid_marker` | 굵은 X 형태 | SOLID 표시 |
| `(9,3)` | `low_cover_marker` | 노란 사선 형태 | LOW COVER 표시 |
| `(10,3)` | `decor_marker` | 십자 형태 | DECOR 표시 |
| `(11,3)–(15,3)` | `stone/blood/rune` | 장식·경계 변형 | DECOR |
| `(0,4)–(15,11)` | 반복 스트립·방향 재검수 셀 | 바닥·벽·문·계단 반복 | 표의 원형 분류를 따름 |

문은 런타임에서 3셀 연속 폭, 즉 `48px` 이상으로 조립한다. 열린 문 순서는 N/S/W/E이며 `(4,2)–(7,2)`를 사용한다. 닫힌 문은 `(0,2)–(3,2)`다. `door_frame`은 프레임이 SOLID이고 중앙 통과 영역만 비워 사용한다.

Unity import: `Sprite (2D and UI)`, `Sprite Mode: Multiple`, `Pixels Per Unit: 16`, `Filter Mode: Point (no filter)`, `Compression: None`, `Mesh Type: Full Rect`, `16×16 Grid Slice`.

## `castle_props.png` 멀티셀 좌표·피벗·상태

좌표와 크기는 셀 기준이며, 실제 픽셀 피벗은 `(width/2, height-4)`에 둔다. 즉 아래 소품은 바닥 접점에서 4px 위를 기준선으로 삼는다.

| 셀 좌상단 | 소품 | 크기 | 픽셀 크기 | 분류 | 상태/판독 요소 |
|---|---|---:|---:|---|---|
| `(0,0)` | 석상 기둥 A | 2×2 | 32×32 | SOLID | 뼈 얼굴·붉은 핵 |
| `(2,0)` | 석상 기둥 B | 2×2 | 32×32 | SOLID | 골드 핵 변형 |
| `(4,0)` | 대칭 벽 조각 | 3×2 | 48×32 | SOLID | 좌우 붉은 문양 대칭 |
| `(8,0)` | 중앙 왕가 제단 | 4×3 | 64×48 | SOLID 중심 | 중앙 금색 문양·붉은 핵 |
| `(13,0)` | 붉은 깃발/화로 | 2×3 | 32×48 | DECOR, 화로 주변 LOW COVER | 깃발, 불꽃, 받침 |
| `(0,4)` | 영혼 상자 닫힘 | 2×2 | 32×32 | SOLID | 닫힌 뚜껑·금색 잠금 |
| `(2,4)` | 영혼 상자 열림 | 2×2 | 32×32 | SOLID | 열린 뚜껑·붉은 영혼핵 |
| `(4,4)` | 치유의 석상 활성 | 2×3 | 32×48 | SOLID | 파란 보석·왕가의 빛 |
| `(6,4)` | 치유의 석상 소진 | 2×3 | 32×48 | SOLID | 어두운 보석·붉은 균열 |
| `(8,4)` | 영혼의 샘 활성 | 4×3 | 64×48 | SOLID 외곽 / DECOR 물 | 파란 물·금색 중심 |
| `(12,4)` | 영혼의 샘 사용 후 | 4×3 | 64×48 | SOLID 외곽 / DECOR 물 | 빈 수면·붉은 균열 |
| `(0,8)–(15,10)` | 반복 기둥·벽·깃발·왕가 문양 | 보조 스탬프 | - | 표의 원형 분류를 따름 | 대칭·장식 검수용 |

Unity import: `Sprite (2D and UI)`, `Sprite Mode: Multiple`, `Pixels Per Unit: 16`, `Filter Mode: Point (no filter)`, `Compression: None`, `Mesh Type: Full Rect`. 멀티셀을 하나의 Sprite로 잘라 사용할 때는 위 픽셀 크기와 바닥 피벗을 유지한다.

## 전투방 3종 (`castle_rooms_preview.png`)

전체 이미지는 패널 3개를 `x=0`, `640`, `1280`에 배치한 `1920×360`이다. 각 패널은 `(0,0)` 기준 `640×360`이며, 파란 사선 박스는 진입 안전 영역, 붉은 다이아몬드는 적 생성 후보, 금색 점선은 자동 조준 시야/문 연결 검수 경로다. 실제 적 후보는 장식이 아니라 승인된 성 적 스프라이트의 주변 위치로 표시한다.

| 패널 | 구조 | 열린 문 | 닫힌 문 | 안전 영역 | 검수 경로 |
|---|---|---|---|---|---|
| A `x=0` | 석상 기둥형 | N/S/W/E, 각 48px | 없음 | `(296,8)–(343,55)`, `(296,304)–(343,351)`, `(8,156)–(55,203)`, `(584,156)–(631,203)` | 네 문을 외곽 루프로 연결하고 중앙 제단을 비움 |
| B `x=640` | 대칭 벽형 | W/E, 각 48px | N/S, 각 48px | `(8,156)–(55,203)`, `(1224,156)–(1271,203)` | W→상단 우회→중앙→하단 우회→E 수평 경로 |
| C `x=1280` | 중앙 제단형 | N/S/W/E, 각 48px | 없음 | `(1576,8)–(1623,55)`, `(1576,304)–(1623,351)`, `(1288,156)–(1335,203)`, `(1864,156)–(1911,203)` | 네 문과 중앙 제단 주위를 잇는 십자·대각선 경로 |

패널 A에는 4개 석상 기둥과 작은 제단, 패널 B에는 상·하 대칭 벽 조각과 중앙 왕가 문양, 패널 C에는 중앙 제단·양쪽 샘·네 모서리 화로를 배치했다. 세 패널 모두 레온, 방패 해골병, 암흑 마법사, 모르칸 실제 픽셀을 합성했으며, 캐릭터의 투명 픽셀은 바닥을 덮지 않는다. 모든 주요 이동 통로는 48px 문 폭보다 넓고, 안전 영역은 문 중심에 맞춘 48×48px이다.

Unity import: `Sprite (2D and UI)`, `Single`, `Pixels Per Unit: 1` (검수 시안 기준), `Filter Mode: Point (no filter)`, `Compression: None`. 실제 런타임 방은 640×360 내부 해상도에서 타일과 오버레이를 별도 레이어로 조립한다.

## `castle_collision_guide.png`

범례는 색상만 의존하지 않는다.

- `SOLID`: 돌 블록 안의 붉은 굵은 X. 이동·화살 모두 차단.
- `LOW COVER`: 갈색 블록 안의 노란 사선. 이동은 허용하고 화살만 차단.
- `DECOR`: 어두운 블록 안의 회색 십자. 이동·화살 모두 허용.
- 아래 3개 방 샘플의 파란 실선은 이동 경로, 금색 점선은 자동 조준 시야, 붉은 X/노란 사선/회색 십자는 해당 충돌 형태다.

## 검증 기록

실행한 Pillow 검증 결과:

- `castle_tiles.png`: `256×192 / RGBA`, `16×12` 정수 격자, 알파 `{255}`, 승인 팔레트 외 RGB `0`.
- `castle_props.png`: `256×192 / RGBA`, 알파 `{0,255}`, 투명 픽셀 `32,078`, 승인 팔레트 외 RGB `0`.
- `castle_collision_guide.png`: `1024×640 / RGBA`, 알파 `{255}`, 승인 팔레트 외 RGB `0`.
- `castle_rooms_preview.png`: `1920×360 / RGBA`, 3패널 `640×360`, 알파 `{255}`, 승인 팔레트 외 RGB `0`.
- 문 폭 `48px`, 패널 수 `3`, 모든 패널 크기 `640×360` 확인.
- 레온 3회 `450/450`, 방패 해골병 3회 `361/361`, 암흑 마법사 3회 `386/386`, 모르칸 3회 `1398/1398` 불투명 픽셀 원본 일치.
- `git diff --check` 통과. 변경 경로는 `References/Art/Environment/Castle/`로 제한한다.

미검증: 실제 Unity 임포트·타일맵 콜라이더 연결·런타임 플레이테스트·실제 카메라에서의 전투 대비. 다음 검토자는 **Designer (CMP-197)** 이다.
