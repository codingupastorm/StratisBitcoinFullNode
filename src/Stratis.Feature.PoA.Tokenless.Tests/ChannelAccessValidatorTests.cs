﻿using System.IO;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.Networks;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class ChannelAccessValidatorTests
    {
        private readonly IChannelAccessValidator channelAccessValidator;

        public ChannelAccessValidatorTests()
        {
            this.channelAccessValidator = new ChannelAccessValidator();
        }

        [Fact]
        public void OnlyOrganisationMembersPermittedOnNetwork()
        {
            var certParser = new X509CertificateParser();
            X509Certificate cert = certParser.ReadCertificate(File.ReadAllBytes("Certificates/cert.crt"));
            string organisation = cert.GetOrganisation();

            var channelNetwork1 = new ChannelNetwork
            {
                Organisation = organisation
            };

            var channelNetwork2 = new ChannelNetwork
            {
                Organisation = "SomeOrganisation2"
            };

            Assert.True(this.channelAccessValidator.ValidateCertificateIsPermittedOnChannel(cert, channelNetwork1));
            Assert.False(this.channelAccessValidator.ValidateCertificateIsPermittedOnChannel(cert, channelNetwork2));
        }
    }
}
