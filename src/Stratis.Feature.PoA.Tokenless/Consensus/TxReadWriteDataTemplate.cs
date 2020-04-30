using System;
using System.Linq;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public class TxReadWriteDataTemplate : ScriptTemplate
    {
        public TxReadWriteDataTemplate(int maxScriptSize)
        {
            this.MaxScriptSizeLimit = maxScriptSize;
        }

        private static readonly TxReadWriteDataTemplate _Instance = new TxReadWriteDataTemplate(MAX_OP_READWRITE_RELAY);

        public static TxReadWriteDataTemplate Instance
        {
            get
            {
                return _Instance;
            }
        }

        public int MaxScriptSizeLimit
        {
            get;
            private set;
        }

        protected override bool FastCheckScriptPubKey(Script scriptPubKey, out bool needMoreCheck)
        {
            byte[] bytes = scriptPubKey.ToBytes(true);

            if (bytes.Length == 0 || bytes[0] != (byte)OpcodeType.OP_READWRITE || bytes.Length > this.MaxScriptSizeLimit)
            {
                needMoreCheck = false;
                return false;
            }

            needMoreCheck = false;

            return true;
        }

        protected override bool CheckScriptPubKeyCore(Script scriptPubKey, Op[] scriptPubKeyOps)
        {
            return true;
        }

        public byte[][] ExtractScriptPubKeyParameters(Script scriptPubKey)
        {
            bool needMoreCheck;
            if (!FastCheckScriptPubKey(scriptPubKey, out needMoreCheck))
                return null;

            return new[] { scriptPubKey.ToBytes().Skip(1).ToArray() };
        }

        protected override bool CheckScriptSigCore(Network network, Script scriptSig, Op[] scriptSigOps, Script scriptPubKey, Op[] scriptPubKeyOps)
        {
            return false;
        }

        public const int MAX_OP_READWRITE_RELAY = 1_024_001; // +1 for OP_READWRITE.

        public Script GenerateScriptPubKey(params byte[][] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (data.Length != 1)
                throw new InvalidOperationException("data");

            byte[] scriptBytes = new[] { (byte)OpcodeType.OP_READWRITE }.Concat(data[0]).ToArray();
            var script = new Script(scriptBytes);
            if (scriptBytes.Length > this.MaxScriptSizeLimit)
                throw new ArgumentOutOfRangeException("data", "Data in OP_READWRITE should have a maximum size of " + this.MaxScriptSizeLimit + " bytes");

            return script;
        }

        public override TxOutType Type
        {
            get
            {
                return TxOutType.TX_READWRITE_DATA;
            }
        }
    }
}
