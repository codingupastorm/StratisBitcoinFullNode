using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Stratis.Features.Api;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification
    {
        [Fact]
        public void CanPerformMultipleParallelCallsToTheSameController()
        {
            // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
            this.stratisApiNode = this.nodeBuilder.CreateTokenlessNode(this.network, 0, this.server).Start();

            this.apiUri = this.stratisApiNode.FullNode.NodeService<ApiSettings>().ApiUri;

            var indexes = new List<int>();
            for (int i = 0; i < 1024; i++)
                indexes.Add(i);

            var success = new bool[indexes.Count];

            var options = new ParallelOptions { MaxDegreeOfParallelism = 16 };
            Parallel.ForEach(indexes, options, ndx =>
            {
                success[ndx] = this.APICallGetsExpectedResult(ndx);
            });

            // Check that none failed.
            Assert.Equal(0, success.Count(s => !s));            
        }

        private bool APICallGetsExpectedResult(int ndx)
        {
            string apiendpoint = $"{GetBlockHashUri}?height=0";

            // One out of two API calls will be invalid.
            bool fail = (ndx & 1) == 0;

            if (fail)
            {
                // Induce failure by passing an invalid api.
                apiendpoint = $"{GetBlockHashUri}xxx";
            }

            HttpResponseMessage response = this.httpClient.GetAsync($"{this.apiUri}{apiendpoint}").GetAwaiter().GetResult();

            // It's only ok to fail when its expected.
            return fail == !response.IsSuccessStatusCode;
        }
    }
}