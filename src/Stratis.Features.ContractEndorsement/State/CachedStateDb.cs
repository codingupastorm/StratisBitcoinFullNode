using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Features.ContractEndorsement.State
{
    /// <summary>
    /// Used during contract execution.
    /// </summary>
    public class CachedStateDb
    {
        /// <summary>
        /// When things are written during execution they go here for retrieval later on.
        /// </summary>
        private readonly Dictionary<CacheKey, StateValue> cache;

        /// <summary>
        /// The actual database.
        /// </summary>
        private readonly IReadableContractStateDb canonicalDb;

        public CachedStateDb(IReadableContractStateDb canonicalDb)
        {
            this.cache = new Dictionary<CacheKey, StateValue>();
            this.canonicalDb = canonicalDb;
        }

        public void SetState(uint160 contractAddress, string key, StateValue data)
        {
            this.cache[new CacheKey(contractAddress, key)] = data;
        }

        public StateValue GetState(uint160 contractAddress, string key)
        {
            var cacheKey = new CacheKey(contractAddress, key);

            if (this.cache.ContainsKey(cacheKey))
                return this.cache[cacheKey];

            return this.canonicalDb.GetState(contractAddress, key);
        }
    }
}
