using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;

namespace Stratis.Bitcoin.IntegrationTests
{
    public static class CoreNodeExtensions
    {
        public static void AppendToConfig(this CoreNode node, string key, string item)
        {
            node.ConfigParameters.Add(key, item);
        }
    }
}