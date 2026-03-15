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
        private readonly List<List<Unit>> _teams = new();
        public HexGrid Grid => _grid;
        public IReadOnlyList<Unit> Units => _units;
        public bool HasUnit(Unit unit) => _units.Contains(unit);
        public IReadOnlyList<List<Unit>> Teams => _teams;
        public int TeamCount => _teams.Count;

        public BattleState(HexGrid grid)
        {
            _grid = grid;
        }

        public int RegisterTeam(List<Unit> units)
        {
            int teamIndex = _teams.Count;
            _teams.Add(units);
            foreach (var unit in units)
                unit.TeamIndex = teamIndex;
            return teamIndex;
        }

        public List<Unit> GetAllies(Unit unit)
        {
            int team = unit.TeamIndex;
            if (team < 0) return new List<Unit>();

            var allies = new List<Unit>();
            foreach (var u in _teams[team])
                if (u != unit && u.IsAlive) allies.Add(u);
            return allies;
        }

        public List<Unit> GetEnemies(Unit unit)
        {
            int team = unit.TeamIndex;
            if (team < 0) return new List<Unit>();

            var enemies = new List<Unit>();
            for (int t = 0; t < _teams.Count; t++)
            {
                if (t == team) continue;
                foreach (var u in _teams[t])
                    if (u.IsAlive) enemies.Add(u);
            }
            return enemies;
        }

        public void PlaceUnit(Unit unit, HexCoord coord)
        {
            var cell = GetCellOrThrow(coord);

            if (!cell.IsWalkable)
                throw new InvalidOperationException($"Cell {coord} is not walkable.");

            cell.AddOccupant(unit);
            unit.Position = coord;
            _units.Add(unit);
        }

        public void MoveUnit(Unit unit, HexCoord target)
        {
            var oldCell = GetCellOrThrow(unit.Position);
            var newCell = GetCellOrThrow(target);

            if (!newCell.IsWalkable)
                throw new InvalidOperationException($"Cell {target} is not walkable.");

            oldCell.RemoveOccupant(unit);
            newCell.AddOccupant(unit);
            unit.Position = target;
        }

        public void RemoveUnit(Unit unit)
        {
            var cell = GetCellOrThrow(unit.Position);
            cell.RemoveOccupant(unit);
            _units.Remove(unit);
        }

        public void Equip(Unit unit, Equipment equipment)
        {
            if (!HasUnit(unit))
                throw new InvalidOperationException("Unit is not in battle.");

            var slot = equipment.Def.Slot;

            if (unit.Equipment.Has(slot))
                throw new InvalidOperationException($"Slot {slot} is already occupied.");

            if (equipment.Def.IsTwoHanded)
            {
                if (unit.Equipment.Has(EquipmentSlot.LeftHand))
                    throw new InvalidOperationException("Left hand must be empty for two-handed weapon.");
            }

            if (slot == EquipmentSlot.LeftHand && unit.Equipment.Get(EquipmentSlot.RightHand)?.Def.IsTwoHanded == true)
                throw new InvalidOperationException("Cannot equip left hand while wielding a two-handed weapon.");

            unit.Equipment.Set(slot, equipment);
        }

        public Equipment? Unequip(Unit unit, EquipmentSlot slot)
        {
            if (!HasUnit(unit))
                throw new InvalidOperationException("Unit is not in battle.");

            return unit.Equipment.Remove(slot);
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

        private HexCell GetCellOrThrow(HexCoord coord)
        {
            if (_grid.TryGetCell(coord, out var cell))
                return cell;

            throw new ArgumentException($"No cell at {coord}.");
        }
    }
}
