# MiniChess 프로젝트 분석

분석 기준: 2026-11-12, 로컬 작업 트리. 후속 작업용 구조 안내이며 전체 결함 감사나 Unity 실행 검증을 의미하지 않는다.

## 저장소와 현재 상태

| 구분 | 위치 | 역할 |
|---|---|---|
| Unity | `../MiniChess_unity` | 클라이언트, 화면·입력·연출, 로컬 대전, 봇 |
| 서버 | `../MiniChess_server` | ASP.NET Core WebSocket 서버, 매칭, 경기 운영 |
| Unity 코어 | `Assets/Scripts/Core` | `MiniChess_Core` Git 서브모듈 |
| 서버 코어 | `../MiniChess_server/Core` | 같은 코어 저장소의 별도 체크아웃 |

애셋 작업 흐름: `../MiniChess_EffectTest`에서 필요한 애셋을 테스트·커스터마이즈한 뒤, 결과와 필요한 의존성만 실 클라이언트 `MiniChess_unity`로 복사한다. EffectTest는 실 클라이언트가 아니다.

AP/HP 토큰은 EffectTest의 Dragon을 기반으로 한 `Resources/Tokens/Materials/M_Token_AP.mat`, `M_Token_HP.mat`을 각각 사용한다. 본체와 프레넬 색은 HUD와 같은 AP 라벤더 `#B99CFF`, HP 연두 `(0.4, 0.8, 0.5, 1)`이다. 셰이더는 `Art/Tokens/Shaders/TokenDistortion.shader` (`MiniChess/Tokens/Distortion`), 노멀/높이 텍스처는 `Art/Tokens/Textures/`, 메시는 `Resources/Tokens/Meshes/SM_TokenGem.asset`, 커스텀 Inspector는 `Scripts/Client/Editor/TokenDistortionShaderGUI.cs`에 둔다. 토큰 메시/생성기에 UV0 및 흰 정점 색을 추가했고 URP의 Opaque Texture도 활성화했다. 원래 공용 머티리얼은 HP 전용으로 이름을 바꾸고 GUID를 유지했으며, AP 전용 머티리얼은 새 GUID로 분리했다.

토큰은 `Resources/Tokens/Prefabs/P_Token_HP.prefab`, `P_Token_AP.prefab`으로 분리했다. `TokensView`는 종류에 맞는 프리팹을 Resources에서 불러와 보드 좌표에 Instantiate하고, 코어 상태가 바뀌면 제거/교체한다. Inspector의 hpPrefab/apPrefab으로 별도 프리팹을 지정할 수도 있다. 메시·전용 머티리얼·크기·높이는 각 프리팹의 Gem에서 편집한다. 루트의 `TokenView`가 Gem을 회전/부유시키며, 속도·진폭도 프리팹별로 저장한다. HP/AP 떠 있는 텍스트와 관련 TMP·Billboard·테두리 설정은 제거했다.

프리팹 내부 fileID/외부 GUID 참조 검증을 통과했다. Unity 프로젝트의 실제 참조 어셈블리를 사용한 독립 C# 컴파일로 Client 114개 소스와 Editor 23개 소스를 검사했다. Unity 에디터의 프리팹 임포트·플레이 및 실제 셰이더 렌더링은 아직 확인하지 않았다.

두 코어는 자동으로 동기화되는 한 디렉터리가 아니다. 각각의 변경·커밋과 상위 저장소의 서브모듈 포인터를 관리해야 한다.

| 항목 | Unity 쪽 | 서버 쪽 |
|---|---|---|
| 코어 실제 HEAD | `6373807904f417bf4265b5b77c47882b3ebdd18e` | `6373807904f417bf4265b5b77c47882b3ebdd18e` |
| 상위 저장소의 코어 포인터 | `6373807904f417bf4265b5b77c47882b3ebdd18e` | `6373807904f417bf4265b5b77c47882b3ebdd18e` |
| `ProtocolInfo.Version` | 9 | 9 |
| 코어 내부 작업 트리 | 변경 없음 | 변경 없음 |

