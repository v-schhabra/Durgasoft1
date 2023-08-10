using System;
using System.Net;

namespace Microsoft.Azure.DevOps.ServiceEndpoints.Sdk.Server.AzureContainerRegistry
{
    internal class AcrRequestEvaluator : IRequestEvaluator
    {
        private const string AcrDomain = "azurecr.io";

        public bool ComplyWith(HttpWebRequest request)
        {
            return request?.RequestUri?.Host?.EndsWith(AcrDomain, StringComparison.OrdinalIgnoreCase) ?? false;
        }
    }
}
