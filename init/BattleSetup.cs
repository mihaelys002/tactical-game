using System;
using System.Collections.Generic;
using TacticalGame.Grid;
using TacticalGame.Grid.Skills;

namespace TacticalGame
{
    public class BattleSetup
    {
        public BattleState Battle { get; }
        public IReadOnlyList<List<Unit>> Teams => _teams;

        private readonly List<List<Unit>> _teams = new();

        public BattleSetup(HexGrid grid)
        {
            Battle = new BattleState(grid);
        }

        public void AddTeam(List<Unit> units, List<HexCoord> positions)
        {
            if (units.Count != positions.Count)
                throw new ArgumentException("Units and positions count mismatch.");

            int teamIndex = _teams.Count;
            var team = new List<Unit>();
            _teams.Add(team);

            for (int i = 0; i < units.Count; i++)
            {
                Battle.PlaceUnit(units[i], positions[i]);
                team.Add(units[i]);
            }

            Battle.RegisterTeam(teamIndex, team);
        }

        // ─── Prototype Helpers ──────────────────────────────────────────

        private static readonly char[] TeamLetters = { 'A', 'B', 'C', 'D', 'E', 'F' };

        public static BattleSetup CreatePrototype()
        {
            var setup = new BattleSetup(CreateHexGrid(7));

            setup.AddRandomTeam(0, new HexCoord(-6, 0), new HexCoord(-6, 1), new HexCoord(-6, 2),
                                   new HexCoord(-6, 3), new HexCoord(-6, 4), new HexCoord(-6, 5));
            setup.AddRandomTeam(1, new HexCoord(6, 0), new HexCoord(6, -1), new HexCoord(6, -2),
                                   new HexCoord(6, -3), new HexCoord(6, -4), new HexCoord(6, -5));
            setup.AddRandomTeam(2, new HexCoord(0, -6), new HexCoord(1, -6), new HexCoord(2, -6),
                                   new HexCoord(3, -6), new HexCoord(4, -6), new HexCoord(5, -6));
            setup.AddRandomTeam(3, new HexCoord(0, 6), new HexCoord(-1, 6), new HexCoord(-2, 6),
                                   new HexCoord(-3, 6), new HexCoord(-4, 6), new HexCoord(-5, 6));

            return setup;
        }

        public static HexGrid CreateHexGrid(int radius)
        {
            var grid = new HexGrid();

            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Math.Max(-radius, -q - radius);
                int r2 = Math.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    var terrain = TerrainType.Plain;
                    int hash = q * 7 + r * 13;
                    if (hash % 5 == 0) terrain = TerrainType.Forest;
                    if (hash % 7 == 0) terrain = TerrainType.Hill;

                    var coord = new HexCoord(q, r);
                    grid.Cells[coord] = new HexCell(coord, terrain, terrain == TerrainType.Hill ? 1 : 0);
                }
            }

            return grid;
        }

        private void AddRandomTeam(int teamIndex, params HexCoord[] positions)
        {
            var rng = new Random(teamIndex * 1000);
            char letter = teamIndex < TeamLetters.Length ? TeamLetters[teamIndex] : '?';
            var units = new List<Unit>();
            var posList = new List<HexCoord>(positions);

            for (int i = 0; i < positions.Length; i++)
            {
                var stats = UnitStats.Fresh(
                    rng.Next(11, 19), rng.Next(8, 16), rng.Next(35, 60),
                    rng.Next(65, 95), rng.Next(25, 50), rng.Next(45, 70), rng.Next(55, 75));
                var unit = new Unit(stats) { Name = letter + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) };
                units.Add(unit);
            }

            AddTeam(units, posList);

            // Equip after placement
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (rng.Next(2) == 0)
                    Battle.Equip(unit, new Equipment(SampleEquipment.Axe));
                else
                    Battle.Equip(unit, new Equipment(SampleEquipment.Sword));

                if (rng.Next(3) == 0)
                    Battle.Equip(unit, new Equipment(SampleEquipment.Shield));

                if (rng.Next(2) == 0)
                    Battle.Equip(unit, new Equipment(SampleEquipment.Helmet));
            }
        }

        public static class SampleEquipment
        {
            private static readonly SkillDef Chop = new ChopSkill();
            private static readonly SkillDef Slash = new SlashSkill();
            private static readonly SkillDef ShieldWall = new ShieldWallSkill();

            public static readonly EquipmentDef Axe = new("axe", "Axe", EquipmentSlot.RightHand,
                8, new StatBonus(attack: 10), new[] { Chop });

            public static readonly EquipmentDef Sword = new("sword", "Sword", EquipmentSlot.RightHand,
                6, new StatBonus(attack: 7, defense: 3), new[] { Slash });

            public static readonly EquipmentDef Shield = new("shield", "Shield", EquipmentSlot.LeftHand,
                10, new StatBonus(defense: 15), new[] { ShieldWall });

            public static readonly EquipmentDef Helmet = new("helmet", "Helmet", EquipmentSlot.Helmet,
                5, new StatBonus(maxArmor: 20), Array.Empty<SkillDef>());

            static SampleEquipment()
            {
                DefRegistry.Register(Axe);
                DefRegistry.Register(Sword);
                DefRegistry.Register(Shield);
                DefRegistry.Register(Helmet);
            }
        }
    }
}
