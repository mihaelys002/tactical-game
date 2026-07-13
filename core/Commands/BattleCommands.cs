namespace TacticalGame.Commands
{
    public interface IBattleCommand
    {
        bool Execute(BattleState battle);
        void Undo(BattleState battle);
    }
}
