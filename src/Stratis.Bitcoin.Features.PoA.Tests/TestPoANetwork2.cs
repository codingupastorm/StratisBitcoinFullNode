using System.Collections.Generic;
using NBitcoin;
using NBitcoin.PoA;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Features.PoA.Tests
{
    public class TestPoANetwork2 : PoANetwork
    {
        public TestPoANetwork2(List<PubKey> pubKeysOverride = null)
        {
            List<IFederationMember> genesisFederationMembers;

            if (pubKeysOverride != null)
            {
                genesisFederationMembers = new List<IFederationMember>();

                foreach (PubKey key in pubKeysOverride)
                    genesisFederationMembers.Add(new FederationMember(key));
            }
            else
            {
                genesisFederationMembers = new List<IFederationMember>()
                {
                    new FederationMember(new PubKey("02d485fc5ae101c2780ff5e1f0cb92dd907053266f7cf3388eb22c5a4bd266ca2e")),
                    new FederationMember(new PubKey("026ed3f57de73956219b85ef1e91b3b93719e2645f6e804da4b3d1556b44a477ef")),
                    new FederationMember(new PubKey("03895a5ba998896e688b7d46dd424809b0362d61914e1432e265d9539fe0c3cac0")),
                    new FederationMember(new PubKey("020fc3b6ac4128482268d96f3bd911d0d0bf8677b808eaacd39ecdcec3af66db34")),
                    new FederationMember(new PubKey("038d196fc2e60d6dfc533c6a905ba1f9092309762d8ebde4407d209e37a820e462")),
                    new FederationMember(new PubKey("0358711f76435a508d98a9dee2a7e160fed5b214d97e65ea442f8f1265d09e6b55"))
                };
            }

            var baseOptions = this.Consensus.Options as PoAConsensusOptions;

            this.Consensus.Options = new PoAConsensusOptions(
                maxBlockBaseSize: baseOptions.MaxBlockBaseSize,
                maxStandardVersion: baseOptions.MaxStandardVersion,
                maxStandardTxWeight: baseOptions.MaxStandardTxWeight,
                maxBlockSigopsCost: baseOptions.MaxBlockSigopsCost,
                maxStandardTxSigopsCost: baseOptions.MaxStandardTxSigopsCost,
                genesisFederationMembers: genesisFederationMembers,
                targetSpacingSeconds: 60,
                votingEnabled: baseOptions.VotingEnabled,
                autoKickIdleMembers: baseOptions.AutoKickIdleMembers,
                federationMemberMaxIdleTimeSeconds: baseOptions.FederationMemberMaxIdleTimeSeconds,
                enablePermissionedMembership: baseOptions.EnablePermissionedMembership
            );

            this.Consensus.SetPrivatePropertyValue(nameof(this.Consensus.MaxReorgLength), (uint)5);
        }
    }
}
