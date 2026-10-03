using UnityEngine;

namespace MiniChess
{
    // Small prototype HUD/input layer; rules remain in BattleSystem.
    public sealed class PrototypeUI : MonoBehaviour
    {
        private GameController game;
        private UnitState selected;
        private bool skillMode;
        private bool showDetails;
        private Vector2 detailsScroll;
        private int deploymentIndex = -1;
        // Deployment-only: clicks lay or lift obstacles instead of placing units.
        private bool obstacleMode;
        // Deployment-only: clicks designate the one capture tile.
        private bool captureMode;
        // First ally picked for a two-target skill, or -1 while none is chosen.
        private int pendingX = -1;
        private int pendingY = -1;
        private Font hangulFont;
        private string message = "고정 배치 준비 완료. 전투 시작을 누르세요.";
        private float Scale => Mathf.Min(Mathf.Clamp(Screen.width / 900f, 0.8f, 1.5f), Screen.height / 400f);
        private float PanelHeight => Mathf.Min(250f * Scale, Screen.height * 0.55f);

        // Unity's built-in GUI font has no Hangul, so pull one from the OS or the
        // whole HUD renders as blank boxes. Null is fine: IMGUI keeps its default.
        private Font HangulFont
        {
            get
            {
                if (hangulFont == null)
                    hangulFont = Font.CreateDynamicFontFromOSFont(new[]
                    {
                        "Malgun Gothic", "맑은 고딕", "NanumGothic", "나눔고딕",
                        "Noto Sans KR", "Gulim", "굴림", "Dotum", "Arial Unicode MS"
                    }, 16);
                return hangulFont;
            }
        }

        public void Initialize(GameController controller)
        {
            if (game != null) game.StateChanged -= Refresh;
            game = controller;
            game.StateChanged += Refresh;
            Refresh();
            FitCamera();
        }

        private void LateUpdate()
        {
            if (game != null) FitCamera();
        }

        private void FitCamera()
        {
            Camera camera = game.BattleCamera;
            Board board = game.Board;
            float panelFraction = PanelHeight / Mathf.Max(1, Screen.height);
            camera.rect = new Rect(0, panelFraction, 1, 1 - panelFraction);
            camera.orthographic = true;
            float halfSize = board.BoardSize * board.CellSize * 0.5f;
            float aspect = Screen.width / Mathf.Max(1f, Screen.height - PanelHeight);
            Vector3 scale = board.transform.lossyScale;
            camera.orthographicSize = Mathf.Max(halfSize * Mathf.Abs(scale.z),
                halfSize * Mathf.Abs(scale.x) / aspect) + board.CellSize;
            float center = (board.BoardSize - 1) * board.CellSize * 0.5f;
            camera.transform.position = board.transform.TransformPoint(new Vector3(center, 0, center))
                + board.transform.up * Mathf.Max(10f, board.BoardSize * board.CellSize * 2f);
            camera.transform.rotation = board.transform.rotation * Quaternion.Euler(90, 0, 0);
        }

