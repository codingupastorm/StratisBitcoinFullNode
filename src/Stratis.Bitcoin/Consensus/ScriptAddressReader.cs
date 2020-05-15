﻿using NBitcoin;
using Stratis.Core.Interfaces;

namespace Stratis.Core.Consensus
{
    /// <inheritdoc cref="IScriptAddressReader"/>
    public class ScriptAddressReader : IScriptAddressReader
    {
        /// <inheritdoc cref="IScriptAddressReader.GetAddressFromScriptPubKey"/>
        public string GetAddressFromScriptPubKey(Network network, Script script)
        {
            ScriptTemplate scriptTemplate = network.StandardScriptsRegistry.GetTemplateFromScriptPubKey(script);

            string destinationAddress = null;

            switch (scriptTemplate?.Type)
            {
                // Pay to PubKey can be found in outputs of staking transactions.
                case TxOutType.TX_PUBKEY:
                    PubKey pubKey = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(script);
                    destinationAddress = pubKey.GetAddress(network).ToString();
                    break;
                // Pay to PubKey hash is the regular, most common type of output.
                case TxOutType.TX_PUBKEYHASH:
                    destinationAddress = script.GetDestinationAddress(network).ToString();
                    break;
                case TxOutType.TX_SCRIPTHASH:
                    destinationAddress = script.GetDestinationAddress(network).ToString();
                    break;
                case TxOutType.TX_NONSTANDARD:
                case TxOutType.TX_MULTISIG:
                case TxOutType.TX_NULL_DATA:
                case TxOutType.TX_SEGWIT:
                    break;
            }

            return destinationAddress;
        }
    }
}