토큰 작업 당시 서버 코어를 `1c7515b`로 동기화했고, 이후 피로도 구현 커밋 `6373807`로 양쪽을 다시 맞췄다. 코어 원격 `main`도 `6373807`이며 서버 통합 테스트 15개가 통과했다. 실제 배포된 실행 파일의 버전까지 확인한 것은 아니다. 동기화 전 서버 체크아웃(`e6067bf`) 대비 다음 네 커밋이 포함된다.

피로도 기능은 코어 커밋 `6373807`로 커밋·푸시했으며 양쪽 코어에 동일하게 반영했다. 기본 규칙은 8 라운드부터 각 팀 턴 종료마다 모든 보드 위 유닛에게 피해 1, 라운드마다 +1이다. `GameRuleData.Fatigue`/`FatigueRuleData`로 수치를 설정하고 null이면 끌 수 있다. `Turns/FatigueSystem.cs`가 종료형 회복·DoT·장판 뒤, 상태 지속시간 감소 전에 실행한다. 기존 보호막·봉인 규칙을 적용하며 소환물도 포함한다. `DamageSystem.ApplyBatch`로 전멸 판정을 피해 묶음 뒤로 미뤄 양 팀 동시 전멸은 무승부로 처리한다. `DamageType.Fatigue` 추가와 규칙 변경으로 프로토콜을 9로 올렸다. 별도 피로도 누적 상태 없이 턴 번호로 피해량을 계산하므로 스냅샷 복원·봇 Fork가 같은 라운드 규칙을 사용한다.

- `7601d82`: 이동 미리보기 `GetMovePreview`
- `891254f`: 기본 공격 사거리 `GetAttackReach`
- `507efc3`: 지정 칸·회전을 반영하는 `GetSkillFailReason`
- `1c7515b`: 힐/AP 토큰, 스냅샷·이벤트 확장, 프로토콜 8

분석 시작 시 서버 상위 저장소의 `M Core`는 기록된 포인터(`1969f45`)와 실제 체크아웃(`e6067bf`)의 차이였다. 포인터는 토큰 작업에서 `1c7515b`, 피로도 작업에서 `6373807`로 갱신했다. Unity 상위 저장소에는 분석 전부터 씬, 머티리얼, 패키지, 프로젝트 설정, `.claude` 스킬 수정과 새 애니메이션 등이 있었다. 해당 변경은 보존하고 토큰 작업 커밋에서 제외했다. 적용되는 `AGENTS.md`는 상위 경로 및 두 프로젝트 파일 검색에서 발견되지 않았다.

## 기술 구성

- Unity `2022.3.55f1`, URP `14.0.11`, TMP `3.0.7`, Timeline `1.7.6`, NovaShader `3.6.0`.
- 어셈블리: `MiniChess.Client` → `MiniChess.Bot`, `MiniChess.Core`, `MiniChess.Protocol`, TMP. 봇은 Core만 참조하며 Unity 엔진 의존성을 금지한다.
- Core/Protocol: `netstandard2.1`, C# 9, nullable 비활성. Protocol은 Newtonsoft.Json `13.0.2`를 사용한다.
- 서버: ASP.NET Core `net8.0`, nullable 활성. Core/Protocol 프로젝트를 직접 참조한다.
- Core 테스트·봇 테스트·서버 테스트: .NET 8 + xUnit. Unity 코어 테스트용 asmdef도 있다.
- Unity MCP 관련 패키지와 `.claude/skills`가 존재한다. 이번 분석은 파일을 직접 읽어 진행했고 에디터를 제어하지 않았다.

## 전체 실행 흐름

```text
사람 입력 / BotDriver
        ↓
GameSession.Try* → Submit(CommandMessage)
        ├─ 로컬: CommandExecutor.Execute(Authority, team, command)
        └─ 온라인: ServerConnection → /ws → Lobby → MatchRoom
                                               ↓
                                   CommandExecutor → GameController
                                               ↓
                              검증 → Action → GameState / 이벤트
                                               ↓
                         팀별 GetSnapshot / GetVisibleEvents
                                               ↓
                          StateUpdate → Restore → GameSession.Present
                                               ↓
                              Units / Board / HUD / Effects 갱신
```

판정은 코어가 담당한다. UI나 이펙트에서 상태를 직접 수정하면 로컬·온라인·봇의 결과가 어긋날 수 있다.

## 코어: 판정과 확장 지점

