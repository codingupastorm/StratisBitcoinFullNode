using System.Collections.Generic;
using Stratis.SmartContracts.Core.Endorsement;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    /// <summary>
    /// Identifies a payload that presents itself as being endorsed.
    /// </summary>
    public interface IEndorsedPayload
    {
        EndorsementPolicy EndorsementPolicy { get; }

        List<Endorsement.Endorsement> Endorsements { get; }
    }
}