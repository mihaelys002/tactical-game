namespace TacticalGame.Grid
{
    public interface IBattleCommand
    {
        Unit Unit { get; }
        bool Execute(BattleState battle);
        void Undo(BattleState battle);
    }
}
