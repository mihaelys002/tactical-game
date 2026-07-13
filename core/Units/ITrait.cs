namespace TacticalGame.Units
{
    public interface ITrait
    {
        string Id { get; }
        void ModifyEffects(PrototypeCommand cmd, Unit traitOwner);
    }
}
