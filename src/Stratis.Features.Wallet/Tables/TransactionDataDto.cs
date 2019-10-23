namespace Stratis.Features.Wallet.Tables
{
    public class TransactionDataDto
    {
        public int WalletId { get; set; }
        public int AccountIndex { get; set; }
        public int AddressType { get; set; }
        public int AddressIndex { get; set; }
        public byte[] RedeemScript { get; set; }
        public byte[] ScriptPubKey { get; set; }
        public string Address { get; set; }
        public decimal Value { get; set; }
        public long OutputTxTime { get; set; }
        public byte[] OutputTxId { get; set; }
        public int OutputIndex { get; set; }
        public int? OutputBlockHeight { get; set; }
        public byte[] OutputBlockHash { get; set; }
        public int OutputTxIsCoinBase { get; set; }
        public long? SpendTxTime { get; set; }
        public byte[] SpendTxId { get; set; }
        public int? SpendBlockHeight { get; set; }
        public int SpendTxIsCoinBase { get; set; }
        public byte[] SpendBlockHash { get; set; }
        public decimal? SpendTxTotalOut { get; set; }
    }
}
