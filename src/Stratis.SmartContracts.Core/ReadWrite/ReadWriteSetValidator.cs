using System.Linq;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core.ReadWrite
{
    public interface IReadWriteSetValidator
    {
        bool IsReadWriteSetValid(IStateRepository stateRepository, ReadWriteSet readWriteSet);
        void ApplyReadWriteSet(IStateRepository stateRepository, ReadWriteSet readWriteSet, string currentVersion);
    }

    public class ReadWriteSetValidator : IReadWriteSetValidator
    {
        // TODO: If validating whole blocks of read-write sets, include the staterepository once and check RWSs sequentially.

        public bool IsReadWriteSetValid(IStateRepository stateRepository, ReadWriteSet readWriteSet)
        {
            foreach (ReadItem read in readWriteSet.Reads)
            {
                StorageValue storageValue = stateRepository.GetStorageValue(read.ContractAddress, read.Key);

                // Does the version match?
                if (storageValue == null || storageValue.Version != read.Version)
                    return false;
            }

            return true;
        }

        public void ApplyReadWriteSet(IStateRepository stateRepository, ReadWriteSet readWriteSet, string currentVersion)
        {
            // May not be the right place for this method? 

            IStateRepository workingStateRepository = stateRepository.StartTracking();

            foreach (WriteItem write in readWriteSet.Writes)
            {
                workingStateRepository.SetStorageValue(write.ContractAddress, write.Key, write.Value, currentVersion);
            }

            workingStateRepository.Commit();
        }

        /// <summary>
        /// Validates that this read write set matches the corresponding public read write set.
        /// </summary>
        /// <param name="privateReadWriteSet">The private read write set.</param>
        /// <param name="publicReadWriteSet">The public read write set.</param>
        /// <returns></returns>
        public static bool ValidatePublicReadWriteSet(ReadWriteSet privateReadWriteSet, ReadWriteSet publicReadWriteSet)
        {
            // TODO validate reads?
            foreach (WriteItem write in privateReadWriteSet.Writes.Where(w => !w.IsPrivateData))
            {
                WriteItem publicWrite = publicReadWriteSet.Writes.FirstOrDefault(w => w.Key == write.Key);

                if (publicWrite == null)
                {
                    return false;
                }

                if (!HashHelper.Keccak256(write.Value).SequenceEqual(publicWrite.Value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
