using System.Collections.Generic;
using System.Linq;
using Godot;
using TacticalGame.AI;
using TacticalGame.AI.Debug;
using TacticalGame.Grid;
using TacticalGame.Messages;

namespace TacticalGame.Prototype
{
    public partial class PrototypeScene : Node2D
    {
        private BattleManager _manager = null!;
        private GridVisualizer _visualizer = null!;
        private BattleOrchestrator _orchestrator = null!;
        private Label _logLabel = null!;
        private Label _statusLabel = null!;
        private string _logText = "";
        private bool _autoPlay;
        private bool _playing;

        private DecisionLog _decisionLog = new();
        private readonly DecisionHistory _history = new();
        private AIDebugOverlay _overlay = null!;

        private MessageEngine? _narrator;
        private MessageBubbleLayer _bubbles = null!;

        public override void _Ready()
        {
            _manager = CreateBattleManager();

            _visualizer = new GridVisualizer();
            AddChild(_visualizer);
            _visualizer.SetState(_manager);

            _orchestrator = new BattleOrchestrator(
                new Dictionary<Unit, UnitVisual>(_visualizer.UnitVisuals),
                _visualizer.HexToPixel);

            _logLabel = new Label();
            _logLabel.Position = new Vector2(10, 450);
            _logLabel.Size = new Vector2(980, 300);
            _logLabel.AddThemeColorOverride("font_color", Colors.White);
            _logLabel.AddThemeFontSizeOverride("font_size", 12);
            AddChild(_logLabel);

            _statusLabel = new Label();
            _statusLabel.Position = new Vector2(10, 10);
            _statusLabel.AddThemeColorOverride("font_color", Colors.Yellow);
            _statusLabel.AddThemeFontSizeOverride("font_size", 16);
            AddChild(_statusLabel);

            _overlay = new AIDebugOverlay();
            _overlay.Init(_history, _visualizer.HexToPixel, _visualizer.HexSize);
            _overlay.Visible = false;
            AddChild(_overlay);

            _bubbles = new MessageBubbleLayer();
            _bubbles.Init(unit => _visualizer.HexToPixel(unit.Position));
            AddChild(_bubbles);
            _narrator = CreateNarrator();

            SubscribeLog(_manager);
            UpdateStatus();
            AppendLog("SPACE=Step  U=Undo  T=Threading  P=AutoPlay  R=Restart  D=AIDebug  M=Banter  Click=Inspect  [/]=ReplayTurn");
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed)
            {
                if (key.Keycode == Key.Space && !_playing && !_manager.IsBattleOver())
                {
                    StepAndPlay();
                }
                else if (key.Keycode == Key.P)
                {
                    _autoPlay = !_autoPlay;
                    AppendLog($"AutoPlay: {(_autoPlay ? "ON" : "OFF")}");
                }
                else if (key.Keycode == Key.T)
                {
                    _manager.UseThreads = !_manager.UseThreads;
                    AppendLog($"Threading: {(_manager.UseThreads ? "ON" : "OFF")}");
                }
                else if (key.Keycode == Key.U && !_playing && _manager.CanUndo)
                {
                    _manager.UndoLastTurn();
                    _orchestrator.SyncAll();
                    _bubbles.Clear(); // spoken past is stale after a rewind
                    AppendLog($"Undo → Turn {_manager.TurnNumber}");
                    UpdateStatus();
                }
                else if (key.Keycode == Key.R)
                {
                    _autoPlay = false;
                    _playing = false;
                    _manager = CreateBattleManager();
                    SubscribeLog(_manager);
                    _visualizer.SetState(_manager);
                    _orchestrator = new BattleOrchestrator(
                        new Dictionary<Unit, UnitVisual>(_visualizer.UnitVisuals),
                        _visualizer.HexToPixel);
                    _history.Clear();
                    _overlay.SelectedUnit = null;
                    _overlay.Refresh();
                    _bubbles.Clear();
                    _narrator = CreateNarrator();
                    _logText = "";
                    AppendLog("Battle restarted!");
                    UpdateStatus();
                }
                else if (key.Keycode == Key.M)
                {
                    _bubbles.Visible = !_bubbles.Visible;
                    AppendLog($"Banter: {(_bubbles.Visible ? "ON" : "OFF")}");
                }
                else if (key.Keycode == Key.D)
                {
                    _overlay.Visible = !_overlay.Visible;
                    AppendLog($"AI Debug: {(_overlay.Visible ? "ON — click a unit" : "OFF")}");
                    _overlay.Refresh();
                }
                else if (key.Keycode == Key.Bracketleft && _overlay.Visible)
                {
                    _overlay.ViewedTurn = System.Math.Max(1, _overlay.ViewedTurn - 1);
                    AppendLog($"AI Debug: viewing turn {_overlay.ViewedTurn}");
                    _overlay.Refresh();
                }
                else if (key.Keycode == Key.Bracketright && _overlay.Visible)
                {
                    int latest = _history.LatestTurn ?? 1;
                    _overlay.ViewedTurn = System.Math.Min(latest, _overlay.ViewedTurn + 1);
                    AppendLog($"AI Debug: viewing turn {_overlay.ViewedTurn}");
                    _overlay.Refresh();
                }
            }
            else if (@event is InputEventMouseButton mouse && mouse.Pressed
                     && mouse.ButtonIndex == MouseButton.Left && _overlay.Visible)
            {
                SelectUnitAt(mouse.Position);
            }
        }

