namespace Stratis.Features.ContractEndorsement.ReadWrite
{
    public class Write
    {
        public string Key { get; }

        public string Value { get; }

        public bool IsDelete { get; private set; }

        public Write(string key, string value)
        {
            this.Key = key;
            this.Value = value;
        }

        public static Write Delete(string key)
        {
            return new Write(key, null)
            {
                IsDelete = true
            };
        }
    }
}
