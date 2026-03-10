using System.IO;

namespace TacticalGame.Grid
{
    public class Loot
    {
        public HexCoord Position { get; internal set; }

        public void WriteTo(BinaryWriter writer)
        {
            Position.WriteTo(writer);
        }

        public static Loot ReadFrom(BinaryReader reader)
        {
            var position = HexCoord.ReadFrom(reader);
            var loot = new Loot();
            loot.Position = position;
            return loot;
        }
    }
}
