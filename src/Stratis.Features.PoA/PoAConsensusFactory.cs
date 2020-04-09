using System;
using NBitcoin;

namespace Stratis.Features.PoA
{
    public class PoAConsensusFactory : ConsensusFactory
    {
        /// <inheritdoc />
        public override Block CreateBlock()
        {
            return new Block(this.CreateBlockHeader());
        }

        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new PoABlockHeader();
        }

        public virtual IFederationMember DeserializeFederationMember(byte[] serializedBytes)
        {
            try
            {
                var key = new PubKey(serializedBytes);

                IFederationMember federationMember = new FederationMember(key);

                return federationMember;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public virtual byte[] SerializeFederationMember(IFederationMember federationMember)
        {
            return federationMember.PubKey.ToBytes();
        }
    }
}
