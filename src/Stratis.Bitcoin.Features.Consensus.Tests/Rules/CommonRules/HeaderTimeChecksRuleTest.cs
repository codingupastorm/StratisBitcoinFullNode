using NBitcoin;
using Stratis.Core.Consensus;
using Stratis.Core.Consensus.Rules;
using Stratis.Core.Networks;
using Stratis.Core.Utilities;
using Stratis.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Features.Consensus.Tests.Rules.CommonRules
{
    public class HeaderTimeChecksRuleTest
    {
        private readonly Network network;

        public HeaderTimeChecksRuleTest()
        {
            this.network = new BitcoinRegTest();
        }

        [Fact]
        public void ChecBlockPreviousTimestamp_ValidationFail()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            var rule = testContext.CreateRule<HeaderTimeChecksRule>();

            RuleContext context = new PowRuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.BlockToValidate = TestRulesContextFactory.MineBlock(this.network, testContext.ChainIndexer);
            context.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(context.ValidationContext.BlockToValidate.Header, context.ValidationContext.BlockToValidate.Header.GetHash(), testContext.ChainIndexer.Tip);
            context.Time = DateTimeProvider.Default.GetTimeOffset();

            // increment the bits.
            context.ValidationContext.BlockToValidate.Header.BlockTime = testContext.ChainIndexer.Tip.Header.BlockTime.AddSeconds(-1);

            ConsensusErrorException error = Assert.Throws<ConsensusErrorException>(() => rule.Run(context));
            Assert.Equal(ConsensusErrors.TimeTooOld, error.ConsensusError);
        }

        [Fact]
        public void ChecBlockFutureTimestamp_ValidationFail()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            var rule = testContext.CreateRule<HeaderTimeChecksRule>();

            RuleContext context = new PowRuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.BlockToValidate = TestRulesContextFactory.MineBlock(this.network, testContext.ChainIndexer);
            context.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(context.ValidationContext.BlockToValidate.Header, context.ValidationContext.BlockToValidate.Header.GetHash(), testContext.ChainIndexer.Tip);
            context.Time = DateTimeProvider.Default.GetTimeOffset();

            // increment the bits.
            context.ValidationContext.BlockToValidate.Header.BlockTime = context.Time.AddHours(3);

            ConsensusErrorException error = Assert.Throws<ConsensusErrorException>(() => rule.Run(context));
            Assert.Equal(ConsensusErrors.TimeTooNew, error.ConsensusError);
        }
    }
}
