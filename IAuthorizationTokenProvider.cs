using System;
using System.Net;

namespace Microsoft.Azure.DevOps.ServiceEndpoints.Sdk.Server
{
    public interface IAuthorizationTokenProvider
    {
        string GetToken(HttpWebRequest request, string resourceUrl);

        bool CanProcess(HttpWebRequest request);
    }
}
