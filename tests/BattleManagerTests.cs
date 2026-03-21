using System.Collections.Generic;
using System.IO;
using System.Linq;
using TacticalGame.AI;
using TacticalGame.Grid;
using Xunit;

namespace TacticalGame.Tests
{
    public class BattleManagerTests
    {
        private static (BattleManager manager, BattleState battle, Unit u1, Unit u2) SetupDuel()
        {
            var (battle, u1, u2) = TestHelpers.MakeDuel();
            var manager = new BattleManager(battle, useThreads: false);
            return (manager, battle, u1, u2);
        }

        // ── Turn stepping ───────────────────────────────────────────────

        [Fact]
        public void StepTurn_IncrementsTurnNumber()
        {
            var (manager, _, _, _) = SetupDuel();
            Assert.Equal(0, manager.TurnNumber);

            manager.StepTurn();
            Assert.Equal(1, manager.TurnNumber);
        }

        [Fact]
        public void StepTurn_ReturnsExecutedCommands()
        {
            var (manager, _, _, _) = SetupDuel();
            var commands = manager.StepTurn();

            Assert.NotEmpty(commands);
        }

        [Fact]
        public void StepTurn_RecoversFatigue()
        {
            var (manager, battle, u1, _) = SetupDuel();

            // Add fatigue
            battle.ChangeFatigue(u1, 30);
            Assert.Equal(30, u1.Stats.CurrentFatigue);

            manager.StepTurn();

            // Should have recovered 15 fatigue (minus whatever the skill added)
            // At minimum, we can verify the recovery happened by checking it's not still 30
            // (the unit will also gain fatigue from attacking, so we just verify it changed)
            Assert.NotEqual(30, u1.Stats.CurrentFatigue);
        }

        // ── Undo ────────────────────────────────────────────────────────

        [Fact]
        public void UndoLastTurn_RestoresState()
        {
            var (manager, _, u1, u2) = SetupDuel();
            int u1HP = u1.Stats.CurrentHP;
            int u2HP = u2.Stats.CurrentHP;
            int u1Armor = u1.Stats.CurrentArmor;
            int u2Armor = u2.Stats.CurrentArmor;

            manager.StepTurn();
            manager.UndoLastTurn();

            Assert.Equal(u1HP, u1.Stats.CurrentHP);
            Assert.Equal(u2HP, u2.Stats.CurrentHP);
            Assert.Equal(u1Armor, u1.Stats.CurrentArmor);
            Assert.Equal(u2Armor, u2.Stats.CurrentArmor);
        }

        [Fact]
        public void UndoLastTurn_ReturnsFalse_WhenNoHistory()
        {
            var (manager, _, _, _) = SetupDuel();
            Assert.False(manager.UndoLastTurn());
        }

        [Fact]
        public void CanUndo_TracksHistory()
        {
            var (manager, _, _, _) = SetupDuel();
            Assert.False(manager.CanUndo);

            manager.StepTurn();
            Assert.True(manager.CanUndo);

            manager.UndoLastTurn();
            Assert.False(manager.CanUndo);
        }

        [Fact]
        public void UndoLastTurn_DecrementsTurnNumber()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();
            Assert.Equal(1, manager.TurnNumber);

            manager.UndoLastTurn();
            Assert.Equal(0, manager.TurnNumber);
        }

        // ── Battle over ─────────────────────────────────────────────────

        [Fact]
        public void IsBattleOver_FalseWithTwoAliveTeams()
        {
            var (manager, _, _, _) = SetupDuel();
            Assert.False(manager.IsBattleOver());
        }

        [Fact]
        public void IsBattleOver_TrueWhenOneTeamRemains()
        {
            var (manager, battle, _, u2) = SetupDuel();
            battle.ChangeHP(u2, -u2.Stats.MaxHP);

            Assert.True(manager.IsBattleOver());
        }

        [Fact]
        public void IsBattleOver_TrueWhenAllDead()
        {
            var (manager, battle, u1, u2) = SetupDuel();
            battle.ChangeHP(u1, -u1.Stats.MaxHP);
            battle.ChangeHP(u2, -u2.Stats.MaxHP);

            Assert.True(manager.IsBattleOver());
        }

