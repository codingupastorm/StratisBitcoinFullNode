using System;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core.ReadWrite
{
    public class ReadWriteSetValidator
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

        public void ApplyReadWriteSet(IStateRepository stateRepository, ReadWriteSetBuilder readWriteSet)
        {
            throw new NotImplementedException("To be used when applying the read write set inside of a block.");
        }
    }
}
