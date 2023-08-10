using System.Net.Http;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.TeamFoundation.Framework.Server;
using System;
using Microsoft.Azure.DevOps.ServiceEndpoints.Sdk.Server.AzureContainerRegistry;

namespace Microsoft.VisualStudio.ExternalProviders.Common
{
    public class GeneralHttpRequesterFactory : IExternalProviderHttpRequesterFactory
    {
        private readonly IVssRequestContext _requestContext;
        private readonly string _providerType;
        private readonly ClientProviderHelper.Options _handlerOptions;

        private const string RegistryPath = "/ExternalProviders/GeneralHttpRequesterFactory";

        public string ProviderType => _providerType;

        public GeneralHttpRequesterFactory(IVssRequestContext requestContext, string providerType)
        {
            ArgumentUtility.CheckForNull(requestContext, nameof(requestContext));
            ArgumentUtility.CheckForNull(providerType, nameof(providerType));

            _handlerOptions = CreateHandlerOptions(requestContext);
            _requestContext = requestContext;
            _providerType = providerType;
        }

        private ClientProviderHelper.Options CreateHandlerOptions(IVssRequestContext requestContext)
        {
            var registry = requestContext.GetService<IVssRegistryService>();

            var maxRetryCount = registry.GetValue(requestContext, $"{RegistryPath}/MaxRetryCount", 3);
            var slowRequestThreshold = registry.GetValue(requestContext, $"{RegistryPath}/SlowRequestThresholdSecs", 5);
            var tracePercentage = registry.GetValue<byte>(requestContext, $"{RegistryPath}/TracePercentage", 100);

            return new ClientProviderHelper.Options(maxRetryCount, TimeSpan.FromSeconds(slowRequestThreshold), tracePercentage);
        }

        public IExternalProviderHttpRequester GetRequester(HttpMessageHandler httpMessageHandler)
        {
            var handler = GetMessageHandler(httpMessageHandler);
            return new GeneralHttpRequester(handler);
        }

        private HttpMessageHandler GetMessageHandler(HttpMessageHandler httpMessageHandler)
        {
            var delegatingHandlers = ClientProviderHelper.GetMinimalDelegatingHandlers(
                _requestContext,
                typeof(GeneralHttpRequester),
                _handlerOptions,
                logAs: _providerType);

            return HttpClientFactory.CreatePipeline(httpMessageHandler, delegatingHandlers);
        }

        public void Initialize(object requestContext)
        {
        }
    }
}
