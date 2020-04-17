using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementSigner
    {
        void Sign(Transaction transaction);

        byte[] Sign(ProposalResponse response);
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

        /// <summary>
        /// Signs the proposal response using the current wallet private key, and returns the signature as a byte array.
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        public byte[] Sign(ProposalResponse response)
        {
            Key key = this.tokenlessWalletManager.LoadTransactionSigningKey();

            uint256 hash = response.GetHash();

            var ecdsaSignature = key.Sign(hash);

            return ecdsaSignature.ToDER();
        }
    }
}
