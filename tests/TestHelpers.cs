using System;
using System.Collections.Generic;
using TacticalGame.Grid;
using TacticalGame.Grid.Skills;

namespace TacticalGame.Tests
{
    /// <summary>
    /// Shared helpers for building minimal battle scenarios in tests.
    /// </summary>
    internal static class TestHelpers
    {
        public static UnitStats FreshStats(
            int attack = 10, int defense = 10, int resolve = 50,
            int maxHP = 100, int maxArmor = 50, int maxFatigue = 60,
            int morale = 70)
        {
            return UnitStats.Fresh(attack, defense, resolve, maxHP, maxArmor, maxFatigue, morale);
        }

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

        /// <summary>
        /// Creates a battle with two units on opposing teams, placed and equipped.
        /// Attacker at origin, defender at (1,0) (adjacent east).
        /// </summary>
        public static (BattleState battle, Unit attacker, Unit defender) MakeDuel(
            EquipmentDef? attackerWeapon = null, EquipmentDef? defenderWeapon = null)
        {
            var battle = MakeBattle();
            var attacker = MakeUnit("Attacker");
            var defender = MakeUnit("Defender");

            battle.PlaceUnit(attacker, HexCoord.Zero);
            battle.PlaceUnit(defender, new HexCoord(1, 0));
            battle.RegisterTeam(0, new List<Unit> { attacker });
            battle.RegisterTeam(1, new List<Unit> { defender });

            attackerWeapon ??= SampleWeapons.Axe;
            battle.Equip(attacker, new Equipment(attackerWeapon));

            if (defenderWeapon != null)
                battle.Equip(defender, new Equipment(defenderWeapon));

            return (battle, attacker, defender);
        }

        public static class SampleWeapons
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

            public static readonly EquipmentDef TwoHandedAxe = new("greataxe", "Greataxe", EquipmentSlot.RightHand,
                14, new StatBonus(attack: 18), new[] { Chop }, isTwoHanded: true);

            static SampleWeapons()
            {
                DefRegistry.Register(Axe);
                DefRegistry.Register(Sword);
                DefRegistry.Register(Shield);
                DefRegistry.Register(Helmet);
                DefRegistry.Register(TwoHandedAxe);
            }
        }
    }
}
