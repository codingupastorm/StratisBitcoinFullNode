using NBitcoin;
using Stratis.Bitcoin.Networks;

namespace Stratis.Bitcoin.Tests.Common
{
    public static class KnownNetworks
    {
        // Ideally we wouldn't need these gross statics, but at the moment comparing new BitcoinMain() ==  new BitcoinMain() returns false,
        // and we do this check between Network objects often in tests.
        
        // TODO: Fix the above. Agree upon using singleton network objects or overriding Equals() or something similar.

        private static BitcoinMain main;
        public static Network Main => main ?? (main = new BitcoinMain());

        private static BitcoinTest testnet;
        public static Network TestNet => testnet ?? (testnet = new BitcoinTest());

        private static BitcoinRegTest regtest;
        public static Network RegTest =>  regtest ?? (regtest = new BitcoinRegTest());

        private static StratisMain stratismain;
        public static Network StratisMain => stratismain ?? (stratismain = new StratisMain());

        private static StratisTest stratistest;
        public static Network StratisTest => stratistest ?? (stratistest = new StratisTest());

        private static StratisRegTest stratisregtest;
        public static Network StratisRegTest => stratisregtest ?? (stratisregtest = new StratisRegTest());
    }
}