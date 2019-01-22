using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.SmartContracts.Networks.Policies
{
    /// <summary>
    /// Smart contract-specific standard transaction definitions.
    /// </summary>
    public class SmartContractsStandardScriptsRegistry : StandardScriptsRegistry
    {
        public const int MaxOpReturnRelay = 40;

        private readonly List<ScriptTemplate> standardTemplates = new List<ScriptTemplate>
        {
            PayToPubkeyHashTemplate.Instance,
            PayToPubkeyTemplate.Instance,
            PayToScriptHashTemplate.Instance,
            PayToMultiSigTemplate.Instance,
            new TxNullDataTemplate(MaxOpReturnRelay),
            PayToWitTemplate.Instance
        };

        public override void RegisterStandardScriptTemplate(ScriptTemplate scriptTemplate)
        {
            if (!this.standardTemplates.Any(template => (template.Type == scriptTemplate.Type)))
            {
                this.standardTemplates.Add(scriptTemplate);
            }
        }

        public override ScriptTemplate GetTemplateFromScriptPubKey(Script script)
        {
            return this.standardTemplates.FirstOrDefault(t => t.CheckScriptPubKey(script));
        }
    }
}