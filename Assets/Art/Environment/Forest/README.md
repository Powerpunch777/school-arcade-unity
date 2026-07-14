# 숲 환경 타일 세트 — CMP-179

`committed implementation` 산출물. 16×16 고정 셀 아틀라스이며, 승인된 숲 마스터 팔레트의 불투명 픽셀만 사용한다.

## 산출물

- `forest_tiles.png`: 256×96, 16열×6행, 셀 크기 16×16.
- `forest_preview.png`: 640×360 실제 타일 조립 예시. 상단에 열린 48px 통로와 닫힌 문을 나란히 배치했으며, 방 템플릿 확정본은 아니다.
- `collision_guide.png`: 16×6 좌표 그리드. SOLID는 굵은 X와 실선 윤곽, DECOR는 빈 원과 점선 윤곽으로 구분한다.

## Unity 임포트

`Sprite (2D and UI)`, `Pixels Per Unit: 16`, `Filter Mode: Point (no filter)`, `Compression: None`, `Mesh Type: Full Rect`를 사용한다. `forest_tiles.png`를 16×16 Grid로 Slice한다.

## 아틀라스 좌표표

좌표는 `(x,y)`이며 행 우선으로 정렬했다. `이동 충돌`은 캐릭터 이동을 막고, `화살 차단`은 화살 투사체를 막는다.

