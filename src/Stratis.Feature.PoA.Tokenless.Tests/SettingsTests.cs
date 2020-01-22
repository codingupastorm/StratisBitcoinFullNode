using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Feature.PoA.Tokenless;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tokenless.Tests
{
    public class SettingsTests
    {

        [Fact]
        public void IpRangeDefaultFalse()
        {
            var connectionSettings = new ConnectionManagerSettings(new NodeSettings(new TokenlessNetwork()));
            Assert.False(connectionSettings.IpRangeFiltering);
        }
    }
}
