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

            needMoreCheck = true;

            return true;
        }

        protected override bool CheckScriptPubKeyCore(Script scriptPubKey, Op[] scriptPubKeyOps)
        {
            return scriptPubKeyOps.Skip(1).All(o => o.PushData != null && !o.IsInvalid);
        }

        public byte[][] ExtractScriptPubKeyParameters(Script scriptPubKey)
        {
            bool needMoreCheck;
            if (!FastCheckScriptPubKey(scriptPubKey, out needMoreCheck))
                return null;

            Op[] ops = scriptPubKey.ToOps().ToArray();
            if (!CheckScriptPubKeyCore(scriptPubKey, ops))
                return null;

            return ops.Skip(1).Select(o => o.PushData).ToArray();
        }

        protected override bool CheckScriptSigCore(Network network, Script scriptSig, Op[] scriptSigOps, Script scriptPubKey, Op[] scriptPubKeyOps)
        {
            return false;
        }

        public const int MAX_OP_READWRITE_RELAY = 1_024_003; //! bytes (+1 for OP_RWS, +2 for the pushdata opcodes)

        public Script GenerateScriptPubKey(params byte[][] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            var ops = new Op[data.Length + 1];
            ops[0] = OpcodeType.OP_READWRITE;
            for (int i = 0; i < data.Length; i++)
            {
                ops[1 + i] = Op.GetPushOp(data[i]);
            }
            var script = new Script(ops);
            if (script.ToBytes(true).Length > this.MaxScriptSizeLimit)
                throw new ArgumentOutOfRangeException("data", "Data in OP_RWS should have a maximum size of " + this.MaxScriptSizeLimit + " bytes");
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