기준 경로는 `Assets/Scripts/Core/src/MiniChess.Core/`이다.

| 위치 | 책임 |
|---|---|
| `Controller/GameController.cs` | 생성, 명령, 조회, 스냅샷, 복원, 탐색용 복사 진입점 |
| `Controller/GameRequests.cs`, `GameResponse.cs` | 요청과 성공/실패 결과 |
| `Actions/` | 배치·이동·공격·장애물 공격·스킬·턴 종료 검증/실행 |
| `State/` | 보드, 유닛, AP, 팀, 경기 단계, 복사 상태 |
| `Movement/` | 경로, 이동 방식, 이동 제약, 밀치기, 칸 진입 처리 |
| `Combat/` | 피해, 보호막, 가로채기, 사망, 종료 판정 |
| `Turns/TurnSystem.cs`, `FatigueSystem.cs` | 시작/종료 효과 순서, AP 회복, 턴 교대, 전체 피로도, 최대 턴 판정 |
| `Characters/Pieces/`, `PieceModules.cs` | 12개 캐릭터 모듈과 등록 |
| `Characters/SkillTuning.cs`, `Presets/PrototypeTestPreset.cs` | 스킬 수치와 플레이 테스트 기본값 |
| `Skills/` | Targeting + Area + Effects + Conditions 조합 |
| `Statuses/` | 상태 동작, 지속시간, 중첩 정책, 표준 상태 라이브러리 |
| `Effects/` | 칸 효과, 구역, 덫, 장애물, 예약 포격 |
| `Capture/`, `Summons/`, `Tokens/` | 점령, 소환물, 힐/AP 토큰 |
| `Snapshots/`, `Events/` | 전송·표시용 상태, 이벤트, 정보 숨김 |

핵심 계약:

- `GameController.Create`: 배치 단계부터 실제 경기를 만든다.
- 명령은 소유 팀·형식·규칙 등을 검증한 뒤 실행하고 실패 이유 enum과 이벤트를 반환한다.
- `GameController.Restore`: 스냅샷 기반 표시·조회 전용 경기다. 명령 실행 및 실행용 Fork가 차단된다.
- `GameController.Fork(viewer, randomSeed)`: 실행 가능한 깊은 복사본이다. 봇은 이를 사용해 결과를 탐색한다. viewer를 지정하면 숨겨진 정보가 제외된다.
- 임시 수치와 정책은 코어 루트 `ASSUMPTIONS.md`에 기록되어 있다. 프리셋 값과 문서를 함께 확인해야 한다.
- 새 캐릭터는 Pieces 구현, PieceModules 등록, 프리셋 수치/튜닝, 클라이언트 시각 자료 등록이 연결된다.

현재 프리셋은 7×7, 4 대 4다. AP 최대 14, 시작 8, 턴 회복 10, 이월 허용, 후공 시작 보너스 4, 일반 이동 칸당 AP 1, 기본 공격 AP 2다. 최대 전체 턴 번호는 200이며 살아 있는 비소환 유닛 수 → 남은 HP 합으로 판정한다. 점령은 자기 턴 시작에 처리하며 2회 연속 유지 시 보상을 준다. 수치는 확정 밸런스로 취급하지 않는다.

턴 시작은 AP 회복 → 유닛 턴 상태 초기화 → 시작 효과 훅 → 토큰 생성 → 점령 순서다. 각 효과 단계에서는 상태 → 칸 효과 → 예약 포격 순으로 처리한다. 턴 종료는 토큰 제거 → 회복 → DoT → 장판 → 전체 피로도 → 지속시간 감소 순서다.

Unity 코어의 토큰은 3라운드부터 2라운드마다 힐/AP 순으로 생성한다. 해당 라운드 양 팀의 턴에 각자 하나씩 생성하며 힐은 HP 3, AP는 4다. 점령 칸을 제외한 배치 가능한 빈 칸을 사용하며, 그 팀의 유닛이 진입하면 칸 효과 처리 뒤 살아 있을 때 먹는다. 미획득 토큰은 턴 종료 때 사라진다.

## 정보 숨김과 통신

