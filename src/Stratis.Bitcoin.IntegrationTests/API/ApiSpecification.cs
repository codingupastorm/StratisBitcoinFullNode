﻿using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification
    {
        [Fact]
        public void Getgeneralinfo_returns_json_starting_with_wallet_path()
        {
            Given(a_proof_of_work_node_with_api_enabled);
            When(calling_general_info);
            Then(general_information_about_the_wallet_and_node_is_returned);
        }

        [Fact]
        public void Block_with_valid_hash_returns_transaction_block()
        {
            Given(two_connected_proof_of_work_nodes_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            And(a_real_transaction);
            And(the_block_with_the_transaction_is_mined);
            When(calling_block);
            Then(the_real_block_should_be_retrieved);
            And(the_block_should_contain_the_transaction);
        }

        [Fact]
        public void Getblockcount_returns_tipheight()
        {
            Given(a_proof_of_work_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
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
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            When(calling_getbestblockhash);
            Then(the_consensus_tip_blockhash_is_returned);
        }

        [Fact]
        public void Getblockhash_returns_blockhash_at_given_height()
        {
            Given(a_proof_of_work_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            When(calling_getblockhash);
            Then(the_blockhash_is_returned);
        }

        [Fact]
        public void Getrawmempool_finds_mempool_transaction()
        {
            Given(two_connected_proof_of_work_nodes_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            And(a_real_transaction);
            When(calling_getrawmempool);
            Then(the_transaction_is_found_in_mempool);
        }

        [Fact]
        public void Getblockheader_returns_blockheader()
        {
            Given(a_proof_of_work_node_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            When(calling_getblockheader);
            Then(the_blockheader_is_returned);
        }

        [Fact]
        public void Getrawtransaction_nonverbose_returns_transaction_hash()
        {
            Given(two_connected_proof_of_work_nodes_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            And(a_real_transaction);
            And(the_block_with_the_transaction_is_mined);
            When(calling_getrawtransaction_nonverbose);
            Then(the_transaction_hash_is_returned);
        }

        [Fact]
        public void Getrawtransaction_verbose_returns_full_transaction()
        {
            Given(two_connected_proof_of_work_nodes_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            And(a_real_transaction);
            And(the_block_with_the_transaction_is_mined);
            When(calling_getrawtransaction_verbose);
            Then(a_verbose_raw_transaction_is_returned);
        }

        [Fact]
        public void Gettxout_nomempool_returns_txouts()
        {
            Given(two_connected_proof_of_work_nodes_with_api_enabled);
            And(a_block_is_mined_creating_spendable_coins);
            And(more_blocks_mined_past_maturity_of_original_block);
            And(a_real_transaction);
            And(the_block_with_the_transaction_is_mined);
            When(calling_gettxout_notmempool);
            Then(the_txout_is_returned);
        }

        [Fact]
        public void Validateaddress_confirms_valid_address()
        {
            Given(a_proof_of_work_node_with_api_enabled);
            When(calling_validateaddress);
            Then(a_valid_address_is_validated);
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
