using System.Text;
using Newtonsoft.Json;

namespace Stratis.Feature.PoA.Tokenless.Config
{
    /// <summary>
    /// Serializes private data configurations.
    /// </summary>
    public class PrivateDataConfigSerializer
    {
        public byte[] Serialize(PrivateDataConfig config)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(config));
        }

        public PrivateDataConfig Deserialize(byte[] data)
        {
            try
            {
                var str = Encoding.UTF8.GetString(data);
                return JsonConvert.DeserializeObject<PrivateDataConfig>(str);
            }
            catch
            {
            }

            return null;
        }
    }
}