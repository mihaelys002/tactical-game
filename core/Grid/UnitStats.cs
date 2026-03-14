namespace TacticalGame.Grid
{
    public class UnitStats
    {
        public int Attack { get; internal set; }
        public int Defense { get; internal set; }
        public int Resolve { get; internal set; }

        public int MaxHP { get; internal set; }
        public int CurrentHP { get; internal set; }
        public int MaxArmor { get; internal set; }
        public int CurrentArmor { get; internal set; }

        public int MaxFatigue { get; internal set; }
        public int CurrentFatigue { get; internal set; }

        public int Morale { get; internal set; }

        public UnitStats(
            int attack, int defense, int resolve,
            int maxHP, int currentHP,
            int maxArmor, int currentArmor,
            int maxFatigue, int currentFatigue,
            int morale)
        {
            Attack = attack;
            Defense = defense;
            Resolve = resolve;
            MaxHP = maxHP;
            CurrentHP = currentHP;
            MaxArmor = maxArmor;
            CurrentArmor = currentArmor;
            MaxFatigue = maxFatigue;
            CurrentFatigue = currentFatigue;
            Morale = morale;
        }

        public static UnitStats Fresh(
            int attack, int defense, int resolve,
            int maxHP, int maxArmor, int maxFatigue,
            int morale)
        {
            return new UnitStats(
                attack, defense, resolve,
                maxHP, maxHP,
                maxArmor, maxArmor,
                maxFatigue, 0,
                morale);
        }

    }
}
