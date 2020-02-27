using System;
using System.Collections.Generic;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core.ReadWrite
{
    public class ReadWriteSetValidator
    {
        // TODO: If validating whole blocks of read-write sets, include the staterepository once and check RWSs sequentially.

        public bool IsReadWriteSetValid(IStateRepository stateRepository, ReadWriteSet readWriteSet)
        {
            foreach (KeyValuePair<ReadWriteSetKey, string> kvp in readWriteSet.ReadSet)
            {
                StorageValue storageValue = stateRepository.GetStorageValue(kvp.Key.ContractAddress, kvp.Key.Key);

                // Does the version match?
                if (storageValue.Version != kvp.Value)
                    return false;
            }

            return true;
        }

        public void ApplyReadWriteSet(IStateRepository stateRepository, ReadWriteSet readWriteSet)
        {
            throw new NotImplementedException("To be used when applying the read write set inside of a block.");
        }
    }
}
