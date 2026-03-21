using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace TacticalGame.Grid
{
    [SuppressMessage("Performance", "CA1822", Justification = "Instance methods by design — BattleState is the mutation gateway")]
    [JsonObject(MemberSerialization.Fields)]
    public class BattleState
    {
        private readonly HexGrid _grid;
        private readonly List<Unit> _units = new();
        private readonly List<List<IBattleCommand>> _turnHistory = new();
        private int _turnNumber;

        public HexGrid Grid => _grid;
        public IReadOnlyList<Unit> Units => _units;
        public IReadOnlyList<List<IBattleCommand>> TurnHistory => _turnHistory;
        public int TurnNumber => _turnNumber;

        public BattleState(HexGrid grid)
        {
            _grid = grid;
        }

        public void RegisterTeam(int teamIndex, List<Unit> units)
        {
            foreach (var unit in units)
                unit.TeamIndex = teamIndex;
        }

        public List<Unit> GetAllies(Unit unit)
        {
            int team = unit.TeamIndex;
            if (team < 0) return new List<Unit>();

            var allies = new List<Unit>();
            foreach (var u in _units)
                if (u != unit && u.TeamIndex == team && u.IsAlive) allies.Add(u);
            return allies;
        }

        public List<Unit> GetEnemies(Unit unit)
        {
            int team = unit.TeamIndex;
            if (team < 0) return new List<Unit>();

            var enemies = new List<Unit>();
            foreach (var u in _units)
                if (u.TeamIndex != team && u.IsAlive) enemies.Add(u);
            return enemies;
        }

        public void PlaceUnit(Unit unit, HexCoord coord)
        {
            var cell = _grid.Cells[coord];

            if (!cell.IsWalkable)
                throw new InvalidOperationException($"Cell {coord} is not walkable.");

            cell.AddOccupant(unit);
            unit.Position = coord;
            _units.Add(unit);
        }

        public void MoveUnit(Unit unit, HexCoord target)
        {
            var oldCell = _grid.Cells[unit.Position];
            var newCell = _grid.Cells[target];

            if (!newCell.IsWalkable)
                throw new InvalidOperationException($"Cell {target} is not walkable.");

            oldCell.RemoveOccupant(unit);
            newCell.AddOccupant(unit);
            unit.Position = target;
        }

        public void RemoveUnit(Unit unit)
        {
            var cell = _grid.Cells[unit.Position];
            cell.RemoveOccupant(unit);
            _units.Remove(unit);
        }

        public void Equip(Unit unit, Equipment equipment)
        {
            if (!_units.Contains(unit))
                throw new InvalidOperationException("Unit is not in battle.");

            var slot = equipment.Def.Slot;

            if (unit.Equipment.ContainsKey(slot))
                throw new InvalidOperationException($"Slot {slot} is already occupied.");

            if (equipment.Def.IsTwoHanded)
            {
                if (unit.Equipment.ContainsKey(EquipmentSlot.LeftHand))
                    throw new InvalidOperationException("Left hand must be empty for two-handed weapon.");
            }

            if (slot == EquipmentSlot.LeftHand
                && unit.Equipment.TryGetValue(EquipmentSlot.RightHand, out var rh)
                && rh.Def.IsTwoHanded)
                throw new InvalidOperationException("Cannot equip left hand while wielding a two-handed weapon.");

            unit.Equipment[slot] = equipment;
        }

        public Equipment? Unequip(Unit unit, EquipmentSlot slot)
        {
            if (!_units.Contains(unit))
                throw new InvalidOperationException("Unit is not in battle.");

            return unit.Equipment.Remove(slot, out var removed) ? removed : null;
        }

        public int ChangeHP(Unit unit, int delta)
        {
            var stats = unit.Stats;
            int old = stats.CurrentHP;
            stats.CurrentHP = Math.Clamp(old + delta, 0, stats.MaxHP);
            return stats.CurrentHP - old;
        }

        public int ChangeArmor(Unit unit, int delta)
        {
            var stats = unit.Stats;
            int old = stats.CurrentArmor;
            stats.CurrentArmor = Math.Clamp(old + delta, 0, stats.MaxArmor);
            return stats.CurrentArmor - old;
        }

        public int ChangeFatigue(Unit unit, int delta)
        {
            var stats = unit.Stats;
            int old = stats.CurrentFatigue;
            stats.CurrentFatigue = Math.Clamp(old + delta, 0, stats.MaxFatigue);
            return stats.CurrentFatigue - old;
        }

        public int ChangeMorale(Unit unit, int delta)
        {
            var stats = unit.Stats;
            int old = stats.Morale;
            stats.Morale = Math.Clamp(old + delta, 0, 100);
            return stats.Morale - old;
        }

        // ─── Turn tracking ──────────────────────────────────────────────

        public void AdvanceTurn() => _turnNumber++;

        public void RewindTurn() => _turnNumber--;

        public void RecordTurn(List<IBattleCommand> commands) => _turnHistory.Add(commands);

        public List<IBattleCommand>? PopLastTurn()
        {
            if (_turnHistory.Count == 0) return null;
            var commands = _turnHistory[^1];
            _turnHistory.RemoveAt(_turnHistory.Count - 1);
            return commands;
        }

    }
}
