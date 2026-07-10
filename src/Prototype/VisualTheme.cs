using System.Collections.Generic;
using Godot;
using TacticalGame.Grid;

namespace TacticalGame.Prototype
{
    // Visual decisions live here, outside any visual node. Nodes ask; they
    // never own palettes or looks.
    public static class VisualTheme
    {
        private static readonly Dictionary<int, Color> TeamColors = new()
        {
            [0] = new Color(0.2f, 0.4f, 0.9f),
            [1] = new Color(0.9f, 0.2f, 0.2f),
            [2] = new Color(0.2f, 0.8f, 0.3f),
            [3] = new Color(0.9f, 0.7f, 0.1f),
        };

        public static Color TeamColor(int teamIndex)
            => TeamColors.TryGetValue(teamIndex, out var color) ? color : Colors.White;

        public static void SetTeamColor(int teamIndex, Color color)
            => TeamColors[teamIndex] = color;
    }

    // Flyweight look definition: shared, immutable-by-convention data that
    // gives a unit its visual identity. Deliberately not part of core Unit
    // data. Plain data shape — moves to a JSON resource file when real
    // portraits/models arrive.
    public class UnitLookDef
    {
        public string Name { get; set; } = "";
        public Color? BodyColor { get; set; }   // null → speaker's team color
        public string Glyph { get; set; } = ""; // portrait letter until real art
    }

    // The dictionary that associates a unit with its look. Assign at scenario
    // setup; visuals only read.
    public static class UnitLooks
    {
        private static readonly Dictionary<Unit, UnitLookDef> Looks = new();

        public static void Set(Unit unit, UnitLookDef look) => Looks[unit] = look;

        public static UnitLookDef? Of(Unit unit)
            => Looks.TryGetValue(unit, out var look) ? look : null;

        public static void Clear() => Looks.Clear();
    }
}
