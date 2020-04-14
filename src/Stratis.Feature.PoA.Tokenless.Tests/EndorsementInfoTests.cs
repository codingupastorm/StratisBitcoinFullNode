using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsementInfoTests
    {
        [Fact]
        public void New_Endorsement_Has_State_Proposed()
        {
            Assert.Equal(EndorsementState.Proposed, new EndorsementInfo().State);
        }

        [Fact]
        public void test()
        {
            var endorsementPolicy = new EndorsementInfo();

            
        }
    }
}
