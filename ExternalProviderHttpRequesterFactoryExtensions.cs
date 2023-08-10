using Microsoft.Azure.DevOps.ServiceEndpoints.Sdk.Server.AzureContainerRegistry;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.VisualStudio.ExternalProviders.Common
{
    public static class ExternalProviderHttpRequesterFactoryExtensions
    {
        public static IExternalProviderHttpRequester GetRequester(this IExternalProviderHttpRequesterFactory factory)
        {
            return factory.GetRequester(new VssHttpMessageHandler());
        }
    }
}
