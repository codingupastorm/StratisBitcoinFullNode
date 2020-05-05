using Microsoft.Extensions.DependencyInjection;
using Stratis.Features.SmartContracts.Interfaces;
using Stratis.Features.SmartContracts.PoA.Rules;

namespace Stratis.Features.SmartContracts.PoA
{
    public static class WhitelistedContractExtensions
    {
        /// <summary>
        /// Adds a consensus rule ensuring only contracts with hashes that are on the PoA whitelist are able to be deployed.
        /// The PoA feature must be installed for this to function correctly.
        /// </summary>
        /// <param name="options">The smart contract options.</param>
        /// <returns>The options provided.</returns>
        public static SmartContractOptions UsePoAWhitelistedContracts(this SmartContractOptions options)
        {
            IServiceCollection services = options.Services;

            // These may have been registered by UsePoAMempoolRules already.
            services.AddSingleton<IWhitelistedHashChecker, WhitelistedHashChecker>();
            services.AddSingleton<IContractCodeHashingStrategy, Keccak256CodeHashingStrategy>();

            // Registers an additional contract tx validation consensus rule which checks whether the hash of the contract being deployed is whitelisted.
            services.AddSingleton<IContractTransactionFullValidationRule, AllowedCodeHashLogic>();

            return options;
        }
    }
}