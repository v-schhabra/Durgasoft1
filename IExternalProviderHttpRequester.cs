using System;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Services.Common
{
    /// <summary>
    /// Implements calls to a third-party service.
    /// </summary>
    /// <remarks>
    /// This interface is intended to be replaced with a mock / stub implementation for L2 tests
    /// since calls to external services are not allowed for L2s.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IExternalProviderHttpRequester : IDisposable
    {
        Boolean SendRequest(HttpRequestMessage message, HttpCompletionOption option, out HttpResponseMessage response, out HttpStatusCode code, out String errorMessage);
        Task<HttpRequestResult> SendRequestAsync(HttpRequestMessage message, HttpCompletionOption option);
    }

    public class HttpRequestResult
    {
        public Boolean Success { get; set; }
        public HttpResponseMessage Response { get; set; }
        public HttpStatusCode Code { get; set; }
        public String ErrorMessage { get; set; }
    }

    /// <summary>
    /// Creates instances of <c>IExternalProviderHttpRequester</c>.
    /// </summary>
    /// <remarks>
    /// <c>IExternalProviderHttpRequester</c> needs a factory because it is disposable.
    /// Creating and disposing a new instance for each request is the easiest way to manage the lifetime.
    /// It also allows settings such as default headers to be passed into the requester dynamically, on a per-request basis.
    /// </remarks>
    public interface IExternalProviderHttpRequesterFactory
    {
        /// <summary>
        /// The name of the external service.
        /// </summary>
        String ProviderType { get; }

        /// <param name="requestContext">This is intended to be an <c>IVssRequestContext</c>, but we don't have a reference to its assembly here.</param>
        void Initialize(Object requestContext);

        /// <summary>
        /// Creates an instance of <c>IExternalProviderHttpRequester</c> from a message handler.
        /// </summary>
        IExternalProviderHttpRequester GetRequester(HttpMessageHandler httpMessageHandler);
    }
}