# 소울 아처 (Soul Archer)

Unity `6000.5.3f1` 기반 Windows PC용 2D 액션 게임 구현입니다. 시작 씬은 `Assets/Scenes/ForestSlice.unity`이며 타이틀에서 난이도, 3장 도입 컷신, 숲·동굴·마왕의 성 캠페인, 공주 구출 엔딩과 결과/전적 화면으로 이어집니다.

## 실행

1. Unity Hub에서 이 폴더를 Unity `6000.5.3f1`로 엽니다.
2. `Assets/Scenes/ForestSlice.unity`를 연 뒤 Play를 누릅니다.
3. 타이틀에서 `[1] 새 게임`을 선택하고 `[1] 쉬움` 또는 `[2] 보통`을 고릅니다.
4. WASD/방향키로 이동합니다. 이동 중에는 새 화살을 발사하지 않으며, 정지하면 화면 안의 가장 가까운 살아 있는 적을 자동 조준합니다.
5. 방을 클리어한 뒤 좌우 출구로 이동합니다. 능력 카드와 상점은 `1/2/3`, 컷신은 `Space/Enter`, 일시정지는 `Esc`입니다.
6. 결과 화면에서 `R`은 처음부터 재시작, `H`는 전적, `T`는 타이틀입니다.

기본 출력은 1920×1080 FHD 전체화면이며 내부 UI 기준은 640×360입니다. 개인정보는 저장하지 않고 결과 요약만 `PlayerPrefs`에 최근 20판까지 저장합니다.

## 검증 명령

Windows PowerShell 기준입니다.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe'
& $unity -batchmode -nographics -quit -projectPath (Get-Location) -logFile build-compile.log
& $unity -batchmode -nographics -projectPath (Get-Location) -runTests -testPlatform EditMode -testResults Tests/editmode-results.xml -logFile tests-editmode.log
New-Item -ItemType Directory -Force Builds/CMP-208 | Out-Null
& $unity -batchmode -nographics -quit -projectPath (Get-Location) -buildTarget Win64 -buildWindows64Player Builds/CMP-208/SoulArcher.exe -logFile Builds/CMP-208/build.log
```

테스트 결과 XML은 `Tests/editmode-results.xml`, Windows 실행 파일은 `Builds/CMP-208/SoulArcher.exe`에 생성됩니다. 빌드 후 `Start-Process .\Builds\CMP-208\SoulArcher.exe`로 실행하고 5초 이상 타이틀 화면을 확인할 수 있습니다.

## 구현 구조

- `Assets/Scripts/SoulArcherModels.cs`: 스테이지·방 그래프, 플레이어 체력/무적/난이도, 자동 조준, 6종 능력 중첩, 상점 경제와 최근 20판 통계 모델.
- `Assets/Scripts/SoulArcherGame.cs`: 타이틀·난이도·컷신·3스테이지 진행, 전투/보스 패턴, 투사체, 상점·능력·HUD·미니맵·결과 화면.
- `Tests/Editor/SoulArcherModelsTests.cs`: 자동 조준, 이동 중 발사 금지, 피해/무적/사망, 방 재방문, 보상/상점, 능력 후보/중첩, 난이도와 전적 검증.
- `Assets/Resources/Art` 및 `Assets/Resources/Audio`: 승인된 설계 저장소 자산을 런타임 `Resources.Load`로 연결한 사본. 원본 경로는 `Documentation/AssetManifest.md`에 기록합니다.

## 알려진 제한사항

- 최소 작동 버전은 외부 유료 패키지 없이 Unity 기본 `SpriteRenderer`와 IMGUI를 사용합니다. 승인 PNG·WAV는 실제 런타임 폴백/배경·배우·적·보스·발사체·음악 연결에 사용됩니다.
- 화면 밖 적의 고급 경고 화살표, 프레임 단위 애니메이션과 세밀한 지형 충돌은 후속 연출 개선 영역입니다. 핵심 이동·정지 자동 공격·방 상태·보스·결과 흐름은 유지됩니다.
- WebGL, 모바일, 영어 현지화와 외부 배포는 범위에 포함하지 않습니다.
