using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.Util;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsedTransactionBuilderTests
    {
        private readonly List<SignedProposalResponse> proposalResponses;
        private readonly Key key;
        private readonly ProposalResponse response;
        private readonly TokenlessNetwork network;
        private readonly TokenlessSigner signer;
        private readonly Key transactionSigningKey;

        public EndorsedTransactionBuilderTests()
        {
            this.network = new TokenlessNetwork();
            this.signer = new TokenlessSigner(this.network, new SenderRetriever());

            this.key = new Key();
            this.transactionSigningKey = new Key();

            this.response = new ProposalResponse
            {
                ReadWriteSet = new ReadWriteSet
                {
                    Reads = new List<ReadItem>
                    {
                        new ReadItem {ContractAddress = uint160.One, Key = new byte[] {0xAA}, Version = "1"}
                    },
                    Writes = new List<WriteItem>
                    {
                        new WriteItem
                        {
                            ContractAddress = uint160.One, IsPrivateData = false, Key = new byte[] {0xBB},
                            Value = new byte[] {0xCC}
                        }
                    }
                }
            };

            this.proposalResponses = new List<SignedProposalResponse>
            {
                new SignedProposalResponse
                {
                    ProposalResponse = response,
                    Endorsement = new Endorsement.Endorsement(this.key.Sign(response.GetHash()).ToDER(), this.key.PubKey.ToBytes())
                }
            };
        }

        [Fact]
        public void Proposer_Signs_Transaction()
        {
            var endorsementSigner = new Mock<IEndorsementSigner>();

            var builder = new EndorsedTransactionBuilder(endorsementSigner.Object);

            Transaction tx = builder.Build(this.proposalResponses);

            // We don't need to verify the tx here, the endorsement signer tests take care of that already.
            endorsementSigner.Verify(s => s.Sign(tx), Times.Once);
        }

        [Fact]
        public void First_Output_Uses_OpReadWrite()
        {
            var endorsementSigner = new Mock<IEndorsementSigner>();

            var builder = new EndorsedTransactionBuilder(endorsementSigner.Object);

            Transaction tx = builder.Build(this.proposalResponses);

            Assert.True(tx.Outputs[0].ScriptPubKey.IsReadWriteSet());
        }

        [Fact]
        public void Second_Output_Is_Endorsement()
        {
            var endorsementSigner = new Mock<IEndorsementSigner>();

            var builder = new EndorsedTransactionBuilder(endorsementSigner.Object);

            Transaction tx = builder.Build(this.proposalResponses);

            Assert.True(tx.Outputs.Count > 1);

            var endorsementData = tx.Outputs[1].ScriptPubKey.ToBytes();

            var endorsements =
                JsonConvert.DeserializeObject<List<Endorsement.Endorsement>>(Encoding.UTF8.GetString(endorsementData));

            Assert.NotEmpty(endorsements);
            // TODO assert content of endorsements
            for (var i = 0; i < endorsements.Count; i++)
            {
                var endorsement = endorsements[i];

                Assert.True(endorsement.Signature.SequenceEqual(this.proposalResponses[i].Endorsement.Signature));
                Assert.True(endorsement.PubKey.SequenceEqual(this.proposalResponses[i].Endorsement.PubKey));
            }
        }

        [Fact]
        public void First_Output_Contains_Correct_Data()
        {
            var endorsementSigner = new Mock<IEndorsementSigner>();

            var builder = new EndorsedTransactionBuilder(endorsementSigner.Object);

            Transaction tx = builder.Build(this.proposalResponses);

            // Expect the data to include the generated RWS, and endorsements
            // First op should be OP_READWRITE, second op should be raw data
            var rwsData = tx.Outputs[0].ScriptPubKey.ToOps()[1].ToBytes();

            var rws = ReadWriteSet.FromJsonEncodedBytes(rwsData);

            Assert.NotNull(rws);
            Assert.True(rwsData.SequenceEqual(this.proposalResponses[0].ProposalResponse.ReadWriteSet.ToJsonEncodedBytes()));
        }
    }
}
