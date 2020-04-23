using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Stratis.SmartContracts.Core.Endorsement
{
    public class EndorsementPolicy
    {
        public const int DefaultRequiredSignatures = 2;

        public Organisation Organisation { get; set; }

        public int RequiredSignatures { get; set; }

        public EndorsementPolicy()
        {
        }

        public Dictionary<Organisation, int> ToDictionary()
        {
            // If no org is defined return an empty dictionary.
            if (string.IsNullOrWhiteSpace(this.Organisation))
                return new Dictionary<Organisation, int>();

            return new Dictionary<Organisation, int>
            {
                { this.Organisation, this.RequiredSignatures }
            };
        }

        #region Serialization

        // TODO: We have lots of components in DLT that are being serialized in the easiest way possible. Could be serialized externally

        public static EndorsementPolicy FromJson(string json)
        {
            return JsonConvert.DeserializeObject<EndorsementPolicy>(json);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static EndorsementPolicy FromJsonEncodedBytes(byte[] bytes)
        {
            return FromJson(Encoding.UTF8.GetString(bytes));
        }

        public byte[] ToJsonEncodedBytes()
        {
            return Encoding.UTF8.GetBytes(this.ToJson());
        }
        #endregion
    }
}
