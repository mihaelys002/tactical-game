namespace TacticalGame.Grid
{
    public abstract class BattleEffect
    {
        public Unit Target { get; }
        public bool IsEssential { get; set; }

        protected BattleEffect(Unit target)
        {
            Target = target;
        }

        public abstract void Apply(BattleState battle);
        public abstract void Reverse(BattleState battle);
    }

    public class DamageEffect : BattleEffect
    {
        public Unit Source { get; }
        public int Amount { get; set; }
        public int AppliedArmorDamage { get; private set; }
        public int AppliedHpDamage { get; private set; }

        public DamageEffect(Unit source, Unit target, int amount) : base(target)
        {
            Source = source;
            Amount = amount;
        }

        public override void Apply(BattleState battle)
        {
            (AppliedArmorDamage, AppliedHpDamage) =
                CombatCalculations.SplitDamage(Amount, Target.Stats.CurrentArmor);
            battle.ChangeArmor(Target, -AppliedArmorDamage);
            battle.ChangeHP(Target, -AppliedHpDamage);
        }

        public override void Reverse(BattleState battle)
        {
            battle.ChangeHP(Target, AppliedHpDamage);
            battle.ChangeArmor(Target, AppliedArmorDamage);
        }

        public override string ToString()
        {
            return Target + " -" + AppliedArmorDamage + "arm -" + AppliedHpDamage + "hp" + (Target.IsAlive ? "" : " DEAD");
        }
    }

    public class HealEffect : BattleEffect
    {
        public int Amount { get; set; }
        public int AppliedHeal { get; private set; }

        public HealEffect(Unit target, int amount) : base(target)
        {
            Amount = amount;
        }

        public override void Apply(BattleState battle)
        {
            AppliedHeal = battle.ChangeHP(Target, Amount);
        }

        public override void Reverse(BattleState battle)
        {
            battle.ChangeHP(Target, -AppliedHeal);
        }

        public override string ToString()
        {
            return Target + " +" + AppliedHeal + "hp";
        }
    }

    public class FatigueEffect : BattleEffect
    {
        public int Amount { get; set; }
        public int AppliedFatigue { get; private set; }

        public FatigueEffect(Unit target, int amount) : base(target)
        {
            Amount = amount;
        }

        public override void Apply(BattleState battle)
        {
            AppliedFatigue = battle.ChangeFatigue(Target, Amount);
        }

        public override void Reverse(BattleState battle)
        {
            battle.ChangeFatigue(Target, -AppliedFatigue);
        }

        public override string ToString()
        {
            return "+" + Amount + "fat";
        }
    }

    public class MoraleEffect : BattleEffect
    {
        public int Delta { get; set; }
        public int AppliedDelta { get; private set; }

        public MoraleEffect(Unit target, int delta) : base(target)
        {
            Delta = delta;
        }

        public override void Apply(BattleState battle)
        {
            AppliedDelta = battle.ChangeMorale(Target, Delta);
        }

        public override void Reverse(BattleState battle)
        {
            battle.ChangeMorale(Target, -AppliedDelta);
        }

        public override string ToString()
        {
            return Target + " " + (Delta >= 0 ? "+" : "") + Delta + "mor";
        }
    }
}
