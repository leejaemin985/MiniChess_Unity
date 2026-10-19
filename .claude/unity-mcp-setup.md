# Unity-MCP (AI Game Developer) 셋업

Claude Code가 Unity Editor를 조작하기 위한 연결 설정. 이펙트(ParticleSystem) 작업에 사용.

- 패키지: `com.ivanmurzak.unity.mcp` + `com.ivanmurzak.unity.mcp.particlesystem` (OpenUPM, `Packages/manifest.json`)
- `.claude/skills/` 는 플러그인이 자동 생성 (Window > AI Game Developer > Skills > Generate)

## 새 PC에서 할 일

연결 모드/도구 활성화 상태는 `UserSettings/AI-Game-Developer-Config.json`(git 제외, 토큰 포함)에 저장되므로 PC마다 다시 설정해야 한다.

1. Unity로 프로젝트 열기 → 패키지/NuGet DLL 자동 복원 확인
2. `Window > AI Game Developer`
   - Connection: **Custom** 선택 (Cloud는 ai-game.dev 로그인 필요)
   - `Unity: Connected`, `MCP server: Running (http)` 초록불 확인
   - Claude Code 카드 > MCP > **Reconfigure** (`.mcp.json` 갱신)
3. 도구 활성화 (`7 / 18 tools` > Open): 아래 도구가 꺼져 있으면 켜기
   - `script-execute`, `screenshot-camera`, `screenshot-scene-view`, `screenshot-game-view`
4. 확인: `npx unity-mcp-cli run-system-tool ping --path .` → 입력 그대로 echo 되면 OK

## 주의

- `rql-mutate` 는 실행 후 **열린 씬을 디스크에 자동 저장**한다. 미리보기용 임시 오브젝트는 Battle 씬이 아닌 별도 씬/프리팹 모드에서 작업할 것.
- ParticleSystem Shape 모듈의 `rotation` 값이 방출 방향에 반영되지 않는 현상이 있었음 → 방향은 자식 Transform 회전으로 처리.
- 이펙트 위치: `Assets/Art/Effects/{EffectName}/`, 머티리얼은 Built-in RP라 `Legacy Shaders/Particles/Additive` 사용.
