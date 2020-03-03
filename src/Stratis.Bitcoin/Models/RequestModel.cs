using Newtonsoft.Json;

namespace Stratis.Bitcoin.Models
{
    public class RequestModel
    {
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
