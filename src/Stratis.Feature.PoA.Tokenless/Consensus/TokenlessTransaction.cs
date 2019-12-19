using NBitcoin;
using NBitcoin.DataEncoders;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    /// <summary>
    /// Transaction type used on tokenless networks.
    /// Serialization skips version and segwit checks but includes time.
    /// </summary>
    public class TokenlessTransaction : Transaction
    {
        public TokenlessTransaction() { }

        public TokenlessTransaction(string hex)
        {
            this.FromBytes(Encoders.Hex.DecodeData(hex));
        }

        public TokenlessTransaction(byte[] bytes)
        {
            this.FromBytes(bytes);
        }

        /// <summary>
        /// HOW CLEAN IS THIS!
        /// </summary>
        public override void ReadWrite(BitcoinStream stream)
        {
            // Specific to Tokenless.
            stream.ReadWrite(ref this.nTime);

            // Standard Bitcoin serialization.
            stream.ReadWrite<TxInList, TxIn>(ref this.vin);
            this.vin.Transaction = this;
            stream.ReadWrite<TxOutList, TxOut>(ref this.vout);
            this.vout.Transaction = this;
            stream.ReadWriteStruct(ref this.nLockTime);
        }
    }
}
