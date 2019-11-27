namespace Stratis.Features.ContractEndorsement.ReadWrite
{
    public class Read
    {
        public string Key { get; }

        public int Version { get; }

        public Read(string key, int version)
        {
            this.Key = key;
            this.Version = version;
        }
    }
}
