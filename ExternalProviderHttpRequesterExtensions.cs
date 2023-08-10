using System.Net.Http;
using Microsoft.Azure.DevOps.ServiceEndpoints.Sdk.Server.AzureContainerRegistry;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.VisualStudio.ExternalProviders.Common
{
    public static class ExternalProviderHttpRequesterExtensions
    {
        public static HttpResponseMessage SendRequest(this IExternalProviderHttpRequester requester, HttpRequestMessage message)
        {
            ArgumentUtility.CheckForNull(requester, nameof(requester));

            var result = requester.SendRequest(message, HttpCompletionOption.ResponseContentRead, out var response, out var code, out var _);

            if (!result)
            {
                return new HttpResponseMessage(code);
            }

            if (response == null)
            {
                return new HttpResponseMessage(code);
            }

            return response;
        }
    }
}
