using System.Net;

namespace Microsoft.Azure.DevOps.ServiceEndpoints.Sdk.Server.AzureContainerRegistry
{
    public interface IRequestEvaluator
    {
        bool ComplyWith(HttpWebRequest request);
    }
}
