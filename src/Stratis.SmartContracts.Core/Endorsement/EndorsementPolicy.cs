using System.Text;
using Newtonsoft.Json;
using Stratis.SmartContracts.Core.AccessControl;

namespace Stratis.SmartContracts.Core.Endorsement
{
    public class EndorsementPolicy
    {
        public const int DefaultRequiredSignatures = 2;

        public AccessControlList AccessList { get; set; }

        public int RequiredSignatures { get; set; }

        public EndorsementPolicy()
        {
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
