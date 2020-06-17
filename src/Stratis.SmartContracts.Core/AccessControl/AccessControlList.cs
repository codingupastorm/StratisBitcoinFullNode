using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Stratis.SmartContracts.Core.AccessControl
{
    /// <summary>
    /// Determines who can access a resource.
    ///
    /// This is a whitelist - if you are named here you have access.
    /// </summary>
    public class AccessControlList
    {
        [JsonPropertyName("organisations")]
        public List<string> Organisations { get; set; }

        [JsonPropertyName("thumbprints")]
        public List<string> Thumbprints { get; set; }

        public AccessControlList()
        {
            this.Organisations = new List<string>();
            this.Thumbprints = new List<string>();
        }

        #region Serialization

        // TODO: Don't be responsible for own serialization.

        public static AccessControlList FromJson(string json)
        {
            return JsonConvert.DeserializeObject<AccessControlList>(json);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        #endregion
    }
}
