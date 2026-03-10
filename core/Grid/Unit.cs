using System.IO;

namespace TacticalGame.Grid
{
    public class Unit
    {
        public HexCoord Position { get; internal set; }
        public UnitStats Stats { get; }
        public bool IsAlive => Stats.CurrentHP > 0;

        public Unit(UnitStats stats)
        {
            Stats = stats;
        }

        public void WriteTo(BinaryWriter writer)
        {
            Position.WriteTo(writer);
            Stats.WriteTo(writer);
        }

        public static Unit ReadFrom(BinaryReader reader)
        {
            var position = HexCoord.ReadFrom(reader);
            var stats = UnitStats.ReadFrom(reader);
            var unit = new Unit(stats);
            unit.Position = position;
            return unit;
        }
    }
}
