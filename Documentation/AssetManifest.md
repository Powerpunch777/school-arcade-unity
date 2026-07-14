# 구현 저장소 자산 manifest

설계 저장소 `C:\Users\young\Documents\Game Design\school-arcade-design`는 읽기 전용으로 유지했습니다. 아래 자산은 `References/Art`와 `References/Audio`에서 구현 저장소로 복사했으며, `Assets/Resources` 아래에서 Unity 런타임이 로드합니다.

| 구현 경로 | 원본 경로 | 런타임 연결 |
|---|---|---|
| `Assets/Resources/Art/Player/Leon` | `school-arcade-design/References/Art/Player/Leon` | 레온·화살 |
| `Assets/Resources/Art/Enemies/Forest` | `school-arcade-design/References/Art/Enemies/Forest` | 숲 늑대·독버섯 |
| `Assets/Resources/Art/Enemies/Cave` | `school-arcade-design/References/Art/Enemies/Cave` | 바위 딱정벌레·수정 정령 |
| `Assets/Resources/Art/Enemies/Castle` | `school-arcade-design/References/Art/Enemies/Castle` | 방패 해골병·암흑 마법사 |
| `Assets/Resources/Art/Bosses/Forest` | `school-arcade-design/References/Art/Bosses/Forest` | 거대 나무 정령 |
| `Assets/Resources/Art/Bosses/Cave` | `school-arcade-design/References/Art/Bosses/Cave` | 거대 박쥐 |
| `Assets/Resources/Art/Bosses/Castle` | `school-arcade-design/References/Art/Bosses/Castle` | 마왕 기사 모르칸 |
| `Assets/Resources/Art/Environment/Forest`, `ForestStructures` | `school-arcade-design/References/Art/Environment/Forest*` | 숲 방 배경 |
| `Assets/Resources/Art/Environment/Cave` | `school-arcade-design/References/Art/Environment/Cave` | 동굴 방 배경 |
| `Assets/Resources/Art/Environment/Castle` | `school-arcade-design/References/Art/Environment/Castle` | 성 방 배경 |
| `Assets/Resources/Art/Illustrations` | `school-arcade-design/References/Art/Illustrations` | 타이틀·도입·엔딩 입력 |
| `Assets/Resources/Art/UI` | `school-arcade-design/References/Art/UI` | UI·미니맵·능력 입력 |
| `Assets/Resources/Audio/*.wav` | `school-arcade-design/References/Audio/*.wav` | 타이틀/스테이지 BGM 및 효과음 입력 |

현재 런타임은 `Resources.Load<Texture2D>`로 캐릭터·적·보스·방 배경을 로드하고, `Resources.Load<AudioClip>`로 타이틀/스테이지 음악을 로드합니다. PNG/WAV 외 README와 원본 설명 파일도 추적해 승인 자산의 출처를 보존합니다.