`Snapshots/GameVisibility.cs`가 상대 덫 및 예약 포격의 숨김 규칙을 정의하고, `GameProjection.cs`가 스냅샷과 이벤트를 필터링한다. 배치 중 상대 배치도 숨기며 전투 시작 시 공개한다. 스킬을 썼다는 사실은 알리지만 비공개 지정 칸·범위는 제거한다. 이 규칙은 온라인 전송, 사람 대 봇 화면, 봇 판단에 함께 영향을 준다.

공통 통신 코드는 `Core/src/MiniChess.Protocol/`에 있다.

- 봉투: `{ "Type": "Move", "Payload": { ... } }`. enum은 문자열, 좌표는 X/Y 객체다.
- `ProtocolJson`: 직렬화, 파싱, 이벤트 변환, 설정 지문(SHA-256). 모르는 종류나 잘못된 입력은 null로 처리한다.
- `OnlineMatch`: 양쪽에서 공유하는 경기 설정, ConfigHash, 무작위 배치.
- `CommandExecutor`: TurnNumber 검사와 메시지→코어 요청 실행. 로컬과 서버가 공유한다.
- 참가 시 ProtocolVersion과 ConfigHash를 검사한다. 설정 지문은 규칙·맵·캐릭터·튜닝을 포함하지만 실행 로직 자체를 식별하지 않는다. 로직/전송 형식 변경은 버전 관리가 필요하다.
- 서버가 연결로 팀을 결정하며 클라이언트가 팀을 지정하지 않는다.
- 명령에는 RequestId와 TurnNumber가 있다. 방은 같은 마지막 RequestId에 저장된 결과를 재전송하고 더 작은 번호는 무시한다.
- 서버는 상태 갱신을 먼저 큐에 넣고 명령 결과를 보낸다. 전체 스냅샷과 이번 이벤트를 보내는 구조다.
- Ping 10초, 무응답 30초, 메시지 한도 256 KiB. 기본 배치 90초, 전투 턴 120초.

## Unity: 입력, 화면, 연출

기준 경로는 `Assets/Scripts/Client/`이다.

| 위치 | 작업 시 확인할 내용 |
|---|---|
| `Bootstrap/GameSession.cs` | 게임 모드, 실제/표시 경기, 명령 제출, 네트워크 수신, 연출 순서의 중심 |
| `Interaction/SelectionController.cs` | 선택, 공격/이동 후보, 스킬 다중 지정·회전·확정, 미리보기 |
| `Interaction/PlacementController.cs` | 캐릭터 배치 |
| `Network/ServerConnection.cs` | 에디터/PC ClientWebSocket 및 WebGL 브라우저 소켓 분기 |
| `Players/BotDriver.cs`, `BotSearchConfig.cs` | 봇 진행 타이밍, 취소, Unity용 탐색 설정 |
| `Board/` | 보드·칸 강조·장판·상태·스킬·토큰 표시, 맵 로딩 |
| `Units/` | 유닛 뷰, HP·상태 배지, 배치 프리뷰, 피해 숫자 |
| `UI/` | HUD, AP, 스킬 버튼, 접속 패널, 설명, 실패 이유 번역 |
| `Visuals/` | 캐릭터 시각 정보, 무기/손 리그, 애니메이션 이벤트, 상태 연출 |
| `Visuals/Effects/` | 스킬별 이펙트, 캐스터, 레지스트리, 풀링 |
| `Editor/` | 시각 자료 편집 메뉴, 환경 생성, WebGL 빌드 |

`GameSession.Authority`는 로컬 실제 경기다. 온라인에서는 서버가 그 역할을 한다. `Game`은 표시·조회용 경기이며 사람 대 봇에서는 사람 시점으로 투영된다. 두 사람 교대/봇끼리 모드는 실제 경기를 사용한다. `_pendingCommand`는 온라인 명령을 한 번에 하나로 제한하며 결과 대기 시간은 10초다.

연출은 `Present` 대기열로 순서대로 처리한다. 공격 모션을 시작하고 타격 시점까지 이벤트/상태 반영을 미루며, 유닛 뷰가 제거되기 전에 관련 이펙트를 재생한다. 이 경로를 바꾸면 HP·사망·이동·서버 갱신의 타이밍을 함께 확인해야 한다.

