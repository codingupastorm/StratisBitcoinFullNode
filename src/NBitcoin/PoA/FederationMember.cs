using System.Text.Json.Serialization;
using NBitcoin.Serialization;

namespace NBitcoin.PoA
{
    /// <summary>Interface that contains data that defines a federation member.</summary>
    public interface IFederationMember
    {
        /// <summary>Public key of a federation member.</summary>
        [JsonPropertyName("pubkey")]
        [JsonConverter(typeof(JsonPubKeyConverter))]
        PubKey PubKey { get; set; }
    }

    /// <summary>Representation of a federation member on standard PoA network.</summary>
    public class FederationMember : IFederationMember
    {
        public FederationMember()
        {
        }

        public FederationMember(PubKey pubKey)
        {
            this.PubKey = pubKey ?? throw new System.Exception($"{nameof(pubKey)} is null");
        }

        /// <inheritdoc />
        [JsonPropertyName("pubkey")]
        [JsonConverter(typeof(JsonPubKeyConverter))]
        public PubKey PubKey { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(this.PubKey)}:'{this.PubKey.ToHex()}'";
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            var item = obj as FederationMember;
            if (item == null)
                return false;

            return this.PubKey.Equals(item.PubKey);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.PubKey.GetHashCode();
        }

        public static bool operator ==(FederationMember a, FederationMember b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (((object)a == null) || ((object)b == null))
                return false;

            return a.PubKey == b.PubKey;
        }

        public static bool operator !=(FederationMember a, FederationMember b)
        {
            return !(a == b);
        }
    }
}