        private void Update()
        {
            if (game == null || showDetails || game.IsBotTurn || game.State.Phase == GamePhase.Finished
                || (game.State.Phase == GamePhase.Deployment && !game.Deployment.Started)) return;
            Vector2 pointer;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase != TouchPhase.Began) return;
                pointer = touch.position;
            }
            else
            {
                if (!Input.GetMouseButtonDown(0)) return;
                pointer = Input.mousePosition;
            }
            if (pointer.y <= PanelHeight) return;
            Ray ray = game.BattleCamera.ScreenPointToRay(pointer);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;
            Unit view = hit.collider.GetComponentInParent<Unit>();
            BoardCell cell = hit.collider.GetComponent<BoardCell>();
            if (view != null && view.State != null
                && game.State.GetUnit(view.State.Owner, view.State.UnitIndex) == view.State)
                ClickCell(view.State.X, view.State.Y);
            else if (cell != null && cell.transform.parent == game.Board.transform)
                ClickCell(cell.Position.x, cell.Position.y);
        }

        private void ClickCell(int x, int y)
        {
            if (game.IsBotTurn) return;
            if (game.State.Phase == GamePhase.Deployment)
            {
                if (captureMode)
                    message = game.State.IsCaptureTile(x, y)
                        ? (game.State.ClearCapturePoint() ? "점령지를 해제했습니다." : message)
                        : game.State.TrySetCapturePoint(x, y)
                            ? "점령지를 지정했습니다. 한 군데만 둘 수 있어 다시 고르면 옮겨집니다."
                            : "장애물이 있는 칸은 점령지로 쓸 수 없습니다.";
                else if (obstacleMode)
                    message = game.State.RemoveObstacle(x, y) ? "장애물을 치웠습니다."
                        : game.State.TryAddObstacle(x, y) ? "장애물을 놓았습니다. 다시 누르면 치웁니다."
                        : game.State.IsCaptureTile(x, y) ? "점령지에는 장애물을 놓을 수 없습니다."
                        : "유닛이 있는 칸에는 장애물을 놓을 수 없습니다.";
                else
                    message = deploymentIndex < 0 ? "먼저 아래 목록에서 유닛을 선택하세요."
                        : game.TryPlaceUnit(deploymentIndex, x, y) ? "배치했습니다. 다른 유닛을 고르거나 위치를 다시 잡으세요."
                        : "내 진영 아래 두 줄의 빈 칸에 배치하세요.";
                Refresh();
                return;
            }
            PlayerId player = game.State.CurrentPlayer.Value;
            UnitState target = game.State.GetUnitAt(x, y);
            if (skillMode && selected != null)
            {
                ClickSkillTarget(player, x, y);
            }
            else if (target != null && target.Owner == player)
            {
                selected = target;
                message = "초록: 이동 가능. 빨강: 공격 가능. 노랑: 선택된 유닛.";
            }
            else if (selected == null)
                message = "먼저 현재 차례인 쪽의 유닛을 선택하세요.";
            else if (target == null)
                message = game.Battle.TryMove(player, selected.UnitIndex, x, y)
                    ? (selected.MovementStoppedThisTurn
                        ? "덫에 걸렸습니다! 이번 턴에는 이 자리에서 움직일 수 없습니다."
                        : "이동했습니다.")
                    : selected.MovementStoppedThisTurn
                        ? "덫에 걸려 이번 턴에는 이동할 수 없습니다."
                        : "그곳으로 이동할 수 없습니다. 거리, 점유 상태, 남은 AP를 확인하세요.";
            else
                message = game.Battle.TryAttack(player, selected.UnitIndex, target.Owner, target.UnitIndex)
                    ? "공격했습니다." : "공격할 수 없습니다. 사거리와 이번 턴 전투 사용 여부를 확인하세요.";
            Refresh();
        }

        // Text markers on top of the tiles, so fire and traps stay readable even when
        // a move or attack highlight has taken over the tile colour.
        private void DrawBoardEffects(float scale, GUIStyle style)
        {
            GameState state = game.State;
            if (state.Areas.Count == 0 && state.Traps.Count == 0 && state.Obstacles.Count == 0
                && !state.HasCapturePoint) return;
            Color oldColor = GUI.color;
            if (state.HasCapturePoint)
                DrawTileMarker(state.CaptureX, state.CaptureY, "점", new Color(0.55f, 1f, 0.8f), scale, style);
            foreach (int tile in state.Obstacles)
                DrawTileMarker(tile % state.BoardSize, tile / state.BoardSize, "벽",
                    new Color(0.75f, 0.78f, 0.85f), scale, style);
            foreach (AreaEffect area in state.Areas)
                for (int i = 0; i < area.TileCount; i++)
                    DrawTileMarker(area.GetTileX(i), area.GetTileY(i), "화", new Color(1f, 0.55f, 0.1f), scale, style);
            // The enemy's traps are drawn by nothing: finding them is the point.
            foreach (TrapEffect trap in state.Traps)
                if (trap.Owner == PlayerId.PlayerOne)
                    DrawTileMarker(trap.X, trap.Y, "덫", new Color(0.78f, 0.45f, 1f), scale, style);
            GUI.color = oldColor;
        }

        private void DrawTileMarker(int x, int y, string text, Color tint, float scale, GUIStyle style)
        {
            BoardCell cell = game.Board.GetCell(x, y);
            if (cell == null) return;
            Vector3 point = game.BattleCamera.WorldToScreenPoint(cell.transform.position);
            if (point.z <= 0) return;
            GUI.color = tint;
            style.fontSize = Mathf.Max(8, Mathf.FloorToInt(11f));
            // Offset to a corner so a unit label on the same tile stays readable.
            GUI.Label(new Rect(point.x / scale - 20f, (Screen.height - point.y) / scale - 22f, 24f, 18f),
                text, style);
        }

        // Prototype markers: orange for a fire zone, purple for an armed trap.
        // A small card in the top-right naming the buffs a capture is currently handing
        // out, and to whom. Without it the buffs are invisible: they show up only as a
        // shield number or a sudden 2 damage, with nothing saying why.
        private void DrawCapturePanel(float width, float top, GameState state)
        {
            if (!state.HasCapturePoint || state.Phase == GamePhase.Deployment) return;
            var lines = new System.Collections.Generic.List<string>();
            foreach (ICapturePointEffect effect in CaptureBuffs(state.Rules.Capture.Effect))
                lines.Add("· " + effect.Describe(state.Rules));
            string heading = state.CaptureOwner.HasValue
                ? "점령: " + PlayerName(state.CaptureOwner.Value)
                : state.CaptureHolder.HasValue
                    ? "점령 중: " + PlayerName(state.CaptureHolder.Value) + " "
                        + state.CaptureHeldRounds + "/" + state.Rules.Capture.RoundsToCapture
                    : "점령지 (" + state.CaptureX + "," + state.CaptureY + ") 비어 있음";
            float panelWidth = 196f;
            float panelHeight = 30f + lines.Count * 17f;
            var area = new Rect(width - panelWidth - 10f, 10f, panelWidth, panelHeight);
            Color oldColor = GUI.color;
            // Unclaimed reads as a neutral note; owned takes the owner's colour so it is
            // obvious at a glance whether these buffs are helping you or the bot.
            GUI.color = !state.CaptureOwner.HasValue ? new Color(0.75f, 0.78f, 0.8f, 0.85f)
                : state.CaptureOwner == PlayerId.PlayerOne ? new Color(0.55f, 0.75f, 1f, 0.92f)
                : new Color(1f, 0.6f, 0.55f, 0.92f);
            GUI.Box(area, GUIContent.none);
            GUI.color = oldColor;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            if (state.CaptureOwner == PlayerId.PlayerOne) style.normal.textColor = new Color(0.2f, 0.45f, 0.95f);
            else if (state.CaptureOwner == PlayerId.PlayerTwo) style.normal.textColor = new Color(0.85f, 0.2f, 0.2f);
            GUI.Label(new Rect(area.x + 8f, area.y + 5f, panelWidth - 16f, 16f), heading, style);
            var buffStyle = new GUIStyle(style) { fontSize = 11 };
            for (int i = 0; i < lines.Count; i++)
                GUI.Label(new Rect(area.x + 8f, area.y + 23f + i * 17f, panelWidth - 16f, 16f),
                    lines[i], buffStyle);
            // Nothing owns it yet, so say what is on offer rather than listing nothing.
            if (!state.CaptureOwner.HasValue && lines.Count == 0)
                GUI.Label(new Rect(area.x + 8f, area.y + 23f, panelWidth - 16f, 16f),
                    "· " + state.Rules.Capture.Effect.Describe(state.Rules), buffStyle);
        }

        // One entry per buff, so each gets its own line. A composite exposes its parts.
        private static System.Collections.Generic.IEnumerable<ICapturePointEffect> CaptureBuffs(
            ICapturePointEffect effect)
        {
            if (effect is CompositeCapturePointEffect composite) return composite.Effects;
            return new[] { effect };
        }

        // Who owns the point, or how far along whoever is standing there has got.
        private static string DescribeCapture(GameState state)
        {
            if (!state.HasCapturePoint) return "";
            if (state.CaptureOwner.HasValue)
                return " | 점령: " + PlayerName(state.CaptureOwner.Value)
                    + " (" + state.Rules.Capture.Effect.Describe(state.Rules) + ")";
            if (!state.CaptureHolder.HasValue) return " | 점령지 비어 있음";
            return " | 점령 " + PlayerName(state.CaptureHolder.Value) + " "
                + state.CaptureHeldRounds + "/" + state.Rules.Capture.RoundsToCapture + "라운드";
        }

        private Color? EffectColor(int x, int y)
        {
            if (game.State.HasObstacleAt(x, y)) return new Color(0.28f, 0.28f, 0.32f);
            // Owned shows in that side's colour; unclaimed stays neutral teal.
            if (game.State.IsCaptureTile(x, y))
                return game.State.CaptureOwner == PlayerId.PlayerOne ? new Color(0.25f, 0.55f, 0.95f)
                    : game.State.CaptureOwner == PlayerId.PlayerTwo ? new Color(0.95f, 0.35f, 0.35f)
                    : new Color(0.20f, 0.62f, 0.55f);
            // Only your own traps are yours to see; the enemy's are hidden information.
            TrapEffect trap = game.State.GetTrapAt(x, y);
            if (trap != null && trap.Owner == PlayerId.PlayerOne) return new Color(0.62f, 0.24f, 0.86f);
            if (game.State.HasAreaAt(x, y)) return new Color(0.95f, 0.42f, 0.10f);
            return null;
        }

        // Tile skills resolve on one click; Swap collects two allies before firing.
        private void ClickSkillTarget(PlayerId player, int x, int y)
        {
            if (selected.Definition.Targeting == SkillTargeting.TwoAllies)
            {
                if (pendingX < 0)
                {
                    if (!game.Battle.CanSkillFirstTarget(player, selected.UnitIndex, x, y))
                    { message = "먼저 사거리 안의 아군을 고르세요."; return; }
                    pendingX = x; pendingY = y;
                    message = "위치를 맞바꿀 두 번째 아군을 고르세요.";
                    return;
                }
                bool swapped = game.Battle.TrySkillAt(player, selected.UnitIndex, pendingX, pendingY, x, y);
                message = swapped ? selected.Definition.SkillName + " 사용!"
                    : "두 번째 대상이 올바르지 않습니다. 하늘색 칸을 고르세요.";
                pendingX = pendingY = -1;
                if (swapped) skillMode = false;
                return;
            }
            bool used = game.Battle.TrySkillAt(player, selected.UnitIndex, x, y);
            message = used ? selected.Definition.SkillName + " 사용!"
                : "스킬 대상이 올바르지 않습니다. 하늘색으로 표시된 칸을 고르세요.";
            if (used) skillMode = false;
        }

        private void Refresh()
        {
            if (game.State.Phase != GamePhase.Deployment || !game.Deployment.Started)
                deploymentIndex = -1;
            if (selected != null && (game.IsBotTurn || game.State.Phase != GamePhase.Battle
                || game.State.CurrentPlayer != selected.Owner || !selected.IsAlive
                || game.State.GetUnit(selected.Owner, selected.UnitIndex) != selected)) selected = null;
            if (selected == null || !game.Battle.CanUseSkill(selected.Owner, selected.UnitIndex)) skillMode = false;
            if (!skillMode) pendingX = pendingY = -1;
            if (selected == null) showDetails = false;
            for (int y = 0; y < game.Board.BoardSize; y++)
                for (int x = 0; x < game.Board.BoardSize; x++)
                {
                    Color? color = null;
                    if (game.State.Phase == GamePhase.Deployment && game.Deployment.Started)
                    {
                        if (captureMode)
                        {
                            if (!game.State.HasObstacleAt(x, y)) color = new Color(0.45f, 0.75f, 0.55f);
                        }
                        else if (obstacleMode)
                        {
                            // Any free tile on the whole board can take a wall.
                            if (!game.State.IsBlocked(x, y)) color = new Color(0.5f, 0.54f, 0.62f);
                        }
                        else
                        {
                            if (game.Deployment.CanPlace(deploymentIndex, x, y)) color = Color.green;
                            UnitState placed = game.State.GetUnit(PlayerId.PlayerOne, deploymentIndex);
                            if (placed != null && placed.X == x && placed.Y == y) color = Color.yellow;
                        }
                    }
                    if (selected != null)
                    {
                        UnitState target = game.State.GetUnitAt(x, y);
                        if (skillMode)
                        {
                            bool legal = pendingX < 0
                                ? game.Battle.CanSkillFirstTarget(selected.Owner, selected.UnitIndex, x, y)
                                : game.Battle.CanSkillAt(selected.Owner, selected.UnitIndex,
                                    pendingX, pendingY, x, y);
                            if (legal) color = Color.cyan;
                            if (x == pendingX && y == pendingY) color = Color.magenta;
                        }
                        else
                        {
                            if (game.Battle.CanMove(selected.Owner, selected.UnitIndex, x, y)) color = Color.green;
                            if (target != null && game.Battle.CanAttack(selected.Owner, selected.UnitIndex,
                                target.Owner, target.UnitIndex)) color = new Color(1f, 0.3f, 0.3f);
                        }
                        if (x == selected.X && y == selected.Y) color = Color.yellow;
                    }
                    // Board effects tint any tile an action highlight has not claimed.
                    if (color == null) color = EffectColor(x, y);
                    game.Board.GetCell(x, y)?.SetHighlight(color);
                }
        }

        private void OnGUI()
        {
            if (game == null || game.State == null) return;
            float scale = Scale;
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale;
            float top = (Screen.height - PanelHeight) / scale;
            Font font = HangulFont;
            if (font != null) GUI.skin.font = font;
            var label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            var unitLabel = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(1, 1, 0, 0),
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            DrawBoardEffects(scale, unitLabel);
            foreach (Unit view in game.Views)
            {
                if (view == null || !view.State.IsAlive) continue;
                Vector3 point = game.BattleCamera.WorldToScreenPoint(view.transform.position);
                if (point.z <= 0) continue;
                // Size labels from projected cell spacing so neighboring units cannot overlap.
                Vector3 right = game.BattleCamera.WorldToScreenPoint(view.transform.position
                    + game.Board.transform.TransformVector(Vector3.right * game.Board.CellSize));
                Vector3 forward = game.BattleCamera.WorldToScreenPoint(view.transform.position
                    + game.Board.transform.TransformVector(Vector3.forward * game.Board.CellSize));
                float labelWidth = Mathf.Min(54f, Mathf.Abs(right.x - point.x) / scale * 0.9f);
                float labelHeight = Mathf.Min(28f, Mathf.Abs(forward.y - point.y) / scale * 0.8f);
                unitLabel.fontSize = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(11f, labelHeight / 2.4f)));
                string title = UnitName(view.State).Substring(0, 1)
                    + (view.State.Shield > 0 ? "+" : "");
                GUI.Box(new Rect(point.x / scale - labelWidth * 0.5f,
                    (Screen.height - point.y) / scale - labelHeight * 0.5f, labelWidth, labelHeight),
                    title + "\n" + view.State.HP + "/" + view.State.MaxHP, unitLabel);
            }
            DrawCapturePanel(width, top, state: game.State);
            GUI.Box(new Rect(0, top, width, PanelHeight / scale), GUIContent.none);
            GameState state = game.State;
            if (state.Phase == GamePhase.Deployment)
            {
                DrawDeploymentPanel(width, top, label);
                GUI.matrix = oldMatrix;
                return;
            }
            string status = state.Phase == GamePhase.Finished ? PlayerName(state.Winner.Value) + " 승리!"
                : "턴 " + state.TurnNumber + " | " + PlayerName(state.CurrentPlayer.Value)
                    + " | AP: " + state.GetAP(state.CurrentPlayer.Value) + "/" + state.Rules.AP.MaxAP
                    + (state.Rules.AP.UseSkillResource ? " | 스킬 자원(SP): " + state.GetCost(state.CurrentPlayer.Value) : "")
                    + DescribeCapture(state);
            GUI.Label(new Rect(12, top + 6, width - 24, 25), status, label);
            string selection = selected == null ? "아군 유닛을 고른 뒤, 이동할 칸이나 적을 클릭하세요."
                : UnitName(selected) + " | 체력 " + selected.HP + "/" + selected.MaxHP
                    + " | 공격력 " + selected.AttackDamage + " | 사거리 "
                    + game.Battle.GetAttackRange(selected.Owner, selected.UnitIndex)
                    + (selected.Definition.AimRangeBonus > 0
                        && game.Battle.GetAttackRange(selected.Owner, selected.UnitIndex) > selected.AttackRange ? " (조준)" : "")
                    + " | 보호막 " + selected.Shield
                    + (selected.HasCombatActedThisTurn ? " | 이번 턴 전투 완료" : " | 공격 " + state.Rules.AP.AttackAP + " AP")
                    + (selected.MovementStoppedThisTurn ? " | 덫에 걸림(이동 불가)" : "")
                    + (selected.AttackBurnDamage > 0
                        ? " | 화상 " + selected.AttackBurnDamage + "(다음 턴 시작)" : "");
            GUI.Label(new Rect(12, top + 32, width - 24, 25), selection, label);
            // Skill text sits above the button so it can be read before committing.
            DrawSkillInfo(width, top + 57, state);
            GUI.Label(new Rect(12, top + 117, width - 24, 32),
                state.Phase == GamePhase.Finished ? "전투가 끝났습니다. 재시작을 누르면 배치 화면으로 돌아갑니다."
                : game.IsBotTurn ? (game.IsBotThinking ? "봇이 생각 중입니다..." : "봇이 행동 중입니다...")
                : state.GetAP(state.CurrentPlayer.Value) == 0 ? "남은 AP가 없습니다. 턴 종료를 누르세요." : message, label);
            if (state.Phase == GamePhase.Battle)
            {
                bool wasEnabled = GUI.enabled;
                GUI.enabled = !game.IsBotTurn;
                if (GUI.Button(new Rect(12, top + 153, 130, 35), "턴 종료"))
                { game.EndTurn(); message = "턴이 넘어갔습니다. 아군 유닛을 선택하세요."; }
                GUI.enabled = wasEnabled;
            }
            if (GUI.Button(new Rect(154, top + 153, 130, 35), "재시작"))
            { game.RestartMatch(); obstacleMode = captureMode = false; message = "고정 배치 준비 완료. 전투 시작을 누르세요."; }
            DrawDifficultyButton(296, top + 153);
            if (selected != null)
            {
                bool oldEnabled = GUI.enabled;
                GUI.enabled = game.Battle.CanUseSkill(selected.Owner, selected.UnitIndex);
                string skillText = skillMode ? "스킬 취소" : selected.Definition.SkillName + " ("
                    + selected.Definition.GetSkillCostText(state.Rules) + ")";
                // Separate row also fits narrow game views.
                if (GUI.Button(new Rect(12, top + 192, 190, 28), skillText))
                {
                    skillMode = !skillMode;
                    message = skillMode ? "하늘색 대상을 클릭하면 " + selected.Definition.SkillName + " 사용"
                        : "이동할 칸이나 공격할 적을 선택하세요.";
                    Refresh();
                }
                GUI.enabled = oldEnabled;
                if (GUI.Button(new Rect(210, top + 192, 74, 28), "상세"))
                { showDetails = !showDetails; detailsScroll = Vector2.zero; }
            }
            if (showDetails && selected != null) DrawUnitDetails(width, top);
            GUI.matrix = oldMatrix;
        }

        // Describes the selected unit's skill up front, and says what is blocking it
        // rather than leaving a greyed-out button with no explanation.
        // Skill text runs long, so it gets its own compact style and a taller box.
        private void DrawSkillInfo(float width, float y, GameState state)
        {
            if (selected == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            UnitDefinition definition = selected.Definition;
            string passive = definition.GetPassiveText();
            string text = passive.Length > 0 ? "[특성] " + passive + "\n" : "";
            if (definition.Skill == UnitSkill.None)
            {
                GUI.Label(new Rect(12, y, width - 24, 56), text + "이 유닛은 스킬이 없습니다.", style);
                return;
            }
            text += "[스킬] " + definition.GetSkillSummary(state.Rules);
            string blocked = DescribeSkillBlock(state);
            if (blocked != null) text += "\n→ 지금은 사용할 수 없습니다: " + blocked;
            else if (skillMode) text += "\n→ 하늘색 칸의 대상을 클릭하세요.";
            GUI.Label(new Rect(12, y, width - 24, 56), text, style);
        }

        private string DescribeSkillBlock(GameState state)
        {
            if (game.Battle.CanUseSkill(selected.Owner, selected.UnitIndex)) return null;
            if (selected.HasCombatActedThisTurn) return "이 유닛은 이번 턴에 이미 전투했습니다.";
            if (selected.MovementStoppedThisTurn && selected.Definition.IsMovementSkill)
                return "덫에 걸려 이번 턴에는 이동 스킬을 쓸 수 없습니다.";
            int apCost = game.Battle.GetSkillAPCost(selected.Owner, selected.UnitIndex);
            if (state.GetAP(selected.Owner) < apCost)
                return "AP가 " + apCost + " 필요한데 " + state.GetAP(selected.Owner) + "만 남았습니다.";
            int spCost = game.Battle.GetSkillCost(selected.Owner, selected.UnitIndex);
            if (state.Rules.AP.UseSkillResource && state.GetCost(selected.Owner) < spCost)
                return "스킬 자원(SP)이 " + spCost + " 필요한데 " + state.GetCost(selected.Owner)
                    + "만 남았습니다. SP는 경기 중 회복되지 않습니다.";
            return "조건을 만족하지 않습니다.";
        }

        private void DrawUnitDetails(float width, float panelTop)
        {
            float boxWidth = Mathf.Min(440, width - 24);
            float boxHeight = Mathf.Min(260, panelTop - 20);
            var box = new Rect(12, 10, boxWidth, boxHeight);
            GUI.Box(box, selected.Definition.Name);
            var textStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            string text = selected.Definition.GetDescription(game.State.Rules);
            float textWidth = boxWidth - 38;
            float textHeight = textStyle.CalcHeight(new GUIContent(text), textWidth);
            detailsScroll = GUI.BeginScrollView(new Rect(22, 35, boxWidth - 20, boxHeight - 68),
                detailsScroll, new Rect(0, 0, textWidth, textHeight));
            GUI.Label(new Rect(0, 0, textWidth, textHeight), text, textStyle);
            GUI.EndScrollView();
            if (GUI.Button(new Rect(22, box.yMax - 34, boxWidth - 20, 25), "닫기")) showDetails = false;
        }

        private static string PlayerName(PlayerId player) => player == PlayerId.PlayerOne ? "청군" : "홍군";
        private void DrawDeploymentPanel(float width, float top, GUIStyle label)
        {
            if (!game.Deployment.Started)
            {
                GUI.Label(new Rect(12, top + 8, width - 24, 45),
                    "당신은 1P(청군)입니다. 게임 시작을 눌러 유닛을 배치하세요.", label);
                if (GUI.Button(new Rect(12, top + 108, 130, 35), "게임 시작"))
                {
                    message = "아래에서 유닛을 고른 뒤, 내 진영 아래 두 줄의 칸을 클릭하세요.";
                    game.BeginDeployment();
                }
                DrawDifficultyButton(154, top + 108);
                return;
            }
            int placed = 0;
            for (int i = 0; i < GameState.UnitsPerPlayer; i++)
                if (game.State.GetUnit(PlayerId.PlayerOne, i) != null) placed++;
            string captureNote = game.State.HasCapturePoint
                ? "점령지 (" + game.State.CaptureX + "," + game.State.CaptureY + ")" : "점령지 없음";
            GUI.Label(new Rect(12, top + 5, width - 24, 25), captureMode
                ? captureNote + " | " + game.State.Rules.Capture.GetSummary()
                : obstacleMode
                    ? "장애물 " + game.State.Obstacles.Count + "개 | 아무 빈 칸이나 눌러 놓고, 다시 눌러 치웁니다"
                    : "1P 배치: " + placed + "/4 | 장애물 " + game.State.Obstacles.Count + "개 | " + captureNote, label);
            GUI.Label(new Rect(12, top + 30, width - 24, 28), message, label);
            float cardWidth = (width - 32) / 2;
            for (int i = 0; i < GameState.UnitsPerPlayer; i++)
            {
                Color oldColor = GUI.backgroundColor;
                if (deploymentIndex == i) GUI.backgroundColor = Color.yellow;
                UnitDefinition drafted = game.Deployment.GetDefinition(i);
                string name = UnitDefinition.GetPositionName(drafted.Position) + " " + drafted.Name
                    + (game.State.GetUnit(PlayerId.PlayerOne, i) != null ? " - 배치됨" : "");
                if (GUI.Button(new Rect(12 + (i % 2) * (cardWidth + 8), top + 62 + (i / 2) * 33, cardWidth, 29), name))
                {
                    deploymentIndex = i;
                    message = game.Deployment.GetDefinition(i).Name + "을(를) 놓을 초록색 칸을 고르세요.";
                    Refresh();
                }
                GUI.backgroundColor = oldColor;
            }
            bool wasEnabled = GUI.enabled;
            GUI.enabled = game.State.IsDeploymentReady();
            if (GUI.Button(new Rect(12, top + 137, 130, 35), "전투 시작"))
            { game.StartBattle(); obstacleMode = captureMode = false; message = "배치 완료. 아군 유닛을 선택하세요."; }
            GUI.enabled = wasEnabled;
            if (GUI.Button(new Rect(154, top + 137, 130, 35), "편성 다시"))
            {
                game.RedraftPlayerRoster();
                message = "새로 편성했습니다. 유닛을 고른 뒤 초록색 칸에 배치하세요.";
            }
            DrawDifficultyButton(296, top + 137);
            Color oldMode = GUI.backgroundColor;
            if (obstacleMode) GUI.backgroundColor = new Color(0.6f, 0.62f, 0.7f);
            if (GUI.Button(new Rect(12, top + 176, 130, 35), obstacleMode ? "장애물 끝내기" : "장애물 놓기"))
            {
                obstacleMode = !obstacleMode;
                captureMode = false;
                message = obstacleMode ? "빈 칸을 눌러 장애물을 놓으세요. 같은 칸을 다시 누르면 치웁니다."
                    : "유닛을 고른 뒤 초록색 칸에 배치하세요.";
                Refresh();
            }
            GUI.backgroundColor = oldMode;
            GUI.enabled = game.State.Obstacles.Count > 0;
            if (GUI.Button(new Rect(154, top + 176, 130, 35), "장애물 비우기"))
            {
                game.State.ClearObstacles();
                message = "장애물을 모두 치웠습니다.";
                Refresh();
            }
            GUI.enabled = wasEnabled;
            if (captureMode) GUI.backgroundColor = new Color(0.45f, 0.78f, 0.6f);
            if (GUI.Button(new Rect(296, top + 176, 150, 35), captureMode ? "점령지 끝내기" : "점령지 지정"))
            {
                captureMode = !captureMode;
                obstacleMode = false;
                message = captureMode ? "점령지로 쓸 칸 하나를 누르세요. 같은 칸을 다시 누르면 해제합니다."
                    : "유닛을 고른 뒤 초록색 칸에 배치하세요.";
                Refresh();
            }
            GUI.backgroundColor = oldMode;
        }

        // Switching applies to the bot's next search, so it stays available mid-match.
        private void DrawDifficultyButton(float x, float y)
        {
            if (!game.HasDifficultyChoice) return;
            if (!GUI.Button(new Rect(x, y, 150, 35), "봇 난이도: " + game.DifficultyName)) return;
            game.CycleDifficulty();
            message = "봇 난이도를 " + game.DifficultyName + "(으)로 바꿨습니다.";
        }
        private static string UnitName(UnitState unit)
        {
            return unit.Definition.Name;
        }

        private void OnDestroy()
        {
            if (game != null) game.StateChanged -= Refresh;
        }
    }
}

