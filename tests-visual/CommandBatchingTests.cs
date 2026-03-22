using System.Collections.Generic;
using System.Linq;
using TacticalGame.Grid;
using TacticalGame.Grid.Skills;
using TacticalGame.Prototype;
using Xunit;

namespace TacticalGame.Visual.Tests
{
    public class CommandBatchingTests
    {
        // ── Helpers ───────────────────────────────────────────────────────

        private static Unit U(string name = "U") => TestHelpers.MakeUnit(name);

        private static MoveCommand Move(Unit unit, HexCoord from, HexCoord to)
            => new(unit, from, to);

        private static CompoundCommand Attack(Unit attacker, Unit target, HexCoord targetHex)
        {
            var dmg = new DamageEffect(attacker, target, 10) { IsEssential = true };
            return new CompoundCommand(attacker, TestHelpers.SampleWeapons.Axe,
                new ChopSkill(), targetHex, new List<BattleEffect> { dmg });
        }

        private static List<IBattleCommand> Commands(params IBattleCommand[] cmds)
            => cmds.ToList();

        // ── CommandBatcher ────────────────────────────────────────────────

        [Fact]
        public void EmptyList_ReturnsNoBatches()
        {
            var batches = CommandBatcher.Batch(new List<IBattleCommand>());
            Assert.Empty(batches);
        }

        [Fact]
        public void SingleCommand_ReturnsSingleBatch()
        {
            var a = U("A");
            var cmds = Commands(Move(a, new HexCoord(0, 0), new HexCoord(1, 0)));

            var batches = CommandBatcher.Batch(cmds);

            Assert.Single(batches);
            Assert.Single(batches[0]);
        }

        [Fact]
        public void NoConflicts_AllInOneBatch()
        {
            var a = U("A");
            var b = U("B");
            var cmds = Commands(
                Move(a, new HexCoord(0, 0), new HexCoord(1, 0)),
                Move(b, new HexCoord(5, 5), new HexCoord(6, 5))
            );

            var batches = CommandBatcher.Batch(cmds);

            Assert.Single(batches);
            Assert.Equal(2, batches[0].Count);
        }

        [Fact]
        public void AllConflict_EachInOwnBatch()
        {
            var a = U("A");
            var cmds = Commands(
                Move(a, new HexCoord(0, 0), new HexCoord(1, 0)),
                Move(a, new HexCoord(1, 0), new HexCoord(2, 0)),
                Move(a, new HexCoord(2, 0), new HexCoord(3, 0))
            );

            var batches = CommandBatcher.Batch(cmds);

            Assert.Equal(3, batches.Count);
            Assert.Single(batches[0]);
            Assert.Single(batches[1]);
            Assert.Single(batches[2]);
        }

        // ── User scenario 1: move before attack (same cell) ──────────────

        [Fact]
        public void MoveIntoCell_ThenAttackCell_MovePlaysFirst()
        {
            var a = U("A");
            var b = U("B");
            var x = new HexCoord(1, 0);

            var cmds = Commands(
                Move(a, new HexCoord(0, 0), x),  // A walks into X
                Attack(b, a, x)                    // B attacks X to hit A
            );

            var batches = CommandBatcher.Batch(cmds);

            Assert.Equal(2, batches.Count);
            Assert.IsType<MoveCommand>(batches[0][0]);
            Assert.IsType<CompoundCommand>(batches[1][0]);
        }

        // ── User scenario 2: attack kills, then move into vacated cell ───

        [Fact]
        public void AttackCell_ThenMoveInto_AttackPlaysFirst()
        {
            var c = U("C");
            var d = U("D");
            var e = U("E");
            var y = new HexCoord(2, 0);

            var cmds = Commands(
                Attack(c, d, y),                   // C attacks D at Y (kills it)
                Move(e, new HexCoord(3, 0), y)     // E walks into Y (now empty)
            );

            var batches = CommandBatcher.Batch(cmds);

            Assert.Equal(2, batches.Count);
            Assert.IsType<CompoundCommand>(batches[0][0]);
            Assert.IsType<MoveCommand>(batches[1][0]);
        }

        // ── User scenario 3: independent actions play simultaneously ─────

        [Fact]
        public void IndependentMoveAndAttack_SameBatch()
        {
            var battle = TestHelpers.MakeBattle();
            var k = U("K");
            var l = U("L");
            var m = U("M");
            battle.PlaceUnit(k, new HexCoord(-2, 0));
            battle.PlaceUnit(l, new HexCoord(2, -2));
            battle.PlaceUnit(m, new HexCoord(3, -3));

            var cmds = Commands(
                Move(k, new HexCoord(-2, 0), new HexCoord(-3, 0)),  // K moves
                Attack(l, m, new HexCoord(3, -3))                    // L attacks M far away
            );

            var batches = CommandBatcher.Batch(cmds);

            Assert.Single(batches);
            Assert.Equal(2, batches[0].Count);
        }

        // ── Mixed: some parallel, some sequential ────────────────────────

        [Fact]
        public void MixedDependencies_CorrectBatching()
        {
            var battle = TestHelpers.MakeBattle(12);
            var a = U("A");
            var b = U("B");
            var c = U("C");
            var d = U("D");
            var x = new HexCoord(1, 0);
            battle.PlaceUnit(a, new HexCoord(0, 0));
            battle.PlaceUnit(b, new HexCoord(-2, 0));
            battle.PlaceUnit(c, new HexCoord(-3, -3));
            battle.PlaceUnit(d, new HexCoord(-3, 0));

            var cmds = Commands(
                Move(a, new HexCoord(0, 0), x),            // 0: A moves to X
                Attack(b, a, x),                            // 1: B attacks A at X (depends on 0)
                Attack(c, d, new HexCoord(-3, 0))           // 2: C attacks D (independent)
            );

            var batches = CommandBatcher.Batch(cmds);

            Assert.Equal(2, batches.Count);
            Assert.Equal(2, batches[0].Count);  // cmd 0 + cmd 2
            Assert.Single(batches[1]);           // cmd 1
        }

        // ── CommandConflictResolver ───────────────────────────────────────

        [Fact]
        public void SharedUnit_IsConflict()
        {
            var a = U("A");
            var cmd1 = Move(a, new HexCoord(0, 0), new HexCoord(1, 0));
            var cmd2 = Move(a, new HexCoord(1, 0), new HexCoord(2, 0));

            Assert.True(CommandConflictResolver.HasConflict(cmd1, cmd2));
        }

        [Fact]
        public void SharedCell_IsConflict()
        {
            var a = U("A");
            var b = U("B");
            var shared = new HexCoord(1, 0);

            var cmd1 = Move(a, new HexCoord(0, 0), shared);
            var cmd2 = Move(b, new HexCoord(2, 0), shared);

            Assert.True(CommandConflictResolver.HasConflict(cmd1, cmd2));
        }

        [Fact]
        public void NoOverlap_NoConflict()
        {
            var a = U("A");
            var b = U("B");

            var cmd1 = Move(a, new HexCoord(0, 0), new HexCoord(1, 0));
            var cmd2 = Move(b, new HexCoord(5, 5), new HexCoord(6, 5));

            Assert.False(CommandConflictResolver.HasConflict(cmd1, cmd2));
        }

        [Fact]
        public void AttackTargetUnit_ConflictsWithMoveOfThatUnit()
        {
            var a = U("A");
            var b = U("B");

            var move = Move(a, new HexCoord(0, 0), new HexCoord(1, 0));
            var attack = Attack(b, a, new HexCoord(1, 0));

            Assert.True(CommandConflictResolver.HasConflict(move, attack));
        }
    }
}
