using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.Util;
using TracerAttributes;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public static class ChannelScript
    {
        /// <summary>
        /// Whether the transaction has any outputs with ScriptPubKeys that are smart contract executions.
        /// </summary>
        public static TxOut TryGetChannelUpdateTxOut(this Transaction tx)
        {
            TxOut channelUpdateTxOut = tx.Outputs.FirstOrDefault(txOut => txOut.ScriptPubKey.IsChannelCreationRequest());
            return channelUpdateTxOut;
        }

        [NoTrace]
        public static bool IsChannelCreationRequest(this Script script)
        {
            return ByteUtils.TestFirstByte(script, (byte)ChannelOpCodes.OP_CREATECHANNEL);
        }
    }

    public enum ChannelOpCodes : byte
    {
        OP_CREATECHANNEL = 0xc4
    }
}
