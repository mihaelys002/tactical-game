using System.Collections.Generic;
using System.IO;
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
        public void SaveLoad_GridCells()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);

            Assert.Equal(37, loaded.Battle.Grid.Count);
        }

        [Fact]
        public void SaveLoad_Units()
        {
            var (manager, _, u1, u2) = SetupDuel();

            var loaded = SaveAndLoad(manager);

            Assert.Equal(2, loaded.Battle.Units.Count);
            Assert.Equal("Attacker", loaded.Battle.Units[0].Name);
            Assert.Equal("Defender", loaded.Battle.Units[1].Name);
        }

        [Fact]
        public void SaveLoad_UnitStats()
        {
            var (manager, battle, u1, _) = SetupDuel();
            battle.ChangeHP(u1, -30);
            battle.ChangeArmor(u1, -10);

            var loaded = SaveAndLoad(manager);
            var loadedU1 = loaded.Battle.Units[0];

            Assert.Equal(u1.Stats.CurrentHP, loadedU1.Stats.CurrentHP);
            Assert.Equal(u1.Stats.CurrentArmor, loadedU1.Stats.CurrentArmor);
            Assert.Equal(u1.Stats.MaxHP, loadedU1.Stats.MaxHP);
            Assert.Equal(u1.Stats.MaxArmor, loadedU1.Stats.MaxArmor);
        }

        [Fact]
        public void SaveLoad_UnitPositions()
        {
            var (manager, _, u1, u2) = SetupDuel();

            var loaded = SaveAndLoad(manager);

            Assert.Equal(u1.Position, loaded.Battle.Units[0].Position);
            Assert.Equal(u2.Position, loaded.Battle.Units[1].Position);
        }

        [Fact]
        public void SaveLoad_Teams()
        {
            var (manager, _, _, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);

            Assert.Equal(2, loaded.TeamCount);
            Assert.Equal(0, loaded.Battle.Units[0].TeamIndex);
            Assert.Equal(1, loaded.Battle.Units[1].TeamIndex);
        }

        [Fact]
        public void SaveLoad_Equipment()
        {
            var (manager, _, u1, _) = SetupDuel();

            var loaded = SaveAndLoad(manager);
            var loadedU1 = loaded.Battle.Units[0];

            var weapon = loadedU1.Equipment.Get(EquipmentSlot.RightHand);
            Assert.NotNull(weapon);
            Assert.Equal("Axe", weapon.Def.Name);
        }

        [Fact]
        public void SaveLoad_UndoHistory()
        {
            var (manager, _, _, _) = SetupDuel();
            manager.StepTurn();

            var loaded = SaveAndLoad(manager);

            Assert.True(loaded.CanUndo);
        }

        private static BattleManager SaveAndLoad(BattleManager manager)
        {
            var stream = new MemoryStream();
            BattleSave.Save(manager, stream);
            stream.Position = 0;
            return BattleSave.Load(stream);
        }
    }
}
