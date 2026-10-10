# 소울 아처 (Soul Archer)

**움직이면 회피하고, 멈추면 공격합니다.** 정지하면 가까운 적을 자동으로 조준하는 2D 액션 게임으로, 위험을 피할지 공격할 시간을 확보할지 계속 판단하도록 설계했습니다.

[English](README.md) · [게임 기획 문서](https://github.com/Powerpunch777/school-arcade-design) · [SPS 개발 작업환경](https://github.com/Powerpunch777/SPS)

![CMP-265 빌드에서 직접 캡처한 첫 숲 전투 화면](Documentation/Media/forest-combat.jpg)

*바탕화면에 제공된 CMP-265 빌드를 실행하고 일반 메뉴로 진행해 캡처했습니다. 왼쪽은 플레이어, 왼쪽 위는 체력, 오른쪽 위는 방 연결을 보여 주는 미니맵입니다.*

## 프로젝트와 담당 역할

생성형 AI가 게임 개발에 어떤 도움을 줄 수 있는지 직접 확인하기 위해 시작한 학교 프로젝트입니다. 저는 기획과 핵심 규칙 설계, 구현 방향 결정, 플레이테스트를 맡았고, AI를 아이디어의 빠른 구현과 시각자료 제작을 돕는 도구로 활용했습니다.

개발 중 그래픽이 의도대로 표시되지 않는 문제를 발견해 생성형 이미지로 대체 자료를 만들고 수정했습니다. 게임이 너무 쉽고 빨리 끝나는 문제는 몬스터 체력과 보스 패턴을 조정하며 개선했습니다. 처음의 아이디어를 그대로 구현하는 것만큼 실제로 플레이하고 수정하는 과정이 중요하다는 점을 배웠습니다.

작업환경으로는 함께 개발 중인 [SPS](https://github.com/Powerpunch777/SPS)를 사용했습니다. 게임을 만들면서 도구의 불편한 점을 발견하고, 게임과 작업환경을 함께 개선했습니다.

![첫 방을 클리어한 뒤 이동 속도, 관통, 공격 속도 중 하나를 고르는 화면](Documentation/Media/ability-selection.jpg)

*첫 방을 클리어한 뒤 실제로 도달한 능력 선택 화면입니다. 이 장면에서는 이동 속도, 관통, 공격 속도 중 하나를 선택합니다.*

<details>
<summary>타이틀 화면 보기</summary>

![CMP-265의 소울 아처 타이틀 화면](Documentation/Media/title.jpg)

</details>

## 구현 개요

Unity `6000.5.3f1` 기반 Windows PC용 2D 액션 게임 구현입니다. 시작 씬은 `Assets/Scenes/ForestSlice.unity`이며 타이틀에서 난이도, 3장 도입 컷신, 숲·동굴·마왕의 성 캠페인, 공주 구출 엔딩과 결과/전적 화면으로 이어집니다.

## 실행

1. Unity Hub에서 이 폴더를 Unity `6000.5.3f1`로 엽니다.
2. `Assets/Scenes/ForestSlice.unity`를 연 뒤 Play를 누릅니다.
3. 타이틀에서 `[1] 새 게임`을 선택하고 `[1] 쉬움` 또는 `[2] 보통`을 고릅니다.
4. WASD/방향키로 이동합니다. 이동 중에는 새 화살을 발사하지 않으며, 정지하면 화면 안의 가장 가까운 살아 있는 적을 자동 조준합니다.
5. 방을 클리어한 뒤 상하좌우 출구로 이동합니다. 능력 카드·상점·메뉴는 마우스 또는 `1/2/3`, 컷신은 마우스나 `Space/Enter`, 일시정지는 `Esc`입니다.
6. 결과 화면에서 `R`은 처음부터 재시작, `H`는 전적, `T`는 타이틀입니다.

기본 출력은 1920×1080 FHD 전체화면이며 내부 UI 기준은 640×360입니다. 개인정보는 저장하지 않고 결과 요약만 `PlayerPrefs`에 최근 20판까지 저장합니다.

## 공개 저장소 범위

이 저장소는 실행 가능한 Unity 소스, 최종 런타임 에셋, 프로젝트 설정과 필요한 라이선스 고지를 포함합니다. 빌드 결과물, Unity 로컬 캐시, 자동 테스트 결과, 전체 QA 스크린샷 아카이브와 이미지 생성 중간산출물은 Git에서 제외합니다. README 소개용 실행 화면만 [Documentation/Media](Documentation/Media/README.md)에 별도로 포함합니다.

프로젝트의 원본 코드와 자체 제작 리소스는 별도 오픈소스 라이선스를 부여하지 않습니다. 포함된 제3자 글꼴·UI·타일은 [THIRD_PARTY_NOTICES.md](Documentation/THIRD_PARTY_NOTICES.md)에 적힌 각 라이선스를 따릅니다.

## 검증 명령

Windows PowerShell 기준입니다.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Unity.exe'
& $unity -batchmode -nographics -quit -projectPath (Get-Location) -logFile build-compile.log
& $unity -batchmode -projectPath (Get-Location) -runTests -testPlatform EditMode -testResults Tests/editmode-results.xml -logFile tests-editmode.log
New-Item -ItemType Directory -Force Builds/CMP-208 | Out-Null
& $unity -batchmode -nographics -quit -projectPath (Get-Location) -buildWindowsPlayer Builds/CMP-208/SoulArcher.exe -logFile Builds/CMP-208/build.log
```

테스트 결과 XML은 `Tests/editmode-results.xml`, Windows 실행 파일은 `Builds/CMP-208/SoulArcher.exe`에 생성됩니다. 렌더링을 확인하는 EditMode 테스트가 있으므로 테스트 명령에는 `-nographics`를 사용하지 않습니다. 빌드 후 `Start-Process .\Builds\CMP-208\SoulArcher.exe`로 실행하고 5초 이상 타이틀 화면을 확인할 수 있습니다.

## 구현 구조

- `Assets/Scripts/SoulArcherModels.cs`: 스테이지·방 그래프, 플레이어 체력/무적/난이도, 자동 조준, 6종 능력 중첩, 상점 경제와 최근 20판 통계 모델.
- `Assets/Scripts/SoulArcherGame.cs`: 타이틀·난이도·컷신·3스테이지 진행, 전투/보스 패턴, 투사체, 상점·능력·HUD·미니맵·결과 화면.
- `Assets/Tests/Editor/SoulArcherModelsTests.cs`: 자동 조준, 이동 중 발사 금지, 피해/무적/사망, 분기 방 재방문, 보상/상점, 능력 후보/중첩, 난이도, 전적과 전체 3스테이지 진행 검증.
- `Assets/Resources/Art` 및 `Assets/Resources/Audio`: 승인된 설계 저장소 자산을 런타임 `Resources.Load`로 연결한 사본. 원본 경로는 `Documentation/AssetManifest.md`에 기록합니다.

## 알려진 제한사항

- 외부 유료 패키지 없이 Unity 기본 `SpriteRenderer`와 IMGUI를 사용합니다. 승인 PNG 스프라이트 시트를 런타임에서 셀 단위로 잘라 플레이어·적·보스 애니메이션과 타일 배경에 사용하고, 승인 WAV를 스테이지 음악과 주요 효과음에 연결합니다.
- 화면 밖 적의 고급 경고 화살표와 방 내부 장애물의 세밀한 물리 충돌은 후속 연출 개선 영역입니다. 방 경계 반사, 배우·투사체 충돌, 이동·정지 자동 공격, 방 상태, 보스와 결과 흐름은 구현되어 있습니다.
- WebGL, 모바일, 영어 현지화와 외부 배포는 범위에 포함하지 않습니다.