| 좌표 | 이름 | 분류 | 이동 충돌 | 화살 차단 |
|---|---|---|---|---|
| (0,0) | `floor_basic` | 바닥 | 아니오 | 아니오 |
| (1,0) | `floor_moss` | 바닥 변형 | 아니오 | 아니오 |
| (2,0) | `floor_leaf` | 바닥 변형 | 아니오 | 아니오 |
| (3,0) | `dirt_basic` | 흙길 | 아니오 | 아니오 |
| (4,0) | `dirt_pebbled` | 흙길 변형 | 아니오 | 아니오 |
| (5,0) | `dirt_rooted` | 흙길 변형 | 아니오 | 아니오 |
| (6,0) | `floor_path_edge_N` | 바닥-흙길 경계 | 아니오 | 아니오 |
| (7,0) | `floor_path_edge_E` | 바닥-흙길 경계 | 아니오 | 아니오 |
| (8,0) | `floor_path_edge_S` | 바닥-흙길 경계 | 아니오 | 아니오 |
| (9,0) | `floor_path_edge_W` | 바닥-흙길 경계 | 아니오 | 아니오 |
| (10,0) | `floor_path_outer_NW` | 외곽 모서리 | 아니오 | 아니오 |
| (11,0) | `floor_path_outer_NE` | 외곽 모서리 | 아니오 | 아니오 |
| (12,0) | `floor_path_outer_SW` | 외곽 모서리 | 아니오 | 아니오 |
| (13,0) | `floor_path_outer_SE` | 외곽 모서리 | 아니오 | 아니오 |
| (14,0) | `floor_path_inner_NW` | 내곽 모서리 | 아니오 | 아니오 |
| (15,0) | `floor_path_inner_NE` | 내곽 모서리 | 아니오 | 아니오 |
| (0,1) | `cliff_N` | 절벽 상단 | 예 | 예 |
| (1,1) | `cliff_S` | 절벽 하단 | 예 | 예 |
| (2,1) | `cliff_W` | 절벽 좌측 | 예 | 예 |
| (3,1) | `cliff_E` | 절벽 우측 | 예 | 예 |
| (4,1) | `cliff_NW` | 절벽 외곽 NW | 예 | 예 |
| (5,1) | `cliff_NE` | 절벽 외곽 NE | 예 | 예 |
| (6,1) | `cliff_SW` | 절벽 외곽 SW | 예 | 예 |
| (7,1) | `cliff_SE` | 절벽 외곽 SE | 예 | 예 |
| (8,1) | `path_edge_N` | 흙길 경계 | 아니오 | 아니오 |
| (9,1) | `path_edge_E` | 흙길 경계 | 아니오 | 아니오 |
| (10,1) | `path_edge_S` | 흙길 경계 | 아니오 | 아니오 |
| (11,1) | `path_edge_W` | 흙길 경계 | 아니오 | 아니오 |
| (12,1) | `path_outer_NW` | 흙길 외곽 NW | 아니오 | 아니오 |
| (13,1) | `path_outer_NE` | 흙길 외곽 NE | 아니오 | 아니오 |
| (14,1) | `path_outer_SW` | 흙길 외곽 SW | 아니오 | 아니오 |
| (15,1) | `path_outer_SE` | 흙길 외곽 SE | 아니오 | 아니오 |
| (0,2) | `path_inner_NW` | 흙길 내곽 NW | 아니오 | 아니오 |
| (1,2) | `path_inner_NE` | 흙길 내곽 NE | 아니오 | 아니오 |
| (2,2) | `path_inner_SW` | 흙길 내곽 SW | 아니오 | 아니오 |
| (3,2) | `path_inner_SE` | 흙길 내곽 SE | 아니오 | 아니오 |
| (4,2) | `door_closed_N` | 닫힌 문 상단 | 예 | 예 |
| (5,2) | `door_closed_S` | 닫힌 문 하단 | 예 | 예 |
| (6,2) | `door_closed_W` | 닫힌 문 좌측 | 예 | 예 |
| (7,2) | `door_closed_E` | 닫힌 문 우측 | 예 | 예 |
| (8,2) | `door_open_N` | 열린 문 상단 | 아니오 | 아니오 |
| (9,2) | `door_open_S` | 열린 문 하단 | 아니오 | 아니오 |
| (10,2) | `door_open_W` | 열린 문 좌측 | 아니오 | 아니오 |
| (11,2) | `door_open_E` | 열린 문 우측 | 아니오 | 아니오 |
| (12,2) | `collision_tree_TL` | 나무 2×2 좌상 | 예 | 예 |
| (13,2) | `collision_tree_TR` | 나무 2×2 우상 | 예 | 예 |
| (14,2) | `collision_rock_TL` | 바위 2×2 좌상 | 예 | 예 |
| (15,2) | `collision_rock_TR` | 바위 2×2 우상 | 예 | 예 |
| (0,3) | `grass` | 장식 | 아니오 | 아니오 |
| (1,3) | `flower` | 장식 | 아니오 | 아니오 |
| (2,3) | `fallen_leaves` | 장식 | 아니오 | 아니오 |
| (3,3) | `small_stone` | 장식 | 아니오 | 아니오 |
| (4,3) | `shadow` | 장식 | 아니오 | 아니오 |
| (5,3) | `grass_tuft` | 장식 | 아니오 | 아니오 |
| (6,3) | `flower_blue` | 장식 | 아니오 | 아니오 |
| (7,3) | `leaf_patch` | 장식 | 아니오 | 아니오 |
| (8,3) | `floor_variant_3_08` | 바닥 변형 | 아니오 | 아니오 |
| (9,3) | `floor_variant_3_09` | 바닥 변형 | 아니오 | 아니오 |
| (10,3) | `floor_variant_3_10` | 바닥 변형 | 아니오 | 아니오 |
| (11,3) | `floor_variant_3_11` | 바닥 변형 | 아니오 | 아니오 |
| (12,3) | `collision_tree_BL` | 나무 2×2 좌하 | 예 | 예 |
| (13,3) | `collision_tree_BR` | 나무 2×2 우하 | 예 | 예 |
| (14,3) | `collision_rock_BL` | 바위 2×2 좌하 | 예 | 예 |
| (15,3) | `collision_rock_BR` | 바위 2×2 우하 | 예 | 예 |
| (0,4) | `collision_stump_TL` | 그루터기 2×2 좌상 | 예 | 예 |
| (1,4) | `collision_stump_TR` | 그루터기 2×2 우상 | 예 | 예 |
| (2,4) | `collision_bush_TL` | 덤불 2×2 좌상 | 예 | 예 |
| (3,4) | `collision_bush_TR` | 덤불 2×2 우상 | 예 | 예 |
| (4,4) | `floor_variant_1_04` | 바닥 변형 | 아니오 | 아니오 |
| (5,4) | `floor_variant_1_05` | 바닥 변형 | 아니오 | 아니오 |
| (6,4) | `floor_variant_1_06` | 바닥 변형 | 아니오 | 아니오 |
| (7,4) | `floor_variant_1_07` | 바닥 변형 | 아니오 | 아니오 |
| (8,4) | `floor_variant_1_08` | 바닥 변형 | 아니오 | 아니오 |
| (9,4) | `floor_variant_1_09` | 바닥 변형 | 아니오 | 아니오 |
| (10,4) | `floor_variant_1_10` | 바닥 변형 | 아니오 | 아니오 |
| (11,4) | `floor_variant_1_11` | 바닥 변형 | 아니오 | 아니오 |
| (12,4) | `floor_variant_1_12` | 바닥 변형 | 아니오 | 아니오 |
| (13,4) | `floor_variant_1_13` | 바닥 변형 | 아니오 | 아니오 |
| (14,4) | `floor_variant_1_14` | 바닥 변형 | 아니오 | 아니오 |
| (15,4) | `floor_variant_1_15` | 바닥 변형 | 아니오 | 아니오 |
| (0,5) | `collision_stump_BL` | 그루터기 2×2 좌하 | 예 | 예 |
| (1,5) | `collision_stump_BR` | 그루터기 2×2 우하 | 예 | 예 |
| (2,5) | `collision_bush_BL` | 덤불 2×2 좌하 | 예 | 예 |
| (3,5) | `collision_bush_BR` | 덤불 2×2 우하 | 예 | 예 |
| (4,5) | `floor_variant_2_04` | 바닥 변형 | 아니오 | 아니오 |
| (5,5) | `floor_variant_2_05` | 바닥 변형 | 아니오 | 아니오 |
| (6,5) | `floor_variant_2_06` | 바닥 변형 | 아니오 | 아니오 |
| (7,5) | `floor_variant_2_07` | 바닥 변형 | 아니오 | 아니오 |
| (8,5) | `floor_variant_2_08` | 바닥 변형 | 아니오 | 아니오 |
| (9,5) | `floor_variant_2_09` | 바닥 변형 | 아니오 | 아니오 |
| (10,5) | `floor_variant_2_10` | 바닥 변형 | 아니오 | 아니오 |
| (11,5) | `floor_variant_2_11` | 바닥 변형 | 아니오 | 아니오 |
| (12,5) | `floor_variant_2_12` | 바닥 변형 | 아니오 | 아니오 |
| (13,5) | `floor_variant_2_13` | 바닥 변형 | 아니오 | 아니오 |
| (14,5) | `floor_variant_2_14` | 바닥 변형 | 아니오 | 아니오 |
| (15,5) | `floor_variant_2_15` | 바닥 변형 | 아니오 | 아니오 |

