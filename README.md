# 폐합 · CLOSURE

철거 전날 하룻밤, 폐쇄된 지질관측소를 측량하는 1인칭 분위기 공포 미스터리 게임입니다. Unity 6 / URP로 만든 Windows 게임이며, 이 버전은 **전체 흐름을 검증하는 단축 프로토타입**입니다.

## 설치

[Releases](https://github.com/gamparda/closure/releases/latest)에서 `Closure-Setup-x.y.z.exe`를 받아 실행하세요.

- 관리자 권한 없이 `%LOCALAPPDATA%\Programs\Closure`에 설치됩니다.
- 서명되지 않은 설치 파일이라 Windows SmartScreen 경고가 나올 수 있습니다.
- 저장 기록은 설치 폴더 밖(`%USERPROFILE%\AppData\LocalLow`)에 있어 업데이트하거나 삭제해도 유지됩니다.

**자동 업데이트:** 타이틀 화면에서 최신 릴리스를 확인합니다. 새 버전이 있으면 `U`로 업데이트, `N`으로 건너뜁니다. 설치 파일의 SHA-256을 검증한 뒤 조용히 설치하고 게임을 다시 시작합니다. 네트워크가 없거나 검증에 실패하면 아무것도 바꾸지 않습니다.

## 조작

마우스는 **시점 전용**입니다. 커서는 항상 숨겨져 있고, 모든 메뉴는 키보드로 조작합니다.

| 입력 | 동작 |
|---|---|
| WASD / Shift | 이동 / 빠르게 걷기 |
| 마우스 | 시점 회전 |
| 클릭 또는 Space | 장비로 측정 |
| 마우스 휠 또는 Z / X | 측정 대상 바꾸기 (대상 쪽으로 자동 조준) |
| P | 기준점 설치 / 회수 |
| Q | 확인한 K점과 설치한 P점 사이 전환 |
| E | 가까운 기준점 채택 / 기록 조사 / 충전 / 구역 이동 |
| R | 감독관 무전 (10분 소모, 도움 이력 기록) |
| F | 손전등 |
| Tab | 야장 |
| Esc | 작업 중지 |
| **메뉴 안에서** | `↑ ↓` 선택, `Space` 확인, `Q E` 또는 `← →` 페이지, `Esc` 닫기 |
| 서명 | `Space`를 길게 누르기 |

화면에는 시계, 배터리, 장비 LCD만 항상 표시됩니다. 나머지 정보는 야장에서 확인하고, 안내 문구는 잠깐 나타났다 사라집니다.

## 구현 내용

- 지상 작업부터 시작하는 8개 지하 구역과 최종 서명
- K 기준(1991 연구자 기준점)과 P 기준(내가 설치한 점)의 측정 차이, 이분 탐색, 고저차, 교회법, 중복 ID, 부분 폐합
- 기준점 설치·회수, 배터리·충전·행동 시간, 수치로 드러나지 않는 불안정도(화면 해상도와 질감으로 전달)
- 불변 측정 원본, 수동 의심 표시, K/P 채택 또는 보류, 도움 기록
- 미확인 공간 U-1~U-6과 최종 폐합, A~E 결말 (B 서명은 모든 구역 판정과 낮은 불안정도가 필요, D는 폐합 필요)
- 05:00 마감 후에도 서명 선택 유지, 자동 저장과 이어하기, 성과도 텍스트 보관

## 개발

`Unity` 폴더를 Unity Hub에서 프로젝트로 추가하세요. 에디터는 **Unity 6.6 (6000.6.5f1)**, URP 17.6, Input System 1.19를 사용합니다. 첫 패키지 설치에는 인터넷이 필요합니다.

1. 프로젝트를 열면 Editor 스크립트가 URP 자산, 블렌더 소품 프리팹, `Assets/Scenes/Main.unity`를 생성합니다.
2. 입력 시스템 활성화에 따른 에디터 재시작 요청이 표시되면 재시작합니다.
3. `Main` 씬에서 Play를 누릅니다.
4. `CLOSURE → Windows 실행 파일 빌드`는 `Unity/Builds/Windows/Closure.exe`를 생성합니다.

| 경로 | 내용 |
|---|---|
| `Unity/Assets/Resources/SurveyData.json` | 구역별 측정 테이블, 기록, 결말. **게임 데이터의 원본** |
| `Unity/Assets/Scripts/SurveyEngine.cs` | 측량 규칙 (Unity API에 의존하지 않는 순수 C#) |
| `Unity/Assets/Scripts/ClosureRuntime.cs` | 월드 생성, 입력, 오디오 |
| `Unity/Assets/Scripts/ClosureUI.cs` | HUD와 키보드 메뉴 |
| `Unity/Assets/Scripts/ClosureUpdater.cs` | 자동 업데이트 |
| `tests/Closure.Core.Tests` | 규칙 테스트 (Unity 없이 `dotnet test`) |
| `installer/closure.iss` | Inno Setup 설치 스크립트 |
| `ObservatoryAssets.blend` | 블렌더 소품 편집 원본 (`Unity/Assets/Art/Observatory.fbx`로 내보냄) |

### 테스트

.NET 8 SDK가 있으면 Unity 에디터나 라이선스 없이 측량 규칙을 검증할 수 있습니다.

```bash
dotnet test tests/Closure.Core.Tests
```

8개 구역 전체 경로, 이분 탐색, 원본 보존, 서명 조건, 진 엔딩 폐합 같은 규칙을 검증합니다. 렌더링, 입력, UI는 포함되지 않습니다.

## 자동 릴리스

`main`에 `Unity/`, `installer/`, 워크플로 변경이 푸시되면 `.github/workflows/release.yml`이 다음을 자동으로 수행합니다.

1. 규칙 테스트 (`dotnet test`)
2. 마지막 릴리스 태그의 patch를 올린 새 버전 계산 (태그가 없으면 `0.1.0`)
3. Unity Windows 빌드 (`Closure.Editor.ClosureBuild.BuildWindowsCI`, 버전은 `-closureVersion`으로 주입)
4. Inno Setup 설치 파일 `Closure-Setup-x.y.z.exe`와 `.sha256` 생성
5. GitHub Release 게시

Actions 탭의 **Release → Run workflow**에서 버전을 직접 지정하거나, 게시를 끄고 빌드만 점검할 수 있습니다. 주 버전·부 버전을 올리려면 `1.0.0` 같은 값을 한 번 지정하면 이후 patch가 이어집니다.

### 자동 릴리스 준비 (최초 1회)

GitHub Actions의 Unity 빌드에는 Unity 라이선스가 필요합니다. 저장소 **Settings → Secrets and variables → Actions**에 아래를 등록하세요. 절차는 [GameCI 활성화 문서](https://game.ci/docs/github/activation)를 따릅니다.

| 시크릿 | 내용 |
|---|---|
| `UNITY_LICENSE` | 활성화로 얻은 `.ulf` 라이선스 파일 내용 |
| `UNITY_EMAIL` | Unity 계정 이메일 |
| `UNITY_PASSWORD` | Unity 계정 비밀번호 |

Unity 버전(`ProjectSettings/ProjectVersion.txt`)에 맞는 GameCI 이미지가 없으면 빌드가 실패합니다. 이 경우 에디터 버전을 GameCI가 지원하는 버전으로 맞추거나, 라이선스가 있는 PC를 self-hosted runner로 등록하세요.

## 현재 범위

구역들은 공통 관측소 모듈을 변형한 초기 레벨입니다. 긴 분량의 독립적인 레벨 설계, 패드 조작, 녹음 대사, 접근성 설정, 상용 수준의 연출·성능 조정은 후속 작업입니다. 교회법과 수준측량은 계산을 장비가 처리하는 단축 퍼즐입니다. 측정 대상은 월드의 표식을 바라보는 방식이 아니라 휠로 고릅니다.
