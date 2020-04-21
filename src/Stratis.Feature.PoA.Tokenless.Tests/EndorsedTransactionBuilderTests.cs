using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ReadWrite;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsedTransactionBuilderTests
    {
        [Fact]
        public void First_Output_Uses_OpReadWrite()
        {
            var builder = new EndorsedTransactionBuilder();

            Transaction tx = builder.Build();

            Assert.True(tx.Outputs[0].ScriptPubKey.IsReadWriteSet());
        }

        [Fact]
        public void Second_Output_Is_Endorsement()
        {
            var builder = new EndorsedTransactionBuilder();

            Transaction tx = builder.Build();

            Assert.True(tx.Outputs.Count > 1);

            var endorsementData = tx.Outputs[1].ScriptPubKey.ToBytes();

            var endorsements =
                JsonConvert.DeserializeObject<List<Endorsement.Endorsement>>(Encoding.UTF8.GetString(endorsementData));

            Assert.NotEmpty(endorsements);
            // TODO assert content of endorsements
        }

        [Fact]
        public void First_Output_Contains_Correct_Data()
        {
            var builder = new EndorsedTransactionBuilder();

            Transaction tx = builder.Build();

            // Expect the data to include the generated RWS, and endorsements
            // First op should be OP_READWRITE, second op should be raw data
            var rwsData = tx.Outputs[0].ScriptPubKey.ToOps()[1].ToBytes();

            var rws = ReadWriteSet.FromJsonEncodedBytes(rwsData);

            Assert.NotNull(rws);
            // TODO assert content of RWS
        }
    }
}
