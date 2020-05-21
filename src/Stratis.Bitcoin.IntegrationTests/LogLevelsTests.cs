using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Flurl;
using Flurl.Http;
using NBitcoin;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Stratis.Core.Controllers.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Core.Utilities.JsonErrors;
using Xunit;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Bitcoin.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using CertificateAuthority.Tests.Common;
using Stratis.SmartContracts.Tests.Common;

namespace Stratis.Bitcoin.IntegrationTests
{
    /// <summary>
    /// This class tests the 'api/node/loglevels' endpoint.
    /// </summary>
    public class LogLevelsTests : IDisposable
    {
        private readonly TokenlessNetwork network;

        private IList<LoggingRule> rules;

        public LogLevelsTests()
        {
            this.network = new TokenlessNetwork();
        }

        public void Dispose()
        {
            LogManager.Configuration.LoggingRules.Clear();
        }

        /// <summary>
        /// Creates a bunch of rules for testing.
        /// </summary>
        private void ConfigLogManager()
        {
            this.rules = LogManager.Configuration.LoggingRules;
            this.rules.Add(new LoggingRule("logging1", LogLevel.Info, new AsyncTargetWrapper(new FileTarget("file1") { FileName = "file1.txt" })));
            this.rules.Add(new LoggingRule("logging2", LogLevel.Fatal, new AsyncTargetWrapper(new FileTarget("file2") { FileName = "file2.txt" })));
            this.rules.Add(new LoggingRule("logging3", LogLevel.Trace, new AsyncTargetWrapper(new FileTarget("file3") { FileName = "file3.txt" })));
        }

        [Fact]
        public async Task ChangeLogLevelWithNonExistantLoggerAsync()
        {
            string ruleName = "non-existant-rule";
            string logLevel = "debug";

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Arrange.
                CoreNode node = nodeBuilder.CreateTokenlessNode(this.network, 0, server).Start();
                this.ConfigLogManager();

                // Act.
                var request = new LogRulesRequest { LogRules = new List<LogRuleRequest> { new LogRuleRequest { RuleName = ruleName, LogLevel = logLevel } } };

                Func<Task> act = async () => await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("node/loglevels")
                    .PutJsonAsync(request)
                    .ReceiveJson<string>();

                // Assert.
                var exception = act.Should().Throw<FlurlHttpException>().Which;
                var response = exception.Call.Response;

                ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
                List<ErrorModel> errors = errorResponse.Errors;

                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                errors.Should().ContainSingle();
                errors.First().Message.Should().Be($"Logger name `{ruleName}` doesn't exist.");
            }
        }

