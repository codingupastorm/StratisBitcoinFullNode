using SQLite;

namespace Stratis.Features.Wallet.Tables
{
    public class WalletRow
    {
        public string Name { get; set; }
        [PrimaryKey]
        public int WalletId { get; set; }
        public int LastBlockSyncedHeight { get; set; }
        public byte[] LastBlockSyncedHash { get; set; }
        public bool IsExtPubKeyWallet { get; set; }
        public string EncryptedSeed { get; set; }
        public string ChainCode { get; set; }
        public string BlockLocator { get; set; }
        public long CreationTime { get; set; }
    }
}
