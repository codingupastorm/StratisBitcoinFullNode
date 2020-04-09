using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.KeyStore;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementSigner
    {
        void Sign(Transaction transaction);
    }

    public class EndorsementSigner : IEndorsementSigner
    {
        private readonly Network network;
        private readonly ITokenlessSigner tokenlessSigner;
        private readonly ITokenlessKeyStoreManager tokenlessWalletManager;

        public EndorsementSigner(Network network, ITokenlessSigner tokenlessSigner, ITokenlessKeyStoreManager tokenlessWalletManager)
        {
            this.network = network;
            this.tokenlessSigner = tokenlessSigner;
            this.tokenlessWalletManager = tokenlessWalletManager;
        }

        public void Sign(Transaction transaction)
        {
            Key key = this.tokenlessWalletManager.LoadTransactionSigningKey();

            this.tokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));
        }
    }
}
