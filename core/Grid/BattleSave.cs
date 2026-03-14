using System.IO;
using Newtonsoft.Json;

namespace TacticalGame.Grid
{
    public static class BattleSave
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            PreserveReferencesHandling = PreserveReferencesHandling.All,
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };

        public static void Save(BattleState battle, Stream stream)
        {
            var json = JsonConvert.SerializeObject(battle, Settings);
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
