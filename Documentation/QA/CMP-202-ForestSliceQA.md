# CMP-202 소울 아처 숲 수직 슬라이스 독립 QA 보고서

- 검증일: 2026-07-15 (KST)
- 대상: `School Arcade Unity Implementation` / `C:\Users\young\Documents\Game Design\school-arcade-unity`
- 기준 Unity: `6000.5.3f1`
- 대상 시작 씬: `Assets/Scenes/ForestSlice.unity`
- 종합 판정: **INCONCLUSIVE**

## 판정 요약

| 기준 | 판정 | 근거 |
|---|---|---|
| Unity 프로젝트 버전·시작 씬 | PASS | `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`, 씬 파일 확인 |
| 640×360 내부 기준 화면 | PASS (정적) | `SoulArcherGame.cs:121-129`의 GUI 기준 행렬이 640×360을 사용 |
| 키보드 이동·이동 중 발사 금지 | PASS (모델/코드), 실행 INCONCLUSIVE | `SoulArcherGame.cs:139`, EditMode `MovingPlayerCannotFireNewArrow` 통과; 키보드 조작 플레이는 미실행 |
| 정지 시 화면 내 최근접 생존 적 자동 조준·발사 | PASS (모델/코드), 실행 INCONCLUSIVE | `SoulArcherGame.cs:78-90`, EditMode `StoppedPlayerSelectsNearestLivingVisibleEnemy` 통과 |
| 5하트·피격·0.5초 무적·사망·R 재시작 | PASS (모델/코드), 실행 INCONCLUSIVE | `SoulArcherModels.cs:44-56`, `SoulArcherGame.cs:99-101`, EditMode `DamageStartsInvincibilityAndDeath` 통과 |
| 숲 전투 3개+상점+보스 이동·재방문·미니맵 | PASS (정적 그래프), 실행 INCONCLUSIVE | `SoulArcherModels.cs:157-165`, `SoulArcherGame.cs:35-36,127`; 실제 방 이동·재방문 조작 미실행 |
| 늑대·독버섯·거대 나무 정령 | PASS (코드) | `SoulArcherGame.cs:49-51,69-72,144-151` |
| 영혼·상점 3종·레벨업 3카드 | PASS (모델/코드), 실행 INCONCLUSIVE | `SoulArcherModels.cs:61-93,138-150`, `SoulArcherGame.cs:103-110`; 실제 구매·카드 선택 미실행 |
| References/Art 자산 추적 | PASS (정적) | `Documentation/AssetManifest.md`; 구현·원본 5개 경로 모두 존재 |
| 핵심 EditMode 테스트 | PASS | 재실행 XML: `total=10, passed=10, failed=0, inconclusive=0, skipped=0` |
| batchmode 컴파일 오류 0개 | PASS | Unity 종료 코드 0, `CMP-205-batchmode-compile.log`의 Tundra build success 및 종료 코드 0 |
| Windows 빌드 5초 이상 스모크 | PASS | `Builds/SoulArcher.exe`가 8초 후에도 실행 중이었고 QA 종료 시 정리함 |

## 실행한 검증

### 1. 저장소·프로젝트 확인

확인한 경로:

- `Assets/Scenes/ForestSlice.unity`
- `ProjectSettings/ProjectVersion.txt` — `m_EditorVersion: 6000.5.3f1`
- `ProjectSettings/EditorBuildSettings.asset` — 시작 씬이 `Assets/Scenes/ForestSlice.unity`
- `Documentation/AssetManifest.md`
- `Assets/Scripts/SoulArcherModels.cs`
- `Assets/Scripts/SoulArcherGame.cs`
- `Assets/Tests/Editor/SoulArcherModelsTests.cs`

설계 저장소의 `References/Art` 및 구현 저장소의 `Assets/Art`에 Player/Enemies/Bosses/Environment/ForestStructures 경로가 모두 존재함을 확인했다.

### 2. batchmode 컴파일

