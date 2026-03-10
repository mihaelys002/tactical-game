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

    }
}
