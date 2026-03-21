namespace TacticalGame.Grid
{
    public interface IBattleCommand
    {
        bool Execute(BattleState battle);
        void Undo(BattleState battle);
    }
}
