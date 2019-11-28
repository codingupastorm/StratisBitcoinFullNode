using NBitcoin;

namespace Stratis.Features.ContractEndorsement.State
{
    public struct CacheKey
    {
        public uint160 ContractAddress { get; set; }
        public string Key { get; set; }

        public CacheKey(uint160 contractAddress, string key)
        {
            this.ContractAddress = contractAddress;
            this.Key = key;
        }
    }
}
