using System;
using NBitcoin;

namespace Stratis.Features.ContractEndorsement.State
{
    public class ContractStateDb
    {
        // Open DBreeze connection

        public void SetState(uint160 contractAddress, StateValue data)
        {
            throw new NotImplementedException();
        }

        public StateValue GetState(uint160 contractAddress)
        {
            throw new NotImplementedException();
        }


    }
}
