using Newtonsoft.Json;
using Stratis.Core.Controllers.Converters;

namespace Stratis.Features.Wallet.Models
{
    [JsonConverter(typeof(ToStringJsonConverter))]
    public class NewAddressModel
    {
        public string Address { get; set; }

        public NewAddressModel(string address)
        {
            this.Address = address;
        }

        public override string ToString()
        {
            return this.Address;
        }
    }
}
