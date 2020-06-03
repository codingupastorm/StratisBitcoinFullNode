using System.Collections.Generic;
using System.IO;
using Moq;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.AccessControl;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Networks;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class ChannelAccessValidatorTests
    {
        private readonly IChannelAccessValidator channelAccessValidator;
        private Mock<IChannelRepository> channelRepository;

        public ChannelAccessValidatorTests()
        {
            this.channelRepository = new Mock<IChannelRepository>();
            this.channelAccessValidator = new ChannelAccessValidator(this.channelRepository.Object);
        }

        [Fact]
        public void OnlyOrganisationMembersPermittedOnNetwork()
        {
            var certParser = new X509CertificateParser();
            X509Certificate cert = certParser.ReadCertificate(File.ReadAllBytes("Certificates/cert.crt"));

            string organisation = cert.GetOrganisation();

            var channelNetwork1 = new ChannelNetwork
            {
                Name = "network1",
                InitialAccessList = new AccessControlList
                {
                    Organisations = new List<string>
                    {
                        organisation
                    }
                }
            };


            var channelNetwork2 = new ChannelNetwork
            {
                Name = "network2",
                InitialAccessList = new AccessControlList
                {
                    Organisations = new List<string>
                    {
                        "SomeOrganisation2"
                    }
                }
            };

            this.channelRepository.Setup(r => r.GetChannelDefinition(channelNetwork1.Name)).Returns(
                new ChannelDefinition
                {
                    AccessList = channelNetwork1.InitialAccessList,
                    Name = channelNetwork1.Name
                });

            this.channelRepository.Setup(r => r.GetChannelDefinition(channelNetwork2.Name)).Returns(
                new ChannelDefinition
                {
                    AccessList = channelNetwork2.InitialAccessList,
                    Name = channelNetwork2.Name
                });

            Assert.True(this.channelAccessValidator.ValidateCertificateIsPermittedOnChannel(cert, channelNetwork1));
            Assert.False(this.channelAccessValidator.ValidateCertificateIsPermittedOnChannel(cert, channelNetwork2));
        }
    }
}
