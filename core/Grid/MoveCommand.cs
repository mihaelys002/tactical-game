using Newtonsoft.Json;

namespace TacticalGame.Grid
{
    [JsonObject(MemberSerialization.Fields)]
    public class MoveCommand : IBattleCommand
    {
        public Unit Unit { get; }
        public HexCoord From { get; }
        public HexCoord To { get; }

        public MoveCommand(Unit unit, HexCoord from, HexCoord to)
        {
            Unit = unit;
            From = from;
            To = to;
        }

        public bool Execute(BattleState battle)
        {
            if (!battle.Grid.Cells.TryGetValue(To, out var cell) || !cell.IsWalkable)
                return false;

            battle.MoveUnit(Unit, To);
            return true;
        }
        public void Undo(BattleState battle) => battle.MoveUnit(Unit, From);

        public override string ToString() => Unit + ": move " + From + " -> " + To;
    }
}
