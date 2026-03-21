using System;
using Newtonsoft.Json;

namespace TacticalGame.Grid
{
    public class EquipmentDefConverter : JsonConverter<EquipmentDef>
    {
        public override void WriteJson(JsonWriter writer, EquipmentDef? value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.Id);
        }

        public override EquipmentDef? ReadJson(JsonReader reader, Type objectType,
            EquipmentDef? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var id = reader.Value as string;
            return id == null ? null : DefRegistry.GetEquipment(id);
        }
    }

    public class SkillDefConverter : JsonConverter<SkillDef>
    {
        public override void WriteJson(JsonWriter writer, SkillDef? value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.Id);
        }

        public override SkillDef? ReadJson(JsonReader reader, Type objectType,
            SkillDef? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var id = reader.Value as string;
            return id == null ? null : DefRegistry.GetSkill(id);
        }
    }
}
