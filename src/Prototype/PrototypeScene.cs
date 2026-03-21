using System.Collections.Generic;
using System.Linq;
using Godot;
using TacticalGame.Grid;

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

            SubscribeLog(_manager);
            UpdateStatus();
            AppendLog("SPACE=Step  U=Undo  T=Threading  P=AutoPlay  R=Restart");
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
                    _logText = "";
                    AppendLog("Battle restarted!");
                    UpdateStatus();
                }
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
            var commands = _manager.StepTurn();
            UpdateStatus();

            await _orchestrator.PlayTurn(commands);

            _playing = false;

            if (_manager.IsBattleOver() && !_autoPlay)
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

        private static BattleManager CreateBattleManager()
        {
            var setup = BattleSetup.CreatePrototype();
            return new BattleManager(setup.Battle);
        }
    }
}
