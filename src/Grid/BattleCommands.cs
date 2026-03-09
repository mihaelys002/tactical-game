using System;

namespace TacticalGame.Grid
{
    public interface IBattleCommand
    {
        Unit Unit { get; }
        void Execute(BattleState battle);
        void Undo(BattleState battle);
    }

    public class MoveCommand : IBattleCommand
    {
        public Unit Unit { get; }
        public HexCoord From { get; }
        public HexCoord To { get; }

        public MoveCommand(Unit unit, HexCoord from, HexCoord to)
        {
            Unit = unit;
            From = from;
            To = to;
        }

        public void Execute(BattleState battle) => battle.MoveUnit(Unit, To);
        public void Undo(BattleState battle) => battle.MoveUnit(Unit, From);
    }

    public class AttackCommand : IBattleCommand
    {
        public Unit Unit { get; }
        public Unit Target { get; }
        public int ArmorDamage { get; private set; }
        public int HpDamage { get; private set; }

        private readonly int _totalDamage;

        public AttackCommand(Unit attacker, Unit target, int totalDamage)
        {
            Unit = attacker;
            Target = target;
            _totalDamage = totalDamage;
        }

        public void Execute(BattleState battle)
        {
            var stats = Target.Stats;
            int remaining = _totalDamage;

            if (stats.CurrentArmor > 0)
            {
                ArmorDamage = Math.Min(stats.CurrentArmor, remaining);
                remaining -= ArmorDamage;
            }

            HpDamage = remaining;
            stats.CurrentArmor -= ArmorDamage;
            stats.CurrentHP -= HpDamage;
        }

        public void Undo(BattleState battle)
        {
            Target.Stats.CurrentHP += HpDamage;
            Target.Stats.CurrentArmor += ArmorDamage;
        }
    }
}