피해 숫자는 `Units/DamageNumbersView.cs`가 담당한다. `GameSession.PlayEvents`의 확정 `UnitDamagedEvent`만 트리거로 사용하며 HP 스냅샷 차이, 턴 번호, 피로도 공식으로 숫자를 추측하지 않는다. 온라인에서는 서버가 보낸 이벤트, 로컬에서는 권한 코어가 반환한 이벤트를 사용한다. `AppliedAmount`와 `ShieldAbsorbed`를 각각 표시하며 0은 생략한다. 색은 서버 이벤트의 `DamageType`으로 결정한다. `Fatigue`는 진한 보라색(#9A32E6), 그 밖의 피해는 진한 빨간색(#E02020)이며 보호막 흡수 숫자에도 같은 구분을 적용한다. 숫자는 부호 없이 표시하고 기본 글자 크기는 2.75(기존 5.5의 절반)다. 숫자는 유닛 뷰를 따라 약 0.9초간 떠오르며 사라지고, 같은 유닛의 연속 피해는 간격을 두어 겹침을 줄인다. 유닛의 자식이 아니어서 결정타 후 뷰가 삭제되어도 마지막 위치에서 끝까지 재생하며 새 경기 시작 시 비운다. 피로도·공격·지속 피해·장판·호위 전가를 같은 경로로 처리한다. 관련 Client C# 컴파일 검사는 통과했으며 Unity 플레이 화면 검증은 아직 수행하지 않았다.

시각 자료는 Resources의 CharacterVisualCatalog, SkillEffectCatalog, FieldEffectCatalog, StatusVisualCatalog, SkillDescriptionCatalog, GameOptions 등에서 연결된다. 캐릭터/스킬/상태 ID로 코어와 매핑되므로 코드만 추가하고 에셋 등록을 빠뜨리지 않아야 한다. 풀 이펙트는 Release 시 방출을 중단하고 남은 파티클이 사라진 뒤 반환한다.

현재 빌드 씬은 `Assets/Scenes/Battle.unity`와 `RuinsMap.unity`다. MapLoader는 맵을 Additive로 로드하고 활성 씬으로 설정한다. MapSceneSettings가 코어 보드와 맞는지 검증하며, 맵 루트는 원점/무회전/크기 1이어야 한다.

WebGL은 `Assets/Plugins/WebGL/WebSocketBridge.jslib`를 사용한다. `Editor/WebGLBuildMenu.cs`의 메뉴가 `Builds/WebGL`로 빌드하며 Gzip + decompression fallback, 활성 빌드 씬, CleanBuildCache를 사용한다. 빌드 중 변경한 압축 설정은 되돌리지만 활성 플랫폼은 WebGL로 남는다.

## 봇

`Assets/Scripts/Bot/`는 Unity 없이 빌드할 수 있다. `BotPlayer`가 배치/다음 행동의 공통 진입점이며 한 턴 행동 상한은 기본 40이다. 판단만 반환하고 실행은 호출자가 수행한다.

- Easy: RuleBot, 실수 확률 0.3.
- Normal: SearchBot 깊이 1, BeamWidth 4, 단계별 후보 6.
- Hard: 깊이 2, BeamWidth 6, 후보 8, 응수 후보 6.
- Custom: SearchSettings. 깊이 0 이하면 RuleBot.
- `BotActionGenerator`: 합법 행동 후보, `Rules/`: 보드 분석·행동 점수·정체 대응, `Eval/`: 상태 평가 항목, `Search/`: 빔 탐색.
- PC/에디터에서는 BotDriver가 백그라운드 Task로 판단한다. WebGL은 메인 스레드에서 처리하므로 깊이/후보 수 확대 시 프레임 지연을 확인해야 한다.

## 서버

기준 경로는 `../MiniChess_server/src/MiniChess.Server/`이다.

- `Program.cs`: WebSocket 활성화, `/` 확인 응답, `/ws` 연결 진입점, 싱글턴 Lobby/MatchOptions.
- `Matches/Lobby.cs`: 첫 JoinMatch 검증, 도착 순서대로 2명씩 매칭, 연결 종료 정리. 방 코드/계정 기반 매칭은 이 경로에 없다.
- `Matches/MatchRoom.cs`: 실제 GameController, 연결별 팀/요청 번호, SemaphoreSlim 직렬 처리, 배치/턴 타이머, 팀별 전송, 경기 종료.
- `Connections/PlayerConnection.cs`: 수신 제한, 메시지 크기 검사, 송신 Channel, 느린 수신자 처리, 종료 및 Flush.
- `Matches/MatchOptions.cs`: 타임아웃과 경기 생성·설정 지문. 테스트에서 주입해 바꿀 수 있다.
- 재접속은 없다. 전투 중 끊기면 상대 승리, 배치 중 끊기면 승자 없이 종료한다. 코어 처리 예외는 무효 경기로 종료한다.
- `StartServer.bat`: 모든 인터페이스의 HTTP 5055 포트로 실행한다. 기본 클라이언트 주소는 `ws://localhost:5055/ws`다.

## 후속 작업별 수정 범위

| 요청 | 우선 확인할 위치 | 같이 확인할 영향 |
|---|---|---|
| 밸런스 | PrototypeTestPreset, SkillTuning, ASSUMPTIONS | 양쪽 코어, ConfigHash, 봇 점수 |
| 새 캐릭터/스킬 | Pieces, PieceModules, Skills | 튜닝, 테스트, 시각/설명 카탈로그, 봇 후보 |
| 이동/AP/공격 | Actions, Movement, Controller 조회 | 화면 미리보기, 실패 문구, 봇 평가 |
| 상태/장판/포격 | Statuses, Effects, Turns | 피해·지속 순서, Snapshot/Fork, 숨김, 이펙트 |
| UI/조작 | SelectionController, PlacementController, UI | GameSession 명령 경로와 조회 API |
| 연출 | GameSession.Present/PlayEvents, Visuals/Effects | 타격 시점, 풀 반환, Rebind 순서 |
| 맵 | 프리셋 MapData, MapSceneSettings, RuinsMap | 논리/시각 일치, Build Settings |
| 온라인 기능 | Protocol, Lobby, MatchRoom, ServerConnection | 버전, 설정 지문, 팀/턴/중복 처리, 통합 테스트 |
| AI | BotActionGenerator, Rules, Eval, Search | Fork·숨김, 성능, WebGL, 헤드리스 테스트 |

## 검증

피로도 반영 후 테스트 실행 결과: **Core 403/403 통과, Bot 28/28 통과, Server 15/15 통과**, 실패·건너뜀 없음. 피로도 테스트는 8 라운드 경계, 양 팀 종료 시 피해와 라운드별 증가, 비활성/수치 변경, 보호막/호위/봉인, 단독/동시 전멸, 소환물 연쇄 사망, Fork, 네트워크 이벤트/스냅샷 왕복을 다룬다. Unity 에디터/플레이・WebGL 실행은 수행하지 않았다.

기본 병렬 빌드로 시작한 두 테스트 명령은 출력 없이 대기해, 빌드 서버를 사용하지 않고 MSBuild 노드 수를 1로 제한한 아래 명령으로 재실행하여 결과를 확인했다.

```powershell
dotnet test Assets/Scripts/Core/tests/MiniChess.Core.Tests/MiniChess.Core.Tests.csproj --no-restore --disable-build-servers --verbosity minimal -m:1 -nr:false
dotnet test Tests/MiniChess.Bot.Tests/MiniChess.Bot.Tests.csproj --no-restore --disable-build-servers --verbosity minimal -m:1 -nr:false
```

Core의 Directory.Build.props가 산출물을 `.artifacts`로 모아 Assets 아래의 DLL 임포트 충돌을 피한다. 봇의 헤드리스 프로젝트는 `Tests/MiniChess.Bot/MiniChess.Bot.csproj`가 실제 Bot 소스를 연결해 컴파일한다. 서버 테스트 프로젝트는 `../MiniChess_server/tests/MiniChess.Server.Tests/MiniChess.Server.Tests.csproj`이며 매칭·배치·턴·숨김·잘못된 버전/설정/입력·Ping·타임아웃·접속 종료·코어 예외를 다룬다.

코어와 프로토콜 버전은 이번 서버 동기화로 일치시켰다. 기존 Unity 미커밋 변경은 별도로 남아 있으므로, 다음 기능 변경 전에 작업 대상 코어와 두 상위 저장소의 상태를 다시 확인한다.
