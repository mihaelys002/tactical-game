using System;
using System.Collections.Generic;
using TacticalGame.Grid;

namespace TacticalGame.Visual.Tests
{
    internal static class TestHelpers
    {
        public static Unit MakeUnit(string name = "TestUnit", int attack = 10, int defense = 10,
            int maxHP = 100, int maxArmor = 50)
        {
            var stats = UnitStats.Fresh(attack, defense, resolve: 50,
                maxHP: maxHP, maxArmor: maxArmor, maxFatigue: 60, morale: 70);
            return new Unit(stats) { Name = name };
        }

        public static HexGrid MakeGrid(int radius = 3)
        {
            var grid = new HexGrid();
            for (int q = -radius; q <= radius; q++)
            {
                int r1 = Math.Max(-radius, -q - radius);
                int r2 = Math.Min(radius, -q + radius);
                for (int r = r1; r <= r2; r++)
                {
                    var coord = new HexCoord(q, r);
                    grid.Cells[coord] = new HexCell(coord, TerrainType.Plain, 0);
                }
            }
            return grid;
        }

        public static BattleState MakeBattle(int gridRadius = 3)
        {
            return new BattleState(MakeGrid(gridRadius));
        }

        public static class SampleWeapons
        {
            private static readonly SkillDef Chop = new ChopSkill();

            public static readonly EquipmentDef Axe = new("axe_vt", "Axe", EquipmentSlot.RightHand,
                8, new StatBonus(attack: 10), new[] { Chop });

            static SampleWeapons()
            {
                DefRegistry.Register(Axe);
            }
        }
    }
}
