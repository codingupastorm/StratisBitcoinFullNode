using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Wallet;

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
        private readonly ITokenlessWalletManager tokenlessWalletManager;

        public EndorsementSigner(Network network, ITokenlessSigner tokenlessSigner, ITokenlessWalletManager tokenlessWalletManager)
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