        private void SelectUnitAt(Vector2 pixel)
        {
            Unit? best = null;
            float bestDist = _visualizer.HexSize;

            foreach (var unit in _manager.Battle.Units)
            {
                float dist = pixel.DistanceTo(_visualizer.HexToPixel(unit.Position));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = unit;
                }
            }

            if (best != null)
            {
                _overlay.SelectedUnit = best;
                _overlay.ViewedTurn = _history.LatestTurn ?? _manager.TurnNumber;
                AppendLog($"AI Debug: inspecting {best}");
                _overlay.Refresh();
            }
        }

        public override void _Process(double delta)
        {
            _logLabel.Text = _logText;

            if (_autoPlay && !_playing && !_manager.IsBattleOver())
                StepAndPlay();

            if (_autoPlay && _manager.IsBattleOver())
            {
                _autoPlay = false;
                AppendLog(">>> BATTLE OVER <<<");
                UpdateStatus();
            }
        }

        private async void StepAndPlay()
        {
            _playing = true;

            if (!_orchestrator.HasPending)
            {
                var commands = _manager.StepTurn();
                _orchestrator.Enqueue(commands);

                _history.Store(_manager.TurnNumber, _decisionLog.Snapshot());
                _decisionLog.Clear();
                if (_overlay.Visible)
                {
                    _overlay.ViewedTurn = _manager.TurnNumber;
                    _overlay.Refresh();
                }
            }

            UpdateStatus();
            await _orchestrator.PlayBatch();

            // Banter after the turn's animations — the narrator observed the
            // same executed commands the orchestrator just replayed.
            if (_narrator != null)
            {
                _narrator.ObserveTurn(_manager.Battle);
                foreach (var line in _narrator.DrainLines())
                    _bubbles.Show(line);
            }

            _playing = false;

            if (!_orchestrator.HasPending && _manager.IsBattleOver() && !_autoPlay)
                AppendLog(">>> BATTLE OVER <<<");

            UpdateStatus();
        }

        private void SubscribeLog(BattleManager manager)
        {
            manager.OnLog += msg =>
            {
                _logText += msg + "\n";
                var lines = _logText.Split('\n');
                if (lines.Length > 15)
                    _logText = string.Join("\n", lines[^15..]);
            };
        }

        private void UpdateStatus()
        {
            var counts = new SortedDictionary<int, int>();
            foreach (var u in _manager.Battle.Units)
            {
                if (!counts.ContainsKey(u.TeamIndex))
                    counts[u.TeamIndex] = 0;
                if (u.IsAlive)
                    counts[u.TeamIndex]++;
            }

            string teamInfo = string.Join("  ", counts.Select(kv => $"T{kv.Key}:{kv.Value}"));
            string mode = _autoPlay ? "AUTO" : (_manager.UseThreads ? "Threaded" : "Single");
            _statusLabel.Text = $"Turn: {_manager.TurnNumber}  |  {teamInfo}  |  {mode}  |  SPACE/U/P/T/R";
        }

        private void AppendLog(string msg)
        {
            _logText += msg + "\n";
        }

        // Same decisions as BattleManager's default planner (bare AIBrain,
        // empty context) — just captures a trace per unit for the overlay.
        private BattleManager CreateBattleManager()
        {
            var setup = BattleSetup.CreatePrototype();
            _decisionLog = new DecisionLog();
            return new BattleManager(setup.Battle, TracingPlan);
        }

        // Banter observer: orc voices for even teams, goblin for odd — same
        // split the headless --vision-ai demo uses. Falls back to silence if
        // resources are missing rather than failing the scene.
        private MessageEngine? CreateNarrator()
        {
            string root = ResolveMessageResources();
            if (root.Length == 0) return null;

            var meta = new CampaignContext();
            meta.Factions.Add("Orcs");
            meta.Factions.Add("Goblins");

            var orcLook = new UnitLookDef { Name = "Orc", Glyph = "O" };
            var goblinLook = new UnitLookDef { Name = "Goblin", Glyph = "g" };

            var engine = new MessageEngine(MessageRegistry.LoadFromDirectory(root), meta, seed: 42);
            UnitLooks.Clear();
            foreach (var unit in _manager.Battle.Units)
            {
                bool orc = unit.TeamIndex % 2 == 0;
                engine.SetVoice(unit, orc ? "Orc" : "Goblin");
                UnitLooks.Set(unit, orc ? orcLook : goblinLook);
            }
            engine.BeginBattle(_manager.Battle);
            return engine;
        }

        private static string ResolveMessageResources()
        {
            // Running from the editor: JSONs live in the source tree.
            string source = ProjectSettings.GlobalizePath("res://messages/Resources");
            if (System.IO.Directory.Exists(source)) return source;

            // Exported build: copied next to the assemblies.
            string deployed = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Resources");
            return System.IO.Directory.Exists(deployed) ? deployed : "";
        }

        private AIAction? TracingPlan(Unit unit, BattleState battle)
        {
            var bb = new AIBlackboard(battle, unit);
            var context = new ScoringContext();
            var trace = new DecisionTrace(unit, battle.TurnNumber) { Context = context };
            var action = AIBrain.DecideAction(unit, bb, context, trace);
            _decisionLog.Add(trace);
            return action;
        }
    }
}
