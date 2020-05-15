using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Endorsement;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public class ChannelUpdateTransactionBuilder
    {
        private readonly IChannelRequestSerializer requestSerializer;

        // Use endorsement signer as it gets the key and signs for us. Maybe it could be refactored?
        private readonly IEndorsementSigner transactionSigner;

        public ChannelUpdateTransactionBuilder(IChannelRequestSerializer requestSerializer, IEndorsementSigner transactionSigner)
        {
            this.requestSerializer = requestSerializer;
            this.transactionSigner = transactionSigner;
        }

        /// <summary>
        /// Builds a transaction to update the channel
        /// </summary>
        /// <returns></returns>
        public Transaction Build(ChannelUpdateRequest request)
        {
            var transaction = new Transaction();

            AddRequest(transaction, request);
            Sign(transaction);

            return transaction;
        }

        private void Sign(Transaction transaction)
        {
            this.transactionSigner.Sign(transaction);
        }

        private void AddRequest(Transaction transaction, ChannelUpdateRequest request)
        {
            var payload = this.requestSerializer.Serialize(request);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(payload)));
        }
    }
}
