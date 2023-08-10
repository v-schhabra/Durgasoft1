using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;

namespace Microsoft.VisualStudio.ExternalProviders.Common
{
    public class GeneralHttpRequester : VssHttpClientBase, IExternalProviderHttpRequester
    {
        public GeneralHttpRequester(HttpMessageHandler httpMessageHandler) : base(baseUrl: null, pipeline: httpMessageHandler, disposeHandler: true)
        {
        }

        public bool SendRequest(HttpRequestMessage message, HttpCompletionOption option, out HttpResponseMessage response, out HttpStatusCode code, out string errorMessage)
        {
            bool success = false;
            try
            {
                Task<HttpResponseMessage> sendAsyncTask = SendAsync(message, option);

                response = sendAsyncTask.SyncResult();
                code = response.StatusCode;
                errorMessage = null;

                success = true;
            }
            catch (VssServiceException e)
            {
                response = null;
                code = LastResponseContext.HttpStatusCode;
                errorMessage = e.InnerException?.Message ?? e.Message;
            }

            return success;
        }

        public async Task<HttpRequestResult> SendRequestAsync(HttpRequestMessage message, HttpCompletionOption option)
        {
            try
            {
                var response = await SendAsync(message, option);

                return new HttpRequestResult()
                {
                    Response = response,
                    Code = response.StatusCode,
                    ErrorMessage = null,
                    Success = true
                };
            }
            catch (VssServiceException e)
            {
                return new HttpRequestResult()
                {
                    Response = null,
                    Code = LastResponseContext.HttpStatusCode,
                    ErrorMessage = e.InnerException?.Message ?? e.Message,
                    Success = false
                };
            }
        }
    }
}
