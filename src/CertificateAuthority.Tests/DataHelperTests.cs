using System.Collections.Generic;
using CertificateAuthority.Models;
using Xunit;

namespace CertificateAuthority.Tests
{
    public class DataHelperTests
    {
        [Fact]
        public void ComputeSha256HashTests()
        {
            Dictionary<string, string> inputToHashDictionary = new Dictionary<string, string>()
            {
                {"65y56h456gergerg", "4e0e7b3189d7cb3a4c6b7bca5844b30d83653eef364af449c6f1e7bf224701fd" },
                {"rg54g4g45g45g", "1eacd796aa227beea7112f5a8524264aec1bb7d72f6fefba9b047c3bd1173c96" },
                {"g456456g45g45", "5bfa0d5fde72297fcb317501c86bb0dde8b28237ce7277043b17b9fd661c801e" },
                {"b65765by56yb56y", "2491635e908aedfbc2be2c71cd3a88572ac0085ccfb68a35967afffcf57b76a1" }
            };

            foreach (KeyValuePair<string, string> pair in inputToHashDictionary)
            {
                string key = pair.Key;
                string hash = pair.Value;

                string actualHash = DataHelper.ComputeSha256Hash(key);

                Assert.Equal(hash, actualHash);
            }
        }

        [Fact]
        public void GetCertificateRequestLinesSuccess_Test()
        {
            string testData = "-----BEGIN CERTIFICATE REQUEST----- MIIE1jCCAr4CAQAwejELMAkGA1UEBhMCcXcxCzAJBgNVBAgMAnF3MQswCQYDVQQH 2IpYg1u4/hr6BAc1Wo9AK5pwUB43Hlxma0EBqVns5spXcWfTO5/9QEkntwIJPi/x 79pteeot4SzcFPkl0hcXix6VnDjI8AFLkN/Dn4SM+vZ5bml+AJiDB7FMulECGJeP 2bBKy8z3ZkSzEFTNwdtU1DAIbutKWTZMZ3A5z8h+riZzkK0dI7w1gjwn -----END CERTIFICATE REQUEST-----";

            List<string> result = DataHelper.GetCertificateRequestLines(testData);

            Assert.Equal("-----BEGIN CERTIFICATE REQUEST-----", result[0]);
            Assert.Equal("MIIE1jCCAr4CAQAwejELMAkGA1UEBhMCcXcxCzAJBgNVBAgMAnF3MQswCQYDVQQH", result[1]);
            Assert.Equal("2IpYg1u4/hr6BAc1Wo9AK5pwUB43Hlxma0EBqVns5spXcWfTO5/9QEkntwIJPi/x", result[2]);
            Assert.Equal("79pteeot4SzcFPkl0hcXix6VnDjI8AFLkN/Dn4SM+vZ5bml+AJiDB7FMulECGJeP", result[3]);
            Assert.Equal("2bBKy8z3ZkSzEFTNwdtU1DAIbutKWTZMZ3A5z8h+riZzkK0dI7w1gjwn", result[4]);
            Assert.Equal("-----END CERTIFICATE REQUEST-----", result[5]);
        }

        [Fact]
        public void GetCertificateRequestLinesInvalidData_Test()
        {
            List<string> invalidData = new List<string>()
            {
                "-----BEGIN g45g54g REQUEST----- MIIE1jQH 2IpYg1u4ox 79pteewn -----END CERTIFICATE REQUEST-----",
                "-----BEGIN CERTIFICATE REQUEST----- MIIE1jQH 2IpYg1u4ox 79pteewn -----END CERTIFICATE REQaaaEST-----",
                "-----BEGIN CERTIFICATE REQUEST----- qasdasd 12  1c43c43 -----END CERTIFICATE REQUEST-----"
            };

            foreach (string invalidDataPiece in invalidData)
            {
                List<string> result = DataHelper.GetCertificateRequestLines(invalidDataPiece);
                Assert.Null(result);
            }
        }

        private class AccessTestData
        {
            public AccessTestData(AccountAccessFlags a, AccountAccessFlags b, bool expectedResult)
            {
                this.A = a;
                this.B = b;
                this.ExpectedResult = expectedResult;
            }

            public AccountAccessFlags A { get; set; }

            public AccountAccessFlags B { get; set; }

            public bool ExpectedResult { get; set; }
        }


        [Fact]
        public void IsCreatorHasGreaterOrEqualAccessTest()
        {
            List<AccessTestData> data = new List<AccessTestData>()
            {
                new AccessTestData(
                    AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.AccessAnyCertificate,
                    AccountAccessFlags.AccessAccountInfo,
                    true),

                new AccessTestData(
                    AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.DeleteAccounts | AccountAccessFlags.CreateAccounts,
                    AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.CreateAccounts,
                    true),

                new AccessTestData(
                    AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.AccessAnyCertificate,
                    AccountAccessFlags.DeleteAccounts,
                    false),

                new AccessTestData(
                    AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.AccessAnyCertificate,
                    AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.DeleteAccounts | AccountAccessFlags.CreateAccounts,
                    false)
            };

            foreach (AccessTestData item in data)
            {
                bool result = DataHelper.IsCreatorHasGreaterOrEqualAccess(item.A, item.B);
                Assert.Equal(item.ExpectedResult, result);
            }
        }
    }
}
