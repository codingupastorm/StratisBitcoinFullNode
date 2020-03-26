using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.Core.Config
{
    /// <summary>
    /// The configuration to apply to private data dissemination and access.
    /// </summary>
    public class PrivateDataConfig
    {
        /// <summary>
        /// The name of the private data field in the contract.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The policy defining who can access the private data.
        /// </summary>
        public PolicyContainer PolicyContainer { get; set; }

        /// <summary>
        /// The minimum number of peers the private data will be sent to upon endorsement.
        /// The endorsement will fail if dissemination to this number of peers is not achieved.
        /// </summary>
        public int MinimumPeerCount { get; set; }

        /// <summary>
        /// The maximum number of peers the private data will be sent to upon endorsement.
        /// </summary>
        public int MaximumPeerCount { get; set; }

        /// <summary>
        /// The number of blocks after which the collection expires and the data is purged.
        /// </summary>
        public int BlockToLive { get; set; }

        /// <summary>
        /// Specifies whether only members can read the private data.
        /// </summary>
        public bool MemberOnlyRead { get; set; }

        /// <summary>
        /// Specifies whether only members can write the private data.
        /// </summary>
        public bool MemberOnlyWrite { get; set; }
    }
}
