using System.Collections.Generic;

namespace TacticalGame.Grid
{
    public class CompoundCommand : IBattleCommand
    {
        public Unit Unit { get; }
        public EquipmentDef Weapon { get; }
        public SkillDef Skill { get; }
        public HexCoord TargetHex { get; }
        public IReadOnlyList<BattleEffect> Effects { get; }

        public CompoundCommand(Unit unit, EquipmentDef weapon, SkillDef skill,
            HexCoord targetHex, IReadOnlyList<BattleEffect> effects)
        {
            Unit = unit;
            Weapon = weapon;
            Skill = skill;
            TargetHex = targetHex;
            Effects = effects;
        }

        public bool Execute(BattleState battle)
        {
            bool hasEssential = false;
            foreach (var effect in Effects)
                if (effect.IsEssential) { hasEssential = true; break; }

            if (!hasEssential)
                return false;

            foreach (var effect in Effects)
                effect.Apply(battle);
            return true;
        }

        public void Undo(BattleState battle)
        {
            for (int i = Effects.Count - 1; i >= 0; i--)
                Effects[i].Reverse(battle);
        }

        public override string ToString()
        {
            string result = Unit + ": " + Skill.Name + " -> " + TargetHex;
            foreach (var effect in Effects)
                result += " | " + effect;
            return result;
        }
    }
}
