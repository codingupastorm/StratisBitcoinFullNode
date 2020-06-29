﻿using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using Stratis.Core.Utilities;

namespace Stratis.Core.Controllers
{
    public interface IRestApiClientBase
    {
        /// <summary>Api endpoint URL that client uses to make calls.</summary>
        string EndpointUrl { get; }
    }

    /// <summary>Client for making API calls for methods provided by controllers.</summary>
    public abstract class RestApiClientBase : IRestApiClientBase
    {
        private readonly IHttpClientFactory httpClientFactory;

        private readonly ILogger logger;
        public const int RetryCount = 3;

        /// <summary>Delay between retries.</summary>
        private const int AttemptDelayMs = 1000;

        public const int TimeoutMs = 60_000;

        private readonly RetryPolicy policy;

        /// <inheritdoc />
        public string EndpointUrl { get; }

        private RestApiClientBase(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.policy = Policy.Handle<HttpRequestException>().WaitAndRetryAsync(retryCount: RetryCount, sleepDurationProvider:
                attemptNumber =>
                {
                    // Intervals between new attempts are growing.
                    int delayMs = AttemptDelayMs;

                    if (attemptNumber > 1)
                        delayMs *= attemptNumber;

                    return TimeSpan.FromMilliseconds(delayMs);
                }, onRetry: this.OnRetry);
        }

        public RestApiClientBase(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, Uri uri, string controllerName)
            : this(httpClientFactory, loggerFactory)
        {
            this.EndpointUrl = new Uri(uri, new Uri($"/api/{controllerName}", UriKind.Relative)).ToString();
            this.logger.LogDebug($"{nameof(this.EndpointUrl)} set to {this.EndpointUrl}");
        }

        public RestApiClientBase(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, string url, int port, string controllerName)
            : this(httpClientFactory, loggerFactory)
        {
            this.EndpointUrl = $"{url}:{port}/api/{controllerName}";
            this.logger.LogDebug($"{nameof(this.EndpointUrl)} set to {this.EndpointUrl}");
        }

        protected async Task<HttpResponseMessage> SendPostRequestAsync<TModel>(TModel requestModel, string apiMethodName, CancellationToken cancellation) where TModel : class
        {
            Guard.NotNull(requestModel, nameof(requestModel));

            var publicationUri = new Uri($"{this.EndpointUrl}/{apiMethodName}");

            HttpResponseMessage response = null;

            using (HttpClient client = this.httpClientFactory.CreateClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(TimeoutMs);

                var request = new JsonContent(requestModel);

                try
                {
                    // Retry the following call according to the policy.
                    await this.policy.ExecuteAsync(async token =>
                    {
                        this.logger.LogDebug("Sending request of type '{0}' to Uri '{1}'.",
                            requestModel.GetType().FullName, publicationUri);

                        response = await client.PostAsync(publicationUri, request, cancellation).ConfigureAwait(false);
                        this.logger.LogDebug("Response received: {0}", response);
                    }, cancellation);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogDebug("Operation canceled.");
                    this.logger.LogTrace("(-)[CANCELLED]:null");
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    this.logger.LogError("Target node is not ready to receive API calls at this time on {0}. Reason: {1}.", this.EndpointUrl, ex.Message);
                    this.logger.LogDebug("Failed to send a message. Exception: '{0}'.", ex);
                    return new HttpResponseMessage() { ReasonPhrase = ex.Message, StatusCode = HttpStatusCode.InternalServerError };
                }
            }

            this.logger.LogTrace("(-)[SUCCESS]");
            return response;
        }

        protected async Task<TResponse> SendPostRequestAsync<TModel, TResponse>(TModel requestModel, string apiMethodName, CancellationToken cancellation) where TResponse : class where TModel : class
        {
            HttpResponseMessage response = await this.SendPostRequestAsync(requestModel, apiMethodName, cancellation).ConfigureAwait(false);

            return await this.ParseHttpResponseMessageAsync<TResponse>(response).ConfigureAwait(false);
        }

        public async Task<TResponse> SendGetRequestAsync<TResponse>(string apiMethodName, string arguments = null, CancellationToken cancellation = default) where TResponse : class
        {
            HttpResponseMessage response = await this.SendGetRequestAsync(apiMethodName, arguments, cancellation).ConfigureAwait(false);

            return await this.ParseHttpResponseMessageAsync<TResponse>(response).ConfigureAwait(false);
        }

        private async Task<TResponse> ParseHttpResponseMessageAsync<TResponse>(HttpResponseMessage httpResponse) where TResponse : class
        {
            if (httpResponse == null)
            {
                this.logger.LogDebug("(-)[NO_RESPONSE]:null");
                return null;
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                this.logger.LogDebug("(-)[NOT_SUCCESS_CODE]:null");
                return null;
            }

            if (httpResponse.Content == null)
            {
                this.logger.LogDebug("(-)[NO_CONTENT]:null");
                return null;
            }

            // Parse response.
            string successJson = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (successJson == null)
            {
                this.logger.LogDebug("(-)[JSON_PARSING_FAILURE]:null");
                return null;
            }

            this.logger.LogDebug($"{successJson}");

            TResponse responseModel = JsonConvert.DeserializeObject<TResponse>(successJson);

            this.logger.LogDebug("(-)[SUCCESS]");
            return responseModel;
        }

        protected async Task<HttpResponseMessage> SendGetRequestAsync(string apiMethodName, string arguments = null,
            CancellationToken cancellation = default)
        {
            string url = $"{this.EndpointUrl}/{apiMethodName}";

            if (!string.IsNullOrEmpty(arguments))
            {
                url += "/?" + arguments;
            }

            HttpResponseMessage response = null;

            using (HttpClient client = this.httpClientFactory.CreateClient())
            {
                client.Timeout = TimeSpan.FromMilliseconds(TimeoutMs);

                try
                {
                    // Retry the following call according to the policy.
                    await this.policy.ExecuteAsync(async token =>
                    {
                        this.logger.LogDebug($"Sending request to '{url}'.");

                        response = await client.GetAsync(url, cancellation).ConfigureAwait(false);

                        if (response != null)
                            this.logger.LogDebug($"Response received: {response}");
                    }, cancellation);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogDebug("Operation canceled.");
                    this.logger.LogTrace("(-)[CANCELLED]:null");
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    this.logger.LogError("Target node is not ready to receive API calls at this time ({0})", this.EndpointUrl);
                    this.logger.LogDebug("Failed to send a message to '{0}'. Exception: '{1}'.", url, ex);
                    return new HttpResponseMessage() { ReasonPhrase = ex.Message, StatusCode = HttpStatusCode.InternalServerError };
                }
            }

            this.logger.LogTrace("(-)[SUCCESS]");
            return response;
        }

        protected virtual void OnRetry(Exception exception, TimeSpan delay)
        {
            this.logger.LogDebug("Exception while calling API method: {0}. Retrying...", exception.ToString());
        }
    }

    /// <summary>
    /// Helper class to interpret a string as json.
    /// </summary>
    public class JsonContent : StringContent
    {
        public JsonContent(object obj) :
            base(JsonConvert.SerializeObject(obj), Encoding.UTF8, "application/json")
        {
        }
    }

    /// <summary>
    /// TODO: this should be removed when compatible with full node API, instead, we should use
    /// services.AddHttpClient from Microsoft.Extensions.Http
    /// </summary>
    public class HttpClientFactory : IHttpClientFactory
    {
        /// <inheritdoc />
        public HttpClient CreateClient(string name)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
        }
    }
}
