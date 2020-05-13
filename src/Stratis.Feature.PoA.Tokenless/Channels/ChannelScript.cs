using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.Util;
using TracerAttributes;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public static class ChannelScript
    {
        /// <summary>
        /// If the transaction contains a channel creation request <see cref="TxOut"/> then return it.
        /// </summary>
        public static TxOut TryGetChannelCreationRequestTxOut(this Transaction tx)
        {
            TxOut txOut = tx.Outputs.FirstOrDefault(txOut => txOut.ScriptPubKey.IsChannelCreationRequest());
            return txOut;
        }

        /// <summary>
        /// If the transaction contains a channel update request <see cref="TxOut"/> then return it.
        /// </summary>
        public static TxOut TryGetChannelUpdateRequestTxOut(this Transaction tx)
        {
            TxOut txOut = tx.Outputs.FirstOrDefault(txOut => txOut.ScriptPubKey.IsChannelUpdateRequest());
            return txOut;
        }

        [NoTrace]
        public static bool IsChannelCreationRequest(this Script script)
        {
            return ByteUtils.TestFirstByte(script, (byte)ChannelOpCodes.OP_CREATECHANNEL);
        }

        [NoTrace]
        public static bool IsChannelUpdateRequest(this Script script)
        {
            return ByteUtils.TestFirstByte(script, (byte)ChannelOpCodes.OP_UPDATECHANNEL);
        }
    }

    public enum ChannelOpCodes : byte
    {
        OP_CREATECHANNEL = 0xc4,
        OP_UPDATECHANNEL = 0xc5
    }
}
