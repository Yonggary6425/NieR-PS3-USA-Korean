# NieR PS3 북미판 한글패치

PS3 북미 디스크판 **NIER (BLUS30481 / 1.00)** 비공식 한글패치입니다. 아버지 니어가 주인공인 판본으로, Xbox 360 일본판 NieR Gestalt의 한국어 번역을 PS3 북미판에 이식했습니다. 음성은 원본 영어 그대로입니다.

- 제작: 용가리 ([@Yonggary6425](https://github.com/Yonggary6425))
- 번역·패처 제작 보조: OpenAI Codex
- 버전: v1.0

> 이 저장소에는 게임 데이터가 들어 있지 않습니다. 직접 덤프한 원본 게임이 있어야 하며,
> 패처는 원본에 **바뀐 부분만** 적용해 새 한글판을 만듭니다.

## 한글화 범위

| 항목 | 내용 |
|---|---|
| 대사·텍스트 | PS3 북미판 본문·메뉴·UI 번역 (NieR Gestalt 한국어 번역 이식) |
| 자막 | 영상 자막 크기 26/20 → 28/22 확대, 긴 자막 줄바꿈 조정 |
| 폰트 | 두 게임 폰트에 한글 각 1,161자, 글자 위아래 잘림 방지 여백 2픽셀 |
| 메뉴 | 우측 상단 경험치 `EXP` 표기 |
| 트로피 | 원본 유지(실기 권장) 또는 한글 중 선택 |

손대지 않은 것: 영어 음성, 동영상 파일. 일본판 레플리칸트 전용 오프닝 자막·타이틀 수정은 포함하지 않습니다.

## 다운로드

[Releases](../../releases/latest)에서 `NieR-PS3-USA-Korean-v1.0-PowerShell.zip`을 받으세요. 배치파일만 따로 받으면 작동하지 않으니 ZIP 전체를 받아야 합니다.

Windows 10/11 기본 구성(Windows PowerShell 5.1, .NET Framework)만으로 동작합니다. Python이나 별도 프로그램 설치, 인터넷 연결이 필요 없습니다.

## 준비물

- 수정하지 않은 BLUS30481 / 1.00 원본: **복호화된 단일 `.iso`** 또는 `PS3_GAME`이 들어 있는 게임 폴더
- 원본이 있는 드라이브의 여유 공간: ISO 작업 **최소 32 GiB**, 폴더 작업 **최소 18 GiB**
- ISO 출력 드라이브는 4GB 이상 파일을 저장할 수 있는 NTFS 또는 exFAT

암호화된 ISO, 분할 ISO(`.iso.0` 등), 다른 지역·버전, 일본판 레플리칸트, Xbox 360판은 지원하지 않습니다. 디스크 복호화 기능은 들어 있지 않습니다. 이미 한글패치한 게임에 덧씌우지 말고 원본에서 새로 적용하세요.

## 적용 방법

1. 받은 ZIP의 **압축을 전부** 새 폴더에 풉니다. 패처는 게임 폴더 밖에 두세요.
2. 원본 **ISO 또는 게임 폴더**를 `Apply-Korean-Patch.bat` 위로 끌어다 놓습니다.
   - `PS3_GAME` 폴더를 끌어다 놓아도 상위 게임 폴더를 찾습니다. 더블클릭으로 실행하면 창에 경로를 끌어다 놓고 Enter를 누르면 됩니다.
3. 적용 방식을 고릅니다.
   - **1: 새 사본 생성** — 원본 옆에 `_Korean` 이름으로 저장합니다. 원본은 그대로입니다.
   - **2: 원래 위치에 적용** — 검증을 마친 결과로 교체하고, 원본은 `_OriginalBackup_날짜` 이름으로 옆에 보관합니다.
4. 트로피를 고릅니다. **1: 원본(PS3 실기 권장) / 2: 한글(RPCS3용)**
5. 초록색 **완료** 메시지가 나오면 끝입니다. 두 질문에서 Enter만 누르면 "새 사본 + 원본 트로피"로 진행합니다.

| 선택 | 결과 |
|---|---|
| 1번 + `Nier.iso` | 같은 폴더에 `Nier_Korean.iso` 생성 |
| 1번 + `Nier` 폴더 | 같은 위치에 `Nier_Korean` 폴더 생성 |
| 2번 + `Nier.iso` | `Nier.iso`가 한글판으로 바뀌고 원본은 `Nier_OriginalBackup_날짜.iso` |
| 2번 + `Nier` 폴더 | `Nier`가 한글판으로 바뀌고 원본은 `Nier_OriginalBackup_날짜` 폴더 |

같은 이름이 있으면 `(2)`, `(3)`을 붙이며 기존 파일이나 백업을 덮어쓰지 않습니다.

### 원래대로 되돌리기
2번으로 적용했다면 게임을 끈 뒤, 지금의 한글판을 다른 이름으로 옮기고 `_OriginalBackup_날짜` 백업을 원래 이름으로 바꾸면 됩니다. 백업은 자동으로 지우지 않습니다. 1번은 원본을 건드리지 않으므로 만들어진 `_Korean` 결과만 지우면 됩니다.

### 명령행 사용 (선택)
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\EasyPatch.ps1 -Source "E:\Games\Nier (USA).iso" -Mode copy -Trophy original
```
`-Mode`는 `copy` 또는 `replace`, `-Trophy`는 `original` 또는 `korean`입니다.

## 참고

- **PS3 실기**: 한글 트로피를 넣으면 트로피 등록 오류 **80030C18**로 실행되지 않는다는 제보가 있습니다. 실기에서는 **원본 트로피**를 고르세요. 실기 부팅은 아직 확인하지 못했습니다.
- **RPCS3**: 기존 폴더판 시험에서 콘솔 언어 English (US), Write Color Buffers 켬 상태로 초반 전투를 확인했습니다. ([RPCS3 이슈 #14771](https://github.com/RPCS3/rpcs3/issues/14771))
- **기록 파일**: 성공하면 ISO 옆 `.patch-report.json` 또는 폴더 안 `PATCH-VERIFIED.json`에 검증 결과가 남습니다. 실패하면 화면에 표시된 작업 폴더와 백업을 지우지 말고 보존하세요.

## 검증 범위

- 실제 북미판 ISO로 원본 트로피·한글 트로피 두 경우 모두 적용을 완료했고, 결과 ISO 안 14개 파일을 해시로 검사했습니다. 별도 ISO 분석기로 ISO9660·Joliet 양쪽에서 다시 읽어 일치를 확인했습니다. (`ISO-PATCHER-VERIFICATION.json`)
- 새 사본 / 원래 위치 적용, 실패 시 원본 복원 흐름을 시험했습니다. (`EASY-PATCHER-VERIFICATION.json`)
- 결과 ISO는 항상 같은 바이트로 만들어집니다. 원본 트로피 `228e1cb9…846786d`, 한글 트로피 `1c448458…de0f427` (SHA-256).
- 게임 전체 구간, 이번 ISO 결과물의 RPCS3·PS3 실기 부팅은 검증하지 않았습니다.

## 저장소 구성

```
EasyPatch.ps1, EasyPatch.Common.ps1   시작 스크립트 (적용 방식·트로피 선택, 자동 경로, 백업·교체)
Apply-Korean-Patch.ps1                ISO/폴더 판별, ISO 추출·생성·검증
Apply-FolderPatch.ps1                 본문·폰트·트로피 패치 (폴더 단위)
NierPatch.cs                          STABLE.SDAT 복호화·재암호화, 아카이브 재구성
NierIso.cs                            외부 프로그램 없이 동작하는 ISO 읽기·쓰기
ps3-local-delta.zip                   원본에 맞춰서만 쓸 수 있는 변경 데이터
licenses/, NOTICE.md                  출처와 권리 고지
*-VERIFICATION.json, BUILD-INFO.txt   검증 기록
```

실행용 `Apply-Korean-Patch.bat`는 Releases의 ZIP에 들어 있습니다.

## 오류 제보

[Issues](../../issues)에 게임 ID·버전, ISO/폴더 여부, 트로피 선택, 실행 환경(실기/RPCS3), 오류 메시지를 알려 주세요. 검증 JSON을 첨부해도 됩니다(개인 경로는 지워 주세요). 원본 ISO·EBOOT·SDAT·TRP 파일은 첨부하지 마세요.

## 권리 고지

NIER의 저작권은 SQUARE ENIX에 있습니다. 이 패치는 비공식·비상업적 팬 번역이며 공식과 관련이 없습니다. 정품을 소유한 분만 사용해 주세요. 세부 고지는 `NOTICE.md`와 `licenses/`를 참고하세요. 형식·폰트·도구 참고: [NieR-Replicant-PS3-Korean](https://github.com/Yonggary6425/NieR-Replicant-PS3-Korean)
