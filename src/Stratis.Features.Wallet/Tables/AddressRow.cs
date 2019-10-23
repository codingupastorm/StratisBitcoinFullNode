using SQLite;

namespace Stratis.Features.Wallet.Tables
{
    public class AddressRow
    {
        // AddressType constants.
        public const int External = 0;
        public const int Internal = 1;

        [PrimaryKey]
        public int WalletId { get; set; }
        [PrimaryKey]
        public int AccountIndex { get; set; }
        [PrimaryKey]
        public int AddressType { get; set; }
        [PrimaryKey]
        public int AddressIndex { get; set; }
        public byte[] ScriptPubKey { get; set; }
        public byte[] PubKey { get; set; }
        public string Address { get; set; }
    }
}
