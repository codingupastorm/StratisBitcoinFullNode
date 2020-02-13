using System;

namespace Stratis.Bitcoin.Consensus.Rules
{
    public abstract class UtxoRuleContext : RuleContext
    {
        protected UtxoRuleContext()
        {
        }

        protected UtxoRuleContext(ValidationContext validationContext, DateTimeOffset time)
            : base(validationContext, time)
        {
        }

        /// <summary>
        /// The UTXO that are representing the current validated block.
        /// </summary>
        public UnspentOutputSet UnspentOutputSet { get; set; }
    }
}
