using System.Collections.Generic;
using Newtonsoft.Json;

namespace TacticalGame.Grid
{
    [JsonObject(MemberSerialization.Fields)]
    public class CompoundCommand : IBattleCommand
    {
        public Unit Unit { get; }
        public EquipmentDef Weapon { get; }
        public SkillDef Skill { get; }
        public HexCoord TargetHex { get; }
        public List<BattleEffect> Effects { get; }
        private bool HasEssential => Effects.Exists(x => x.IsEssential);


        public CompoundCommand(Unit unit, EquipmentDef weapon, SkillDef skill,
            HexCoord targetHex, List<BattleEffect> effects)
        {
            Unit = unit;
            Weapon = weapon;
            Skill = skill;
            TargetHex = targetHex;
            Effects = effects;
        }

        public bool Execute(BattleState battle)
        {
            if (!HasEssential)
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