        // ── Custom planner ──────────────────────────────────────────────

        [Fact]
        public void CustomPlanner_IsUsed()
        {
            var (battle, u1, u2) = TestHelpers.MakeDuel();
            int plannerCalled = 0;

            PlanAction planner = (unit, b) =>
            {
                plannerCalled++;
                return null; // skip action
            };

            var manager = new BattleManager(battle, planner, useThreads: false);
            manager.StepTurn();

            Assert.True(plannerCalled > 0);
        }

        // ── Multi-turn combat ───────────────────────────────────────────

        [Fact]
        public void MultipleStepTurns_EventuallyEndBattle()
        {
            var (manager, _, _, _) = SetupDuel();

            for (int i = 0; i < 100; i++)
            {
                if (manager.IsBattleOver()) break;
                manager.StepTurn();
            }

            Assert.True(manager.IsBattleOver());
        }

        // ── OnLog event ─────────────────────────────────────────────────

        [Fact]
        public void OnLog_FiresDuringExecution()
        {
            var (manager, _, _, _) = SetupDuel();
            var logs = new List<string>();
            manager.OnLog += msg => logs.Add(msg);

            manager.StepTurn();

            Assert.NotEmpty(logs);
        }

        // ── Save/Load ─────────────────────────────────────────────────────

