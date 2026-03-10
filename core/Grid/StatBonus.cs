using System.IO;

namespace TacticalGame.Grid
{
    public readonly struct StatBonus
    {
        public int Attack { get; }
        public int Defense { get; }
        public int Resolve { get; }
        public int MaxHP { get; }
        public int MaxArmor { get; }
        public int MaxFatigue { get; }

        public StatBonus(int attack = 0, int defense = 0, int resolve = 0,
            int maxHP = 0, int maxArmor = 0, int maxFatigue = 0)
        {
            Attack = attack;
            Defense = defense;
            Resolve = resolve;
            MaxHP = maxHP;
            MaxArmor = maxArmor;
            MaxFatigue = maxFatigue;
        }

        public static StatBonus operator +(StatBonus a, StatBonus b) => new(
            a.Attack + b.Attack,
            a.Defense + b.Defense,
            a.Resolve + b.Resolve,
            a.MaxHP + b.MaxHP,
            a.MaxArmor + b.MaxArmor,
            a.MaxFatigue + b.MaxFatigue);

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Attack);
            writer.Write(Defense);
            writer.Write(Resolve);
            writer.Write(MaxHP);
            writer.Write(MaxArmor);
            writer.Write(MaxFatigue);
        }

        public static StatBonus ReadFrom(BinaryReader reader)
        {
            return new StatBonus(
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        public override string ToString() =>
            $"StatBonus(Atk:{Attack} Def:{Defense} Res:{Resolve} HP:{MaxHP} Arm:{MaxArmor} Fat:{MaxFatigue})";
    }
}
