using Stratis.Core.Configuration;
using Stratis.Core.Configuration.Settings;
using Stratis.Feature.PoA.Tokenless.Networks;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
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
