using System;
using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public class BattleState
    {
        private readonly HexGrid _grid;
        private readonly List<Unit> _units = new();
        private readonly HashSet<Unit> _unitSet = new();
        private readonly List<Loot> _loot = new();
        private readonly HashSet<Loot> _lootSet = new();

        public HexGrid Grid => _grid;
        public IReadOnlyList<Unit> Units => _units;
        public IReadOnlyList<Loot> Loot => _loot;

        public bool HasUnit(Unit unit) => _unitSet.Contains(unit);
        public bool HasLoot(Loot loot) => _lootSet.Contains(loot);

        public BattleState(HexGrid grid)
        {
            _grid = grid;
        }

        public void PlaceUnit(Unit unit, HexCoord coord)
        {
            var cell = GetCellOrThrow(coord);

            if (!cell.IsWalkable)
                throw new InvalidOperationException($"Cell {coord} is not walkable.");

            cell.AddOccupant(unit);
            unit.Position = coord;
            _units.Add(unit);
            _unitSet.Add(unit);
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
            _unitSet.Remove(unit);
        }

        public void PlaceLoot(Loot loot, HexCoord coord)
        {
            var cell = GetCellOrThrow(coord);

            if (cell.Loot != null)
                throw new InvalidOperationException($"Cell {coord} already has loot.");

            cell.Loot = loot;
            loot.Position = coord;
            _loot.Add(loot);
            _lootSet.Add(loot);
        }

        public void RemoveLoot(Loot loot)
        {
            var cell = GetCellOrThrow(loot.Position);
            cell.Loot = null;
            _loot.Remove(loot);
            _lootSet.Remove(loot);
        }

        public void ApplyDamage(Unit unit, int amount)
        {
            var stats = unit.Stats;
            int remaining = amount;

            if (stats.CurrentArmor > 0)
            {
                int absorbed = Math.Min(stats.CurrentArmor, remaining);
                stats.CurrentArmor -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0)
                stats.CurrentHP -= remaining;
        }

        public void ApplyFatigue(Unit unit, int amount)
        {
            var stats = unit.Stats;
            stats.CurrentFatigue = Math.Min(stats.MaxFatigue, stats.CurrentFatigue + amount);
        }

        public void RecoverFatigue(Unit unit, int amount)
        {
            var stats = unit.Stats;
            stats.CurrentFatigue = Math.Max(0, stats.CurrentFatigue - amount);
        }

        public void ChangeMorale(Unit unit, int delta)
        {
            var stats = unit.Stats;
            stats.Morale = Math.Clamp(stats.Morale + delta, 0, 100);
        }

        private HexCell GetCellOrThrow(HexCoord coord)
        {
            if (_grid.TryGetCell(coord, out var cell))
                return cell;

            throw new ArgumentException($"No cell at {coord}.");
        }
    }
}
