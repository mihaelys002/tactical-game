using System.IO;

namespace TacticalGame.Grid
{
    public static class BattleSave
    {
        public static void Save(BattleState battle, BinaryWriter writer)
        {
            // Cells
            var cells = battle.Grid.AllCells;
            writer.Write(battle.Grid.Count);
            foreach (var cell in cells)
                cell.WriteTo(writer);

            // Units
            writer.Write(battle.Units.Count);
            foreach (var unit in battle.Units)
                unit.WriteTo(writer);

            // Loot
            writer.Write(battle.Loot.Count);
            foreach (var loot in battle.Loot)
                loot.WriteTo(writer);
        }

        public static BattleState Load(BinaryReader reader, EquipmentRegistry registry)
        {
            // Cells
            var grid = new HexGrid();
            int cellCount = reader.ReadInt32();
            for (int i = 0; i < cellCount; i++)
            {
                var cell = HexCell.ReadFrom(reader);
                grid.AddCell(cell.Coord, cell.Terrain, cell.Elevation);
            }

            var battle = new BattleState(grid);

            // Units
            int unitCount = reader.ReadInt32();
            for (int i = 0; i < unitCount; i++)
            {
                var unit = Unit.ReadFrom(reader, registry);
                battle.PlaceUnit(unit, unit.Position);
            }

            // Loot
            int lootCount = reader.ReadInt32();
            for (int i = 0; i < lootCount; i++)
            {
                var loot = Loot.ReadFrom(reader);
                battle.PlaceLoot(loot, loot.Position);
            }

            return battle;
        }
    }
}
