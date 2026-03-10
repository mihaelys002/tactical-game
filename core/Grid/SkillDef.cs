namespace TacticalGame.Grid
{
    public class SkillDef
    {
        public string Id { get; }
        public string Name { get; }
        public int FatigueCost { get; }
        public int Range { get; }

        public SkillDef(string id, string name, int fatigueCost, int range)
        {
            Id = id;
            Name = name;
            FatigueCost = fatigueCost;
            Range = range;
        }

        public override string ToString() => $"SkillDef({Id})";
    }
}
