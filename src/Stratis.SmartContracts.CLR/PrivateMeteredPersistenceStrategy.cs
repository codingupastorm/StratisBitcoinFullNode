using NBitcoin;

namespace Stratis.SmartContracts.CLR
{
    public class PrivateMeteredPersistenceStrategy : IPersistenceStrategy
    {
        public bool ContractExists(uint160 address)
        {
            throw new System.NotImplementedException();
        }

        public byte[] FetchBytes(uint160 address, byte[] key)
        {
            throw new System.NotImplementedException();
        }

        public void StoreBytes(uint160 address, byte[] key, byte[] value)
        {
            throw new System.NotImplementedException();
        }
    }
}