using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public class PollsRepository : IDisposable
    {
        /// <summary>The database engine.</summary>
        IKeyValueStore KeyValueStore { get; }

        private readonly ILogger logger;

        private readonly RepositorySerializer repositorySerializer;

        internal const string TableName = "DataTable";

        private static readonly byte[] RepositoryHighestIndexKey = new byte[0];

        private int highestPollId;

        public PollsRepository(ILoggerFactory loggerFactory, IPollsKeyValueStore pollsKeyValueStore)
        {
            this.KeyValueStore = pollsKeyValueStore;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            // Load highest index.
            this.highestPollId = -1;

            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                if (transaction.Select<byte[], int>(TableName, RepositoryHighestIndexKey, out int highestPollId))
                    this.highestPollId = highestPollId;
            }

            this.logger.LogDebug("Polls repo initialized with highest id: {0}.", this.highestPollId);
        }

        /// <summary>Provides Id of the most recently added poll.</summary>
        public int GetHighestPollId()
        {
            return this.highestPollId;
        }

        private void SaveHighestPollId(IKeyValueStoreTransaction transaction)
        {
            transaction.Insert<byte[], int>(TableName, RepositoryHighestIndexKey, this.highestPollId);
        }

        /// <summary>Removes polls under provided ids.</summary>
        public void RemovePolls(params int[] ids)
        {
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                foreach (int pollId in ids.Reverse())
                {
                    if (this.highestPollId != pollId)
                        throw new ArgumentException("Only deletion of the most recent item is allowed!");

                    if (transaction.Select(TableName, pollId, out Poll poll))
                        transaction.RemoveKey(TableName, pollId, poll);

                    this.highestPollId--;
                    this.SaveHighestPollId(transaction);
                }

                transaction.Commit();
            }
        }

        /// <summary>Adds new poll.</summary>
        public void AddPolls(params Poll[] polls)
        {
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                foreach (Poll pollToAdd in polls)
                {
                    if (pollToAdd.Id != this.highestPollId + 1)
                        throw new ArgumentException("Id is incorrect. Gaps are not allowed.");

                    transaction.Insert<int, Poll>(TableName, pollToAdd.Id, pollToAdd);

                    this.highestPollId++;
                    this.SaveHighestPollId(transaction);
                }

                transaction.Commit();
            }
        }

        /// <summary>Updates existing poll.</summary>
        public void UpdatePoll(Poll poll)
        {
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                if (!transaction.Exists<int>(TableName, poll.Id))
                    throw new ArgumentException("Value doesn't exist!");

                transaction.Insert<int, Poll>(TableName, poll.Id, poll);

                transaction.Commit();
            }
        }

        /// <summary>Loads polls under provided keys from the database.</summary>
        public List<Poll> GetPolls(params int[] ids)
        {
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                var polls = new List<Poll>(ids.Length);

                foreach (int id in ids)
                {
                    if (!transaction.Select<int, Poll>(TableName, id, out Poll poll))
                        throw new ArgumentException("Value under provided key doesn't exist!");

                    polls.Add(poll);
                }

                return polls;
            }
        }

        /// <summary>Loads all polls from the database.</summary>
        public List<Poll> GetAllPolls()
        {
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                var polls = new List<Poll>(this.highestPollId + 1);

                for (int i = 0; i < this.highestPollId + 1; i++)
                {
                    if (!transaction.Select<int, Poll>(TableName, i, out Poll poll))
                        throw new ArgumentException("Value under provided key doesn't exist!");

                    polls.Add(poll);
                }

                return polls;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
