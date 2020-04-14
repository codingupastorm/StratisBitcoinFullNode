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
        public void MofNPolicyValidator_Validates_No_Signature_Correctly()
        {
            var policy = new Dictionary<Organisation, int>();
            var validator = new MofNPolicyValidator(policy);

            Assert.True(validator.Valid);
        }

        [Fact]
        public void MofNPolicyValidator_Validates_One_Signature_One_Organisation_Correctly()
        {
            var policy = new Dictionary<Organisation, int>();
            var validator = new MofNPolicyValidator(policy);

            Assert.True(validator.Valid);
        }

        [Fact]
        public void MofNPolicyValidator_Validates_Multiple_Signatures_One_Organisation_Correctly()
        {
            var org = (Organisation) "Test";
            var policy = new Dictionary<Organisation, int>
            {
                { org, 2 }
            };

            var validator = new MofNPolicyValidator(policy);

            validator.AddSignature(org, "test");

            Assert.False(validator.Valid);

            validator.AddSignature(org, "test2");

            Assert.True(validator.Valid);
        }

        [Fact]
        public void MofNPolicyValidator_Validates_Multiple_Signatures_Multiple_Organisations_Correctly()
        {
            var org = (Organisation)"Test";
            var org2 = (Organisation)"Test2";
            var policy = new Dictionary<Organisation, int>
            {
                { org, 2 },
                { org2, 3 }
            };

            var validator = new MofNPolicyValidator(policy);

            // Add org 1 signatures
            validator.AddSignature(org, "test");
            validator.AddSignature(org, "test 2");

            Assert.False(validator.Valid);

            // Add org 2 signatures

            validator.AddSignature(org2, "test2 2");
            validator.AddSignature(org2, "test2 3");
            validator.AddSignature(org2, "test2 4");

            Assert.True(validator.Valid);
        }
    }
}
