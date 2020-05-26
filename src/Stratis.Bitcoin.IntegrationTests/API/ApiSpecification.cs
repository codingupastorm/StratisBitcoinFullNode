using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification
    {
        [Fact]
        public void Getblockcount_returns_tipheight()
        {
            Given(a_proof_of_work_node_with_api_enabled);
            And(a_block_is_mined);
            And(more_blocks_mined_past_maturity_of_original_block);
            When(calling_getblockcount);
            Then(the_blockcount_should_match_consensus_tip_height);
        }

        [Fact]
        public void Getpeerinfo_returns_connected_peer()
        {
            Given(two_connected_proof_of_work_nodes_with_api_enabled);
            When(calling_getpeerinfo);
            Then(a_single_connected_peer_is_returned);
        }

        [Fact]
        public void Getbestblockhash_returns_tip_hash()
        {
            Given(a_proof_of_work_node_with_api_enabled);
            And(a_block_is_mined);
            And(more_blocks_mined_past_maturity_of_original_block);
            When(calling_getbestblockhash);
            Then(the_consensus_tip_blockhash_is_returned);
        }

        [Fact]
        public void Getblockhash_returns_blockhash_at_given_height()
        {
            Given(a_proof_of_work_node_with_api_enabled);
            And(a_block_is_mined);
            When(calling_getblockhash);
            Then(the_blockhash_is_returned);
        }

        [Fact]
        public void Getblockheader_returns_blockheader()
        {
            Given(a_proof_of_work_node_with_api_enabled);
            And(a_block_is_mined);
            When(calling_getblockheader);
            Then(the_blockheader_is_returned);
        }

        [Fact]
        public void Status_returns_status_info()
        {
            Given(a_proof_of_work_node_with_api_enabled);
            When(calling_status);
            Then(status_information_is_returned);
        }
    }
}
