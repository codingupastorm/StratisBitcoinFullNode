using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Store;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface IPrivateDataRetriever
    {
        /// <summary>
        /// Lets the node know about some private data that has arrived in a block, so it can decide whether to ask around for it.
        /// It will only ask around for it if it is allowed to access this data.
        /// </summary>
        /// <param name="rws">The ReadWriteSet that came in a transaction.</param>
        /// <returns>Whether this node received the private data as it is from this organisation.</returns>
        Task<bool> WaitForPrivateDataIfRequired(ReadWriteSet rws);

        /// <summary>
        /// Retrieves the specified read-write sets from the transient store and then puts them in the private state db.
        /// </summary>
        void MoveDataFromTransientToPrivateStore(List<uint256> rwsHashes);
    }

    public class PrivateDataRetriever : IPrivateDataRetriever
    {
        private readonly ITransientStore transientStore;
        private readonly IPrivateDataStore privateDataStore;
        private readonly ITokenlessBroadcaster tokenlessBroadcaster;
        private readonly ReadWriteSetPolicyValidator rwsPolicyValidator;

        /// <summary>
        /// How often to go out and request the private data.
        /// </summary>
        private static readonly TimeSpan TimeBetweenQueries = TimeSpans.FiveSeconds;

        /// <summary>
        /// This will hold up the node for a maximum of 25 seconds (5 x 5).
        /// </summary>
        private const int AmountOfTimesToRetry = 5;

        public PrivateDataRetriever(ITransientStore transientStore,
            IPrivateDataStore privateDataStore,
            ITokenlessBroadcaster tokenlessBroadcaster,
            ReadWriteSetPolicyValidator rwsPolicyValidator)
        {
            this.transientStore = transientStore;
            this.privateDataStore = privateDataStore;
            this.tokenlessBroadcaster = tokenlessBroadcaster;
            this.rwsPolicyValidator = rwsPolicyValidator;
        }

        /// <inheritdoc />
        public async Task<bool> WaitForPrivateDataIfRequired(ReadWriteSet readWriteSet)
        {
            if (!this.rwsPolicyValidator.ClientCanAccessPrivateData(readWriteSet))
            {
                return false;
            }

            uint256 id = readWriteSet.GetHash();

            for (int i=0; i< AmountOfTimesToRetry; i++)
            {
                (TransientStorePrivateData Data, uint BlockHeight) item = this.transientStore.Get(id);

                if (item.Data != null)
                {
                    // We have the data inside the transient store - we're good to go!
                    return true;
                }

                await this.tokenlessBroadcaster.BroadcastToWholeOrganisationAsync(new RequestPrivateDataPayload(id))
                    .ConfigureAwait(false);

                Thread.Sleep(TimeBetweenQueries);
            }

            throw new NotImplementedException(
                "Node never received the private data. Need to handle how to progress in this case.");
        }

        public void MoveDataFromTransientToPrivateStore(List<uint256> rwsHashes)
        {
            foreach (var rwsHash in rwsHashes)
            {
                // Get from transient store
                (TransientStorePrivateData Data, uint BlockHeight) item = this.transientStore.Get(rwsHash);
                ReadWriteSet rws = ReadWriteSet.FromJsonEncodedBytes(item.Data.ToBytes());

                // Move to private data store.
                foreach (WriteItem write in rws.Writes)
                {
                    this.privateDataStore.StoreBytes(write.ContractAddress, write.Key, write.Value);
                }

                this.transientStore.Purge(rwsHash);
            }
        }
    }
}
