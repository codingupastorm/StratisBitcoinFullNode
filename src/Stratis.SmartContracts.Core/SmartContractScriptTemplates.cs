using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts.Core
{
    public class SmartContractCreateTemplate : ScriptTemplate
    {
        private static SmartContractCreateTemplate _Instance;
        public new static SmartContractCreateTemplate Instance
        {
            get
            {
                return _Instance = _Instance ?? new SmartContractCreateTemplate();
            }
        }

        public override TxOutType Type => TxOutType.TX_SMART_CONTRACT;

        public override bool CheckScriptPubKey(Script script)
        {
            return script.IsSmartContractCreate();
        }

        public virtual bool CheckScriptSig(Network network, Script scriptSig, Script scriptPubKey)
        {
            throw new NotImplementedException("Smart contract create scripts are only valid as an output");
        }

        protected override bool CheckScriptPubKeyCore(Script scriptPubKey, Op[] scriptPubKeyOps)
        {
            throw new NotImplementedException("This shouldn't be reached. All of the functionality of this template should be available ");
        }

        protected override bool CheckScriptSigCore(Network network, Script scriptSig, Op[] scriptSigOps, Script scriptPubKey, Op[] scriptPubKeyOps)
        {
            throw new NotImplementedException("Unreachable code.");
        }
    }
}
