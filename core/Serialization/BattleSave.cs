using System.IO;
using Newtonsoft.Json;

namespace TacticalGame.Serialization
{
    public static class BattleSave
    {
        private static readonly JsonSerializerSettings Settings = CreateSettings();

        // Single source of truth for save serialization rules. Callers that
        // save a larger graph around BattleState (e.g. GameSave) reuse these
        // settings and add their own def converters.
        public static JsonSerializerSettings CreateSettings(params JsonConverter[] extraConverters)
        {
            var settings = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                Converters = { new EquipmentDefConverter(), new SkillDefConverter(), new TraitConverter() }
            };
            foreach (var converter in extraConverters)
                settings.Converters.Add(converter);
            return settings;
        }

        public static void Save(BattleState state, Stream stream)
        {
            var json = JsonConvert.SerializeObject(state, Settings);
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(json);
        }

        public static BattleState Load(Stream stream)
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            var json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<BattleState>(json, Settings)!;
        }
    }
}
