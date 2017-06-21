using System.IO;
using Newtonsoft.Json;

namespace Common.IO.Utility
{
    public class JsonUtil
    {
        public static void Save(string filepath, object o)
        {
            string json = JsonConvert.SerializeObject(o, Formatting.Indented);
            using (var writer = File.CreateText(filepath))
            {
                writer.Write(json);
            }
        }

        public static T Deserialize<T>(string filepath)
        {
            var json = File.ReadAllText(filepath);
            T x = JsonConvert.DeserializeObject<T>(json);
            return x;
        }
    }
}