## 멀티셀 스탬프와 검토 메모

- 나무: 점유 셀 `(12,2)–(13,3)`, 충돌 셀 4개 전부.
- 큰 바위: 점유 셀 `(14,2)–(15,3)`, 충돌 셀 4개 전부.
- 그루터기: 점유 셀 `(0,4)–(1,5)`, 충돌 셀 4개 전부.
- 덤불: 점유 셀 `(2,4)–(3,5)`, 충돌 셀 4개 전부.
- `forest_preview.png`의 장애물은 위 셀을 2×2로 직접 조립한 것이다. 큰 검은 사각형 그림자는 사용하지 않고 작은 타원 접점 그림자만 사용했다.
- 미리보기 상단의 열린 문은 `(8,2)` 셀 3개로 48px 통로를, 닫힌 문은 `(4,2)` 셀 3개로 상태 비교를 보여 준다.
- `forest_preview.png`의 레온·숲 늑대·독버섯은 각각 `References/Art/Player/Leon/leon_walk.png`, `References/Art/Enemies/Forest/forest_wolf.png`, `References/Art/Enemies/Forest/poison_mushroom.png`의 실제 32×32 프레임을 최근접 합성했다.

## 검증 범위

PNG 크기·16px 나눗셈, 셀 좌표 정렬, 3×3 바닥/흙길 반복, 경계·모서리 셀, 48px 문 폭, 불투명 알파와 팔레트 수를 확인했다. Unity 실제 임포트와 게임 내 충돌 컴포넌트 연동은 아직 검증하지 않았다.

다음 검토자: Designer (CMP-110)
