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
            TxOut channelUpdateTxOut = tx.Outputs.FirstOrDefault(txOut => txOut.ScriptPubKey.IsChannelCreationRequest());
            return channelUpdateTxOut;
        }

        /// <summary>
        /// If the transaction contains a channel "add memmber" request <see cref="TxOut"/> then return it.
        /// </summary>
        public static TxOut TryGetChannelAddMemberRequestTxOut(this Transaction tx)
        {
            TxOut channelUpdateTxOut = tx.Outputs.FirstOrDefault(txOut => txOut.ScriptPubKey.IsChannelAddMemberRequest());
            return channelUpdateTxOut;
        }

        [NoTrace]
        public static bool IsChannelCreationRequest(this Script script)
        {
            return ByteUtils.TestFirstByte(script, (byte)ChannelOpCodes.OP_CREATECHANNEL);
        }

        [NoTrace]
        public static bool IsChannelAddMemberRequest(this Script script)
        {
            return ByteUtils.TestFirstByte(script, (byte)ChannelOpCodes.OP_ADDCHANNELMEMBER);
        }
    }

    public enum ChannelOpCodes : byte
    {
        OP_CREATECHANNEL = 0xc4,
        OP_ADDCHANNELMEMBER = 0xc5
    }
}
