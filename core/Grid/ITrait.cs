namespace TacticalGame.Grid
{
    public interface ITrait
    {
        string Id { get; }
        void ModifyEffects(PrototypeCommand cmd, Unit owner);
    }
}