        [Fact]
        public async Task ChangeLogLevelWithNonExistantLogLevelAsync()
        {
            string ruleName = "logging1";
            string logLevel = "xxxxxxxx";

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Arrange.
                CoreNode node = nodeBuilder.CreateTokenlessNode(this.network, 0, server).Start();
                this.ConfigLogManager();

                // Act.
                var request = new LogRulesRequest { LogRules = new List<LogRuleRequest> { new LogRuleRequest { RuleName = ruleName, LogLevel = logLevel } } };

                Func<Task> act = async () => await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("node/loglevels")
                    .PutJsonAsync(request)
                    .ReceiveJson<string>();

                // Assert.
                var exception = act.Should().Throw<FlurlHttpException>().Which;
                var response = exception.Call.Response;

                ErrorResponse errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(await response.Content.ReadAsStringAsync());
                List<ErrorModel> errors = errorResponse.Errors;

                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                errors.Should().ContainSingle();
                errors.First().Message.Should().Be($"Failed converting {logLevel} to a member of NLog.LogLevel.");
            }
        }

        [Fact]
        public async Task ChangeLogLevelToLowerOrdinalAsync()
        {
            string ruleName = "logging2"; // Currently 'fatal'.
            string logLevel = "trace";

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Arrange.
                CoreNode node = nodeBuilder.CreateTokenlessNode(this.network, 0, server).Start();
                this.ConfigLogManager();

                // Act.
                var request = new LogRulesRequest { LogRules = new List<LogRuleRequest> { new LogRuleRequest { RuleName = ruleName, LogLevel = logLevel } } };

                HttpResponseMessage result = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("node/loglevels")
                    .PutJsonAsync(request);

                // Assert.
                result.StatusCode.Should().Be(HttpStatusCode.OK);
                this.rules = LogManager.Configuration.LoggingRules;
                this.rules.Single(r => r.LoggerNamePattern == ruleName).Levels.Should().ContainInOrder(new[] { LogLevel.Trace, LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Fatal });
            }
        }

        [Fact]
        public async Task ChangeLogLevelToHigherOrdinalAsync()
        {
            string ruleName = "logging3"; // Currently 'trace'.
            string logLevel = "info";

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Arrange.
                CoreNode node = nodeBuilder.CreateTokenlessNode(this.network, 0, server).Start();
                this.ConfigLogManager();

                // Act.
                var request = new LogRulesRequest { LogRules = new List<LogRuleRequest> { new LogRuleRequest { RuleName = ruleName, LogLevel = logLevel } } };

                HttpResponseMessage result = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("node/loglevels")
                    .PutJsonAsync(request);

                // Assert.
                result.StatusCode.Should().Be(HttpStatusCode.OK);
                this.rules = LogManager.Configuration.LoggingRules;
                this.rules.Single(r => r.LoggerNamePattern == ruleName).Levels.Should().ContainInOrder(new[] { LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Fatal });
            }
        }

        [Fact]
        public async Task ChangeLogLevelOfMultipleRulesAsync()
        {
            string ruleName1 = "logging1";
            string ruleName2 = "logging2";
            string ruleName3 = "logging3";
            string logLevel = "Error";

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Arrange.
                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server).Start();
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server).Start();

                TestHelper.Connect(node1, node2);

                this.ConfigLogManager();

                // Act.
                var request = new LogRulesRequest
                {
                    LogRules = new List<LogRuleRequest>
                {
                    new LogRuleRequest { RuleName = ruleName1, LogLevel = logLevel },
                    new LogRuleRequest { RuleName = ruleName2, LogLevel = logLevel },
                    new LogRuleRequest { RuleName = ruleName3, LogLevel = logLevel }
                }
                };

                HttpResponseMessage result = await $"http://localhost:{node1.ApiPort}/api"
                    .AppendPathSegment("node/loglevels")
                    .PutJsonAsync(request);

                // Assert.
                result.StatusCode.Should().Be(HttpStatusCode.OK);
                this.rules = LogManager.Configuration.LoggingRules;
                this.rules.Single(r => r.LoggerNamePattern == ruleName1).Levels.Should().ContainInOrder(new[] { LogLevel.Error, LogLevel.Fatal });
                this.rules.Single(r => r.LoggerNamePattern == ruleName2).Levels.Should().ContainInOrder(new[] { LogLevel.Error, LogLevel.Fatal });
                this.rules.Single(r => r.LoggerNamePattern == ruleName3).Levels.Should().ContainInOrder(new[] { LogLevel.Error, LogLevel.Fatal });
            }
        }

        [Fact]
        public async Task GetLogRulesAsync()
        {
            string ruleName1 = "logging1";
            string ruleName2 = "logging2";
            string ruleName3 = "logging3";

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Arrange.
                CoreNode node = nodeBuilder.CreateTokenlessNode(this.network, 0, server).Start();
                this.ConfigLogManager();

                // Act.
                List<LogRuleModel> rules = await $"http://localhost:{node.ApiPort}/api"
                    .AppendPathSegment("node/logrules")
                    .GetJsonAsync<List<LogRuleModel>>();

                // Assert.
                rules.Should().Contain(r => r.RuleName == ruleName1 && r.LogLevel == "Info" && r.Filename.Contains("file1.txt"));
                rules.Should().Contain(r => r.RuleName == ruleName2 && r.LogLevel == "Fatal" && r.Filename.Contains("file2.txt"));
                rules.Should().Contain(r => r.RuleName == ruleName3 && r.LogLevel == "Trace" && r.Filename.Contains("file3.txt"));

                // Addtionally, there is always a node.txt file by default.
                rules.Should().Contain(r => r.Filename.Contains("node.txt"));
            }
        }
    }
}