        [Fact]
        public void SaveLoad_TurnNumber()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);

            Assert.Equal(1, loaded.TurnNumber);
        }

        [Fact]
        public void SaveLoad_GridPreservesCellCount()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);

            Assert.Equal(37, loaded.Battle.Grid.Cells.Count);
        }

        [Fact]
        public void SaveLoad_GridPreservesCellCoords()
        {
            var (manager, battle, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);

            foreach (var coord in battle.Grid.Cells.Keys)
                Assert.True(loaded.Battle.Grid.Cells.ContainsKey(coord), $"Missing cell {coord}");
        }

        [Fact]
        public void SaveLoad_GridPreservesTerrain()
        {
            var battle = TestHelpers.MakeBattle();
            var hillCoord = new HexCoord(1, 0);
            battle.Grid.Cells[hillCoord] = new HexCell(hillCoord, TerrainType.Hill, 2);
            var manager = new BattleManager(battle, useThreads: false);

            var loaded = SaveAndLoad(manager);
            var cell = loaded.Battle.Grid.Cells[hillCoord];

            Assert.Equal(TerrainType.Hill, cell.Terrain);
            Assert.Equal(2, cell.Elevation);
        }

        [Fact]
        public void SaveLoad_UnitCount()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);

            Assert.Equal(2, loaded.Battle.Units.Count);
        }

        [Fact]
        public void SaveLoad_UnitName()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);

            Assert.Equal("Attacker", loaded.Battle.Units[0].Name);
            Assert.Equal("Defender", loaded.Battle.Units[1].Name);
        }

        [Fact]
        public void SaveLoad_UnitPosition()
        {
            var (manager, _, u1, u2) = SetupDuel();

            var loaded = SaveAndLoad(manager);

            Assert.Equal(u1.Position, loaded.Battle.Units[0].Position);
            Assert.Equal(u2.Position, loaded.Battle.Units[1].Position);
        }

        [Fact]
        public void SaveLoad_UnitTeamIndex()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);

            Assert.Equal(0, loaded.Battle.Units[0].TeamIndex);
            Assert.Equal(1, loaded.Battle.Units[1].TeamIndex);
        }

        [Fact]
        public void SaveLoad_UnitStatsAllFields()
        {
            var (manager, battle, u1, _) = SetupDuel();
            battle.ChangeHP(u1, -30);
            battle.ChangeArmor(u1, -10);
            battle.ChangeFatigue(u1, 25);
            battle.ChangeMorale(u1, -15);

            var loaded = SaveAndLoad(manager);
            var s = loaded.Battle.Units[0].Stats;

            Assert.Equal(u1.Stats.Attack, s.Attack);
            Assert.Equal(u1.Stats.Defense, s.Defense);
            Assert.Equal(u1.Stats.Resolve, s.Resolve);
            Assert.Equal(u1.Stats.MaxHP, s.MaxHP);
            Assert.Equal(u1.Stats.CurrentHP, s.CurrentHP);
            Assert.Equal(u1.Stats.MaxArmor, s.MaxArmor);
            Assert.Equal(u1.Stats.CurrentArmor, s.CurrentArmor);
            Assert.Equal(u1.Stats.MaxFatigue, s.MaxFatigue);
            Assert.Equal(u1.Stats.CurrentFatigue, s.CurrentFatigue);
            Assert.Equal(u1.Stats.Morale, s.Morale);
        }

        [Fact]
        public void SaveLoad_EquipmentDefAndDurability()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);
            var loadedU1 = loaded.Battle.Units[0];

            Assert.True(loadedU1.Equipment.TryGetValue(EquipmentSlot.RightHand, out var weapon));
            Assert.Equal("axe", weapon.Def.Id);
            Assert.Equal("Axe", weapon.Def.Name);
            Assert.Equal(100, weapon.CurrentDurability);
        }

        [Fact]
        public void SaveLoad_EquipmentMultipleSlots()
        {
            var (battle, u1, _) = TestHelpers.MakeDuel(defenderWeapon: TestHelpers.SampleWeapons.Sword);
            battle.Equip(u1, new Equipment(TestHelpers.SampleWeapons.Helmet));
            var manager = new BattleManager(battle, useThreads: false);

            var loaded = SaveAndLoad(manager);
            var loadedU1 = loaded.Battle.Units[0];

            Assert.True(loadedU1.Equipment.ContainsKey(EquipmentSlot.RightHand));
            Assert.True(loadedU1.Equipment.ContainsKey(EquipmentSlot.Helmet));
            Assert.Equal("Axe", loadedU1.Equipment[EquipmentSlot.RightHand].Def.Name);
            Assert.Equal("Helmet", loadedU1.Equipment[EquipmentSlot.Helmet].Def.Name);
        }

        [Fact]
        public void SaveLoad_UnequippedUnitHasNoEquipment()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);
            var loadedU2 = loaded.Battle.Units[1];

            Assert.Empty(loadedU2.Equipment);
        }

        [Fact]
        public void SaveLoad_CellOccupants()
        {
            var (manager, _, u1, u2) = SetupDuel();

            var loaded = SaveAndLoad(manager);
            var cell1 = loaded.Battle.Grid.Cells[u1.Position];
            var cell2 = loaded.Battle.Grid.Cells[u2.Position];

            Assert.Single(cell1.Occupants);
            Assert.Single(cell2.Occupants);
            Assert.Equal("Attacker", cell1.Occupants[0].Name);
            Assert.Equal("Defender", cell2.Occupants[0].Name);
        }

        [Fact]
        public void SaveLoad_CellIsWalkable()
        {
            var (manager, _, u1, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);
            var occupied = loaded.Battle.Grid.Cells[u1.Position];
            var empty = loaded.Battle.Grid.Cells[new HexCoord(2, 0)];

            Assert.False(occupied.IsWalkable);
            Assert.True(empty.IsWalkable);
        }

        [Fact]
        public void SaveLoad_EmptyCellHasNoOccupants()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);
            var emptyCell = loaded.Battle.Grid.Cells[new HexCoord(2, 0)];

            Assert.Empty(emptyCell.Occupants);
        }

        [Fact]
        public void SaveLoad_UndoHistory()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);

            Assert.True(loaded.CanUndo);
        }

        [Fact]
        public void SaveLoad_UndoActuallyWorks()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);
            var u1Before = loaded.Battle.Units[0].Stats.CurrentFatigue;

            loaded.UndoLastTurn();

            Assert.Equal(0, loaded.TurnNumber);
            Assert.NotEqual(u1Before, loaded.Battle.Units[0].Stats.CurrentFatigue);
        }

        [Fact]
        public void SaveLoad_UnitIdentityPreserved_InCells()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);
            var loadedU1 = loaded.Battle.Units[0];
            var cellOccupant = loaded.Battle.Grid.Cells[loadedU1.Position].Occupants[0];

            Assert.Same(loadedU1, cellOccupant);
        }

        [Fact]
        public void SaveLoad_UnitIdentityPreserved_InCommands()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);
            var loadedU1 = loaded.Battle.Units[0];
            var cmd = loaded.Battle.TurnHistory[0][0];

            if (cmd is CompoundCommand cc)
                Assert.Same(loadedU1, cc.Unit);
            else if (cmd is MoveCommand mc)
                Assert.Same(loadedU1, mc.Unit);
        }

        [Fact]
        public void SaveLoad_CompoundCommandFields()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);
            var compound = loaded.Battle.TurnHistory[0]
                .OfType<CompoundCommand>().First();

            Assert.Equal("axe", compound.Weapon.Id);
            Assert.Equal("chop", compound.Skill.Id);
            Assert.NotEqual(HexCoord.Zero, compound.TargetHex);
            Assert.NotEmpty(compound.Effects);
        }

        [Fact]
        public void SaveLoad_DamageEffectFields()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);
            var compound = loaded.Battle.TurnHistory[0]
                .OfType<CompoundCommand>().First();
            var dmg = compound.Effects.OfType<DamageEffect>().First();

            Assert.NotNull(dmg.Source);
            Assert.NotNull(dmg.Target);
            Assert.True(dmg.Amount > 0);
            Assert.True(dmg.AppliedArmorDamage > 0 || dmg.AppliedHpDamage > 0);
            Assert.True(dmg.IsEssential);
        }

        [Fact]
        public void SaveLoad_FatigueEffectFields()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);
            var compound = loaded.Battle.TurnHistory[0]
                .OfType<CompoundCommand>().First();
            var fat = compound.Effects.OfType<FatigueEffect>().First();

            Assert.NotNull(fat.Target);
            Assert.True(fat.Amount > 0);
            Assert.Equal(fat.Amount, fat.AppliedFatigue);
            Assert.False(fat.IsEssential);
        }

        [Fact]
        public void SaveLoad_MoveCommandFields()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);
            var move = loaded.Battle.TurnHistory[0]
                .OfType<MoveCommand>().FirstOrDefault();

            if (move != null)
            {
                Assert.NotNull(move.Unit);
                Assert.NotEqual(move.From, move.To);
            }
        }

        [Fact]
        public void SaveLoad_EquipmentDefIsSameRegistryInstance()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);
            var loadedWeapon = loaded.Battle.Units[0].Equipment[EquipmentSlot.RightHand];

            Assert.Same(TestHelpers.SampleWeapons.Axe, loadedWeapon.Def);
        }

        [Fact]
        public void SaveLoad_EquipmentDefPropertiesIntact()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);
            var def = loaded.Battle.Units[0].Equipment[EquipmentSlot.RightHand].Def;

            Assert.Equal(EquipmentSlot.RightHand, def.Slot);
            Assert.Equal(8, def.Weight);
            Assert.Equal(10, def.Bonus.Attack);
            Assert.False(def.IsTwoHanded);
            Assert.Single(def.GrantedSkills);
        }

        [Fact]
        public void SaveLoad_SkillDefIsSameRegistryInstance()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);
            var compound = loaded.Battle.TurnHistory[0]
                .OfType<CompoundCommand>().First();

            Assert.Same(DefRegistry.GetSkill("chop"), compound.Skill);
        }

        [Fact]
        public void SaveLoad_WeaponDefSameInCommandAndEquipment()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);
            var compound = loaded.Battle.TurnHistory[0]
                .OfType<CompoundCommand>().First();
            var equippedDef = loaded.Battle.Units[0].Equipment[EquipmentSlot.RightHand].Def;

            Assert.Same(equippedDef, compound.Weapon);
        }

        [Fact]
        public void SaveLoad_TraitsPreserved()
        {
            var trait = new TestTrait("test_trait");
            DefRegistry.Register(trait);

            var (battle, u1, _) = TestHelpers.MakeDuel();
            u1.Traits.Add(trait);
            var manager = new BattleManager(battle, useThreads: false);

            var loaded = SaveAndLoad(manager);
            var loadedU1 = loaded.Battle.Units[0];

            Assert.Single(loadedU1.Traits);
            Assert.Equal("test_trait", loadedU1.Traits[0].Id);
            Assert.Same(trait, loadedU1.Traits[0]);
        }

        [Fact]
        public void SaveLoad_FullBattleScene()
        {
            // ── Setup: a real battle mid-game ──────────────────────────────
            var trait = new TestTrait("tough");
            DefRegistry.Register(trait);

            var battle = TestHelpers.MakeBattle();

            // Two equipped units on opposing teams
            var u1 = TestHelpers.MakeUnit("Knight", attack: 15, defense: 12, maxHP: 120, maxArmor: 60);
            var u2 = TestHelpers.MakeUnit("Bandit", attack: 12, defense: 8, maxHP: 80, maxArmor: 30);

            battle.PlaceUnit(u1, HexCoord.Zero);
            battle.PlaceUnit(u2, new HexCoord(1, 0));
            battle.RegisterTeam(0, new List<Unit> { u1 });
            battle.RegisterTeam(1, new List<Unit> { u2 });

            battle.Equip(u1, new Equipment(TestHelpers.SampleWeapons.Axe));
            battle.Equip(u1, new Equipment(TestHelpers.SampleWeapons.Helmet));
            battle.Equip(u2, new Equipment(TestHelpers.SampleWeapons.Sword));
            battle.Equip(u2, new Equipment(TestHelpers.SampleWeapons.Shield));

            u1.Traits.Add(trait);

            // Simulate mid-battle state
            battle.ChangeHP(u1, -25);
            battle.ChangeArmor(u1, -15);
            battle.ChangeFatigue(u1, 20);
            battle.ChangeMorale(u1, -10);

            battle.ChangeHP(u2, -40);
            battle.ChangeArmor(u2, -20);
            battle.ChangeFatigue(u2, 35);
            battle.ChangeMorale(u2, -30);

            // Run 2 turns of combat
            var manager = new BattleManager(battle, useThreads: false);
            manager.StepTurn();
            manager.StepTurn();

            // ── Snapshot before save ────────────────────────────────────────
            var turnNumber = manager.TurnNumber;
            var historyCount = manager.Battle.TurnHistory.Count;

            var u1Stats = u1.Stats;
            var u1Pos = u1.Position;
            var u1Team = u1.TeamIndex;
            var u1HP = u1Stats.CurrentHP;
            var u1Armor = u1Stats.CurrentArmor;
            var u1Fatigue = u1Stats.CurrentFatigue;
            var u1Morale = u1Stats.Morale;

            var u2Stats = u2.Stats;
            var u2Pos = u2.Position;
            var u2Team = u2.TeamIndex;
            var u2HP = u2Stats.CurrentHP;
            var u2Armor = u2Stats.CurrentArmor;
            var u2Fatigue = u2Stats.CurrentFatigue;
            var u2Morale = u2Stats.Morale;

            // ── Save & Load ────────────────────────────────────────────────
            var loaded = SaveAndLoad(manager);
            var b = loaded.Battle;
            var lu1 = b.Units[0];
            var lu2 = b.Units[1];

            // ── Verify: turn state ─────────────────────────────────────────
            Assert.Equal(turnNumber, loaded.TurnNumber);
            Assert.Equal(historyCount, b.TurnHistory.Count);
            Assert.True(loaded.CanUndo);

            // ── Verify: grid ───────────────────────────────────────────────
            Assert.Equal(37, b.Grid.Cells.Count);

            // ── Verify: unit 1 ─────────────────────────────────────────────
            Assert.Equal("Knight", lu1.Name);
            Assert.Equal(u1Pos, lu1.Position);
            Assert.Equal(u1Team, lu1.TeamIndex);
            Assert.Equal(u1HP, lu1.Stats.CurrentHP);
            Assert.Equal(u1Stats.MaxHP, lu1.Stats.MaxHP);
            Assert.Equal(u1Armor, lu1.Stats.CurrentArmor);
            Assert.Equal(u1Stats.MaxArmor, lu1.Stats.MaxArmor);
            Assert.Equal(u1Fatigue, lu1.Stats.CurrentFatigue);
            Assert.Equal(u1Stats.MaxFatigue, lu1.Stats.MaxFatigue);
            Assert.Equal(u1Morale, lu1.Stats.Morale);
            Assert.Equal(u1Stats.Attack, lu1.Stats.Attack);
            Assert.Equal(u1Stats.Defense, lu1.Stats.Defense);
            Assert.Equal(u1Stats.Resolve, lu1.Stats.Resolve);

            // ── Verify: unit 2 ─────────────────────────────────────────────
            Assert.Equal("Bandit", lu2.Name);
            Assert.Equal(u2Pos, lu2.Position);
            Assert.Equal(u2Team, lu2.TeamIndex);
            Assert.Equal(u2HP, lu2.Stats.CurrentHP);
            Assert.Equal(u2Stats.MaxHP, lu2.Stats.MaxHP);
            Assert.Equal(u2Armor, lu2.Stats.CurrentArmor);
            Assert.Equal(u2Stats.MaxArmor, lu2.Stats.MaxArmor);
            Assert.Equal(u2Fatigue, lu2.Stats.CurrentFatigue);
            Assert.Equal(u2Stats.MaxFatigue, lu2.Stats.MaxFatigue);
            Assert.Equal(u2Morale, lu2.Stats.Morale);
            Assert.Equal(u2Stats.Attack, lu2.Stats.Attack);
            Assert.Equal(u2Stats.Defense, lu2.Stats.Defense);
            Assert.Equal(u2Stats.Resolve, lu2.Stats.Resolve);

            // ── Verify: equipment ──────────────────────────────────────────
            Assert.Equal(2, lu1.Equipment.Count);
            Assert.Same(TestHelpers.SampleWeapons.Axe, lu1.Equipment[EquipmentSlot.RightHand].Def);
            Assert.Same(TestHelpers.SampleWeapons.Helmet, lu1.Equipment[EquipmentSlot.Helmet].Def);

            Assert.Equal(2, lu2.Equipment.Count);
            Assert.Same(TestHelpers.SampleWeapons.Sword, lu2.Equipment[EquipmentSlot.RightHand].Def);
            Assert.Same(TestHelpers.SampleWeapons.Shield, lu2.Equipment[EquipmentSlot.LeftHand].Def);

            // ── Verify: traits ─────────────────────────────────────────────
            Assert.Single(lu1.Traits);
            Assert.Same(trait, lu1.Traits[0]);
            Assert.Empty(lu2.Traits);

            // ── Verify: cell occupants ─────────────────────────────────────
            var cell1 = b.Grid.Cells[lu1.Position];
            var cell2 = b.Grid.Cells[lu2.Position];
            Assert.Single(cell1.Occupants);
            Assert.Same(lu1, cell1.Occupants[0]);
            Assert.Single(cell2.Occupants);
            Assert.Same(lu2, cell2.Occupants[0]);
            Assert.False(cell1.IsWalkable);
            Assert.False(cell2.IsWalkable);

            // ── Verify: empty cells are walkable ───────────────────────────
            var emptyCoord = new HexCoord(-1, 0);
            Assert.True(b.Grid.Cells[emptyCoord].IsWalkable);
            Assert.Empty(b.Grid.Cells[emptyCoord].Occupants);

            // ── Verify: unit identity across the graph ─────────────────────
            var cmd = b.TurnHistory[0].OfType<CompoundCommand>().First();
            Assert.Same(lu1, cmd.Unit);
            Assert.NotNull(cmd.Effects.OfType<DamageEffect>().First().Target);

            // ── Verify: undo still works after load ────────────────────────
            loaded.UndoLastTurn();
            Assert.Equal(turnNumber - 1, loaded.TurnNumber);
        }

        private sealed class TestTrait : ITrait
        {
            public string Id { get; }
            public TestTrait(string id) { Id = id; }
            public void ModifyEffects(PrototypeCommand cmd, Unit traitOwner) { }
        }

        private static BattleManager SaveAndLoad(BattleManager manager)
        {
            var stream = new MemoryStream();
            BattleSave.Save(manager.Battle, stream);
            stream.Position = 0;
            var state = BattleSave.Load(stream);
            return new BattleManager(state, useThreads: false);
        }
    }
}