실행 명령:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe'
& $unity -batchmode -nographics -quit -projectPath (Get-Location).Path -logFile 'Documentation/QA/CMP-205-batchmode-compile.log'
```

결과:

- Unity 종료 코드: `0`
- `*** Tundra build success`
- `AssetDatabase: script compilation time: 0.332696s`
- `Exiting batchmode successfully now!`
- 코드 컴파일 오류: `0` (로그에서 `error CS`/컴파일 오류 없음)

로그: `Documentation/QA/CMP-205-batchmode-compile.log`

### 3. EditMode 테스트 재실행

실행 명령:

```powershell
& $unity -batchmode -nographics -projectPath (Get-Location).Path -runTests -testPlatform EditMode -testResults 'Documentation/QA/CMP-205-editmode-results.xml' -logFile 'Documentation/QA/CMP-205-editmode.log'
```

결과 XML:

- `result=Passed`
- `total=10`
- `passed=10`
- `failed=0`
- `inconclusive=0`
- `skipped=0`
- Unity 테스트 실행 종료 코드: `0`

주요 통과 테스트에는 `MovingPlayerCannotFireNewArrow`, `StoppedPlayerSelectsNearestLivingVisibleEnemy`, `DamageStartsInvincibilityAndDeath`, `RoomStateTracksVisitClearAndRevisit`, `ShopPurchaseSpendsSoulsAndAppliesItemOnce`, `AbilitySelectionChangesStats`가 포함된다.

결과: `Documentation/QA/CMP-205-editmode-results.xml`  
로그: `Documentation/QA/CMP-205-editmode.log`

### 4. Windows 실행 스모크

실행 명령:

```powershell
$p=Start-Process -FilePath (Resolve-Path 'Builds/SoulArcher.exe').Path -ArgumentList '-screen-width 1280','-screen-height 720','-screen-fullscreen 0' -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 8
```

결과:

- 실행 파일 크기: `667648` bytes
- SHA-256: `5AE0C84993C6BFF7D0F03166CAFD148CDEC61F67EC85B5D97600E04B2ABB26E4`
- PID `13320`이 8초 후에도 존재하여 실행 유지 확인
- 스모크 종료 후 해당 QA 실행 프로세스만 강제 종료
- 이 검증은 창 내부 입력·렌더링·게임플레이 통과를 의미하지 않음

로그: `Documentation/QA/CMP-205-windows-smoke.log`

## 독립 플레이 흐름 판정

실행 파일을 8초 유지하는 스모크는 수행했지만, 현재 환경에서 Unity 창에 키보드 입력을 주입하는 독립 자동화 경로가 없어 전체 플레이스루는 실행하지 못했다. 따라서 아래 기능은 코드·모델·EditMode 근거와 실제 플레이 결과를 구분한다.

| 흐름 | 판정 | 미검증 범위 |
|---|---|---|
| WASD/방향키 이동 | INCONCLUSIVE | 실제 키 입력 및 화면 이동 |
| 이동 중 발사 금지 / 정지 자동 조준 발사 | INCONCLUSIVE | 실제 투사체 생성·근접 적 선택·화면 표시 |
| 피격·무적·사망·R 재시작 | INCONCLUSIVE | 실제 적 공격, HUD 하트 변화, 사망 화면 및 재시작 |
| 방 클리어 후 좌우 이동·재방문·미니맵 | INCONCLUSIVE | 실제 전투 클리어, 출구 통과, 재방문 및 미니맵 시각 상태 |
| 상점 구매 및 Esc/Space 이탈 | INCONCLUSIVE | 실제 영혼 차감, 구매 완료 표시 및 이탈 |
| 전투 클리어 후 3카드 선택 | INCONCLUSIVE | 실제 오버레이 정지, 숫자키 입력 및 수치 반영 |
| 늑대·독버섯·거대 나무 정령 전투 | INCONCLUSIVE | 실제 패턴·피격 판정·처치·보스 HUD |

## 발견 결함

### F-01 — 재시작해도 상점 구매 상태가 초기화되지 않음

- 심각도: Major
- 기준/기능: 사망 후 재시작, 상점 상태 초기화
- 근거: `SoulArcherGame.cs:14`의 `bought` 배열은 인스턴스 생성 시 한 번만 만들어지고, `Restart()`(`SoulArcherGame.cs:101`)에서 새 배열로 초기화하지 않음.
- 재현 단계:
  1. 숲 상점에서 영혼을 충분히 확보한다.
  2. 상품 1개를 구매해 `판매 완료` 상태로 만든다.
  3. 사망한 뒤 `R`을 눌러 재시작한다.
  4. 다시 상점에 진입한다.
- 기대 결과: 새 판이므로 3개 상품이 모두 구매 가능 상태로 초기화된다.
- 실제 결과(코드 근거): 구매한 상품의 `bought[i]`가 유지되어 `판매 완료`로 남고 재구매할 수 없다.
- 미실행 사유: 키보드 입력 플레이스루는 이 환경에서 수행하지 못함.

### F-02 — 구현 실행 출력이 설계의 기본 FHD 전체화면과 불일치

- 심각도: Minor / 설계 적합성
- 기준/기능: 기본 출력
- 근거: 설계 `UIWireframes.md:7-8`, `GameDesignDocument.md:508-509`는 내부 640×360 및 기본 1920×1080 FHD 정수 3배 전체화면을 규정하지만, `SoulArcherGame.cs:19`는 `Screen.SetResolution(1280, 720, false)`를 강제한다.
- 기대 결과: 기본 실행이 설계의 1920×1080 전체화면 기준을 따른다.
- 실제 결과: 1280×720 창 모드가 강제된다. 다만 GUI 논리 좌표 자체는 640×360으로 구성되어 있어 이번 수락표의 내부 기준 화면은 정적 PASS로 판정했다.

### F-03 — 설계의 영혼 보상값과 구현 보상값 불일치

- 심각도: Minor / 경제 밸런스
- 기준/기능: 영혼 경제
- 근거: 설계 `GameDesignDocument.md:446-449`는 일반 몬스터 1, 숲 보스 5를 초기값으로 제시하지만, `SoulArcherModels.cs:63-70`은 늑대 5, 독버섯 7, 거대 나무 정령 40을 반환한다.
- 기대 결과: 승인된 초기 경제값과 일치하거나 변경 승인 근거가 보고된다.
- 실제 결과: 구현값이 설계 초기값보다 크게 높다. 상점 가격 자체(10/15/20)는 `SoulArcherModels.cs:77`에서 일치한다.

## 잔여 위험 및 미검증 영역

- 실제 Unity 에디터 Play 또는 Windows 빌드의 키보드 입력을 통한 전체 플레이스루를 수행하지 못했다.
- 자동 조준 고리, 일반 적 체력바, 보스 이름/대형 체력바, 실제 스프라이트·애니메이션·충돌·사운드의 시각/청각 동작은 미검증이다. 현재 런타임은 `Texture2D.whiteTexture` 기반 컬러 스프라이트를 생성하며 `Assets/Art` PNG는 추적 입력으로만 보존한다.
- `Builds/SoulArcher.exe`는 실행 스모크만 했고 이번 QA에서 새 빌드를 생성하지 않았다. 실행 파일이 현재 소스와 동일한 빌드 산출물인지 별도 provenance 파일은 확인하지 못했다.
- 설계 전체 캠페인(동굴·마왕의 성), 전적 저장, 영혼의 샘·보상방 등 숲 수직 슬라이스 범위를 넘어서는 항목은 이번 판정 대상이 아니다.

## 변경 범위 확인

QA 시작 시 `git status --short`에는 다음 기존 작업 상태가 있었다: `M .gitignore`, `M README.md`, `?? Assets/`, `?? Documentation/`, `?? Packages/`, `?? ProjectSettings/`, `?? Tests/`.

이번 QA에서 제품 코드·씬·ProjectSettings·Tests·Assets·빌드 산출물은 수정하지 않았다. 생성된 QA 산출물은 다음으로 제한했다.

- `Documentation/QA/CMP-202-ForestSliceQA.md`
- `Documentation/QA/CMP-205-batchmode-compile.log`
- `Documentation/QA/CMP-205-editmode-results.xml`
- `Documentation/QA/CMP-205-editmode.log`
- `Documentation/QA/CMP-205-windows-smoke.log`

최종 검토자: CMP-205 Task Owner, 손영하-CTO
