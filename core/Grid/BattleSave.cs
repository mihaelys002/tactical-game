using System.IO;
using Newtonsoft.Json;

namespace TacticalGame.Grid
{
    /// <summary>
    /// Serializes/deserializes the full BattleManager (state + undo history).
    /// Uses Newtonsoft.Json constructor parameter matching: on deserialization,
    /// property values from JSON are passed to the constructor by matching
    /// parameter names to property names (case-insensitive).
    ///
    /// Serialization contract:
    ///  - Every readonly or private-set property must have a matching constructor parameter.
    ///  - Public-set properties are restored automatically after construction.
    ///  - Delegates (e.g. PlanAction) cannot be serialized and must be re-supplied after load.
    /// </summary>
    public static class BattleSave
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            PreserveReferencesHandling = PreserveReferencesHandling.All,
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };

        public static void Save(BattleManager manager, Stream stream)
        {
            var json = JsonConvert.SerializeObject(manager, Settings);
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(json);
        }

        public static BattleManager Load(Stream stream)
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            var json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<BattleManager>(json, Settings)!;
        }
    }
}
