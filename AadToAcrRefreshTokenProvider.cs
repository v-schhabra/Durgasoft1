using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.ServiceEndpoints.WebApi;
using Microsoft.VisualStudio.ExternalProviders.Common;
using Newtonsoft.Json.Linq;
using Microsoft.VisualStudio.Services.OAuth;

namespace Microsoft.Azure.DevOps.ServiceEndpoints.Sdk.Server.AzureContainerRegistry
{
    internal class AadToAcrRefreshTokenProvider : IAuthorizationTokenProvider
    {
        private readonly IAuthorizationTokenProvider _aadTokenProvider;
        private readonly ServiceEndpoint _serviceEndpoint;
        private readonly IExternalProviderHttpRequesterFactory _requesterFactory;
        private readonly IRequestEvaluator _acrRequestEvaluator;


        public AadToAcrRefreshTokenProvider(IAuthorizationTokenProvider aadTokenProvider, ServiceEndpoint serviceEndpoint, IExternalProviderHttpRequesterFactory requesterFactory, IRequestEvaluator acrRequestEvaluator)
        {
            ArgumentUtility.CheckForNull(aadTokenProvider, nameof(aadTokenProvider));
            ArgumentUtility.CheckForNull(acrRequestEvaluator, nameof(acrRequestEvaluator));
            ArgumentUtility.CheckForNull(serviceEndpoint, nameof(serviceEndpoint));
            ArgumentUtility.CheckForNull(requesterFactory, nameof(requesterFactory));

            _aadTokenProvider = aadTokenProvider;
            _serviceEndpoint = serviceEndpoint;
            _requesterFactory = requesterFactory;
            _acrRequestEvaluator = acrRequestEvaluator;
        }

        public bool CanProcess(HttpWebRequest request) => _acrRequestEvaluator.ComplyWith(request);

        public string GetToken(HttpWebRequest request, string resourceUrl)
        {
            if (!CanProcess(request))
            {
                throw new ArgumentException(nameof(request));
            }

            if (!_serviceEndpoint.Authorization.Parameters.TryGetValue(EndpointAuthorizationParameters.TenantId, out string tenant))
            {
                return null;
            }

            if (string.IsNullOrEmpty(tenant))
            {
                return null;
            }

            var aadToken = _aadTokenProvider.GetToken(request, resourceUrl);
            if (aadToken == null)
            {
                return null;
            }

            var uri = new Uri(request.RequestUri, "/oauth2/exchange");

            using (var client = _requesterFactory.GetRequester())
            using (var message = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                message.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { VssOAuthConstants.GrantType, VssOAuthConstants.AccessTokenGrantType },
                    { VssOAuthConstants.Service, uri.Host },
                    { VssOAuthConstants.Tenant,  tenant },
                    { VssOAuthConstants.AccessToken, aadToken }
                });

                var response = client.SendRequest(message);
                response.EnsureSuccessStatusCode();

                var content = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                var token = (string)JObject.Parse(content).SelectToken(VssOAuthConstants.RefreshToken);
                return token;
            }
        }
    }
}
