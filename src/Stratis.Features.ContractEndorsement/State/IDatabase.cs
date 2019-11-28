namespace Stratis.Features.ContractEndorsement.State
{
    public interface IDatabase<K,V>
    {
        V Get(K key);

        void Put(K key, V val);

        // void Delete(K key);
    }
}
