using NBitcoin;
using Stratis.Core.Networks;

namespace Stratis.Bitcoin.Tests.Common
{
    public static class KnownNetworks
    {
        public static Network Main => new BitcoinMain();

        public static Network TestNet => new BitcoinTest();

        public static Network RegTest => new BitcoinRegTest();

        public static Network StratisMain => new StratisMain();

        public static Network StratisTest => new StratisTest();

        public static Network StratisRegTest => new StratisRegTest();
    }
}