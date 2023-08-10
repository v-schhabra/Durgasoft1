using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Web;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.TeamFoundation.Framework.Server
{
    #region Public Types 

    public enum AccessIntent
    {
        NotSpecified = 0,
        Read = 1,
        ReadLatest = 2,
        Write = 3,
    }

    public enum RequestContextType
    {
        ServicingContext,
        SystemContext,
        UserContext
    }
    public static class ClientProviderHelper
    {
        private const String c_tooManyRequestHandlerFeatureName = "VisualStudio.FrameworkService.TooManyRequestsHandler";

        public static List<DelegatingHandler> GetMinimalDelegatingHandlers(
            IVssRequestContext requestContext,
            Type requestedType,
            Options options,
            String logAs,
            VssHttpClientOptions httpClientOptions = null,
            bool useAcceptLanguageHandler = true)
        {
            // Initialize the list so that it won't need to grow
            List<DelegatingHandler> delegatingHandlers = new List<DelegatingHandler>(8);

            //always add this one first, to pass the requestContext through
            delegatingHandlers.Add(new VssRequestContextCaptureHandler(requestContext));

            if (useAcceptLanguageHandler)
            {
                // Add a handler for setting the Accept-Language header
                delegatingHandlers.Add(new AcceptLanguageHandler());
            }

            delegatingHandlers.Add(new VssTracingHttpRetryMessageHandler(options.MaxRetryCount, requestedType.Name));

            // Put trace handler inside the retry to accurately measure retry volume and request duration when timeouts occur
            delegatingHandlers.Add(
                new ClientTraceHandler(requestedType,
                                       "Framework" + logAs,
                                       logAs,
                                       options.SlowRequestThreshold,
                                       options.TracePercentage,
                                       GetSensitiveHeadersOfClientType(requestedType)));

            if (requestContext.ExecutionEnvironment.IsHostedDeployment)
            {
                // Circuit breaker handler is within the retry handler so each failed retry can be considered for breaker volume.
                delegatingHandlers.Add(ThrottlingMessageHandler.Create(requestContext, requestedType));

                // Handle read consistency level header
                if (httpClientOptions?.ReadConsistencyLevel != null)
                {
                    delegatingHandlers.Add(new ReadConsistencyLevelHandler(httpClientOptions.ReadConsistencyLevel));
                }
            }

            return delegatingHandlers;
        }

        public static List<DelegatingHandler> GetDelegatingHandlers(
            IVssRequestContext requestContext,
            Type requestedType,
            Uri baseUri,
            Options options,
            String logAs,
            IEnumerable<DelegatingHandler> customDelegatingHandlers = null,
            VssHttpClientOptions httpClientOptions = null)
        {
            List<DelegatingHandler> delegatingHandlers = new List<DelegatingHandler>();

            //always add this one first, to pass the requestContext through
            delegatingHandlers.Add(new VssRequestContextCaptureHandler(requestContext));

            if (customDelegatingHandlers != null && customDelegatingHandlers.Any() == true)
            {
                delegatingHandlers.AddRange(customDelegatingHandlers);
            }

            if (requestContext.ExecutionEnvironment.IsHostedDeployment &&
                requestContext.IsFeatureEnabled(FrameworkServerConstants.HandlerConfigureAwaitFeatureFlag) &&
                requestContext.IsFeatureEnabled(c_tooManyRequestHandlerFeatureName))
            {
                // Add the 429 handler as early as possible to short-circuit any requests
                delegatingHandlers.Add(new RetryAfterHandler(requestContext, requestedType.Name, baseUri.Host));
            }

            delegatingHandlers.Add(new TfsImpersonationMessageHandler());

            delegatingHandlers.Add(new TfsSubjectDescriptorImpersonationMessageHandler(requestContext));

            // Add a handler for setting the Accept-Language header
            delegatingHandlers.Add(new AcceptLanguageHandler());

            // Forward client IP along with requests to prevent AFD DoS Defender from triggering
            delegatingHandlers.Add(new AfdClientIpHandler(requestContext));

            delegatingHandlers.Add(new VssTracingHttpRetryMessageHandler(options.MaxRetryCount, requestedType.Name));

            // Put trace handler inside the retry to accurately measure retry volume and request duration when timeouts occur
            delegatingHandlers.Add(
                new ClientTraceHandler(requestedType,
                                       "Framework" + logAs,
                                       logAs,
                                       options.SlowRequestThreshold,
                                       options.TracePercentage,
                                       GetSensitiveHeadersOfClientType(requestedType)));

            if (requestContext.ExecutionEnvironment.IsHostedDeployment)
            {
                delegatingHandlers.Add(S2SUnauthorizedHandler.Create(requestContext));
                delegatingHandlers.Add(new VssPriorityHandler());

                // Circuit breaker handler is within the retry handler so each failed retry can be considered for breaker volume.
                delegatingHandlers.Add(ThrottlingMessageHandler.Create(requestContext, requestedType));

                // Fault injection handler is within the other handlers to ensure that network fault injection will trigger realistic circuit breaker, timeout, and retry behavior
                delegatingHandlers.Add(new FaultInjectionHandler(requestContext, baseUri.Host));

                // Preserve client access mapping when hopping across the services
                delegatingHandlers.Add(new ClientAccessMappingHandler(requestContext));

                // Handle read consistency level header
                if (httpClientOptions?.ReadConsistencyLevel != null)
                {
                    delegatingHandlers.Add(new ReadConsistencyLevelHandler(httpClientOptions.ReadConsistencyLevel));
                }
            }

            // Loopback handler should be added second to last so that the HttpRequestMessage object is
            // fully built before we create the LoopbackContext based on the HttpRequestMessage
            // If the HttpRequestMessage is modified after the loopback context is created the changes will not apply
            if (requestContext.ExecutionEnvironment.IsOnPremisesDeployment && !requestContext.RootContext.Items.ContainsKey(RequestContextItemsKeys.BypassLoopbackHandler))
            {
                delegatingHandlers.Add(new LoopbackHandler());
            }

            // Always add this handler last
            delegatingHandlers.Add(new ConnectionLockHandler());

            return delegatingHandlers;
        }

        static HashSet<string> GetSensitiveHeadersOfClientType(Type clientType)
        {
            return clientType.GetCustomAttributes(typeof(ClientSensitiveHeaderAttribute), inherit: true)?.Cast<ClientSensitiveHeaderAttribute>().Select(csha => csha.HeaderName).ToHashSet();
        }

        public static IEnumerable<DelegatingHandler> GetCustomHandlersFromType(IVssRequestContext requestContext, Type type, string area, string layer)
        {
            if (type == null)
            {
                return Enumerable.Empty<DelegatingHandler>();
            }

            var list = new List<DelegatingHandler>();

            try
            {
                var throttlingAttr = type.GetCustomAttribute<ReactiveClientToThrottlingAttribute>(true);
                if (throttlingAttr?.ReactToThrottlingHeaders == true)
                {
                    list.Add(new ClientRateLimiterHandler(type));
                }

                var attrs = type.GetCustomAttributes<AddCustomHandlerBaseAttribute>(true);
                foreach (var attribute in attrs)
                {
                    list.Add(attribute.CreateHandler(requestContext));
                }

                return list;
            }
            catch (Exception ex)
            {
                requestContext.Trace(CustomHandlerException, TraceLevel.Error, area, layer, $"Custom Delegating Handlers could not be retrieved from {type} ({nameof(VssHttpClientBase)}). Ex: {ex}");
                return Enumerable.Empty<DelegatingHandler>();
            }
        }

        /// <summary>
        /// Settings for <see cref="GetMinimalDelegatingHandlers"/>
        /// and <see cref="GetDelegatingHandlers"/>
        /// </summary>
        public class Options
        {
            public Options(
                int maxRetryCount,
                TimeSpan slowRequestThreshold,
                byte tracePercentage)
            {
                MaxRetryCount = maxRetryCount;
                SlowRequestThreshold = slowRequestThreshold;
                TracePercentage = tracePercentage;
            }

            public static Options CreateDefault(IVssRequestContext requestContext)
            {
                IVssRequestContext elevatedDeploymentContext = requestContext.To(TeamFoundationHostType.Deployment).Elevate();
                IVssRegistryService registryService = elevatedDeploymentContext.GetService<IVssRegistryService>();

                return new Options(
                    maxRetryCount: registryService.GetValue<int>(
                        elevatedDeploymentContext,
                        "/Service/HttpResourceManagementService/MaxRetryHttpRequest",
                        5),
                    slowRequestThreshold: registryService.GetValue<TimeSpan>(
                        elevatedDeploymentContext,
                        "/Service/HttpResourceManagementService/SlowRequestThreshold",
                        TimeSpan.FromSeconds(5)),
                    tracePercentage: registryService.GetValue<byte>(
                        elevatedDeploymentContext,
                        "/Service/HttpResourceManagementService/TracePercentage",
                        100));
            }

            public int MaxRetryCount { get; }
            public TimeSpan SlowRequestThreshold { get; }
            public byte TracePercentage { get; }
        }
    }

    public interface IVssRequestContext : IDisposable
    {
        #region Instance Properties
        /// <summary>
        /// ActivityId is a unique identifier for *this* request only.
        /// </summary>
        Guid ActivityId { get; }

        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Links the passed in token source to the current token source
        /// and sets the active token source to it. Disposing of the returned value
        /// will set the active token source back to the value prior.
        /// </summary>
        /// <returns></returns>
        IDisposable LinkTokenSource(CancellationTokenSource toLink);

        Int64 ContextId { get; }

        /// <summary>
        /// If this request is part of a chain of requests (S2S calls or Loopbacks)
        /// UniqueIdentifier is the ActivityId of the calling request
        /// </summary>
        Guid UniqueIdentifier { get; }

        /// <summary>
        /// End to End ID - typically generated by the first VSTS Service and sent unchanged across all VSTS services
        /// </summary>
        Guid E2EId { get; }

        /// <summary>
        /// Orchestration ID - correlates traces for a long running orchestration together
        /// </summary>
        String OrchestrationId { get; }

        String UserAgent { get; }

        String DomainUserName { get; }
        String AuthenticatedUserName { get; }

        TeamFoundationExecutionEnvironment ExecutionEnvironment { get; }

       

        Boolean IsSystemContext { get; }

        Boolean IsServicingContext { get; }

        Boolean IsUserContext { get; }

        Boolean IsImpersonating { get; }

        Boolean IsCanceled { get; }

        Boolean IsTracked { get; set; }

        IDictionary<String, Object> Items { get; }

        

       

        TimeSpan RequestTimeout { get; set; }

        int ResponseCode { get; }

        IVssRequestContext RootContext { get; }

        

        String ServiceName { get; set; }

        Exception Status { get; set; }

        IdentityDescriptor UserContext { get; }

        long CPUCycles { get; }

        long AllocatedBytes { get; }

        double TSTUs { get; set; }

      

        /// <summary>
        /// The dataspace the request is scoped to. If set
        /// access to cross dataspace activities will be restricted.
        /// </summary>
        Guid DataspaceIdentifier { get; }
        #endregion

        #region Methods
        void AddDisposableResource(IDisposable disposable);

        [Obsolete("Use CreateUserContext instead.", false)]
        IVssRequestContext CreateImpersonationContext(IdentityDescriptor identity, RequestContextType? newType = null);

        void Cancel(String reason);

        void Cancel(String reason, HttpStatusCode statusCode);

      

        void LinkCancellation(IVssRequestContext childContext);

      
        void LeaveMethod();
        IVssRequestContext Elevate(Boolean throwIfShutdown = true);
      
        #endregion

        #region Sub-object accessors
        //SqlResource component creation, see extensions
       

       

        #endregion

    }

    //this interface extend the request context with properties that
    //pertain specifically to a Web-based request.
    //Implemented on WebRequestContext subclass, from which AspNetRequestContext
    //and LoopbackRequestContext derive.
    public interface IVssWebRequestContext : IVssRequestContext
    {
        String AuthenticationType { get; }
        String Command { get; }
        String HttpMethod { get; }
        //tquinn: Can I kill raw URL?
        String RawUrl { get; }
        String RemotePort { get; }
        String RemoteIPAddress { get; }
        String RemoteComputer { get; }
        Uri RequestUri { get; }
      
        String UniqueAgentIdentifier { get; }

        String RequestPath { get; }
        String RelativePath { get; }
        String RelativeUrl { get; }
        String VirtualPath { get; }
        String WebApplicationPath { get; }

        Boolean GetSessionValue(String sessionKey, out string sessionValue);
        Boolean SetSessionValue(String sessionKey, string sessionValue);

        /// <summary>
        /// PartialResultsReady is called when the command is executing and
        /// the command has determined that enough of the result is ready but
        /// not all of it. This implies that the caller may start accessing
        /// data but there is more data for the response than is currently
        /// available.
        /// </summary>
        void PartialResultsReady();
    }

    #endregion

    #region Internal Types
    internal enum HostRequestType
    {
        Default = 0,
        AspNet = 1,
        Job = 2,
        [Obsolete("Use HostRequestType.Default instead")]
        Task = 3,
        GenericHttp = 4,
        Ssh = 5,
        [Obsolete("Use HostRequestType.Default instead")]
        ServiceBus = 6,
    }

    internal interface ITrackClientConnection
    {
        Boolean IsClientConnected { get; }
    }

    /// <summary>
    /// Internal interface that allows additional control
    /// over the request context for internal consumption only.
    /// </summary>
    internal interface IRequestContextInternal
    {
        Boolean IsRootContext { get; }

      

        Boolean HasRequestTimedOut { get; }

        Thread MethodExecutionThread { get; }

       

        void ResetCancel();
        void CheckCanceled();

        void RemoveDisposableResource(IDisposable resource);
        void DisposeDisposableResources();

        T[] GetDisposableResources<T>();

        void SetAuthenticatedUserName(String authenticatedUserName);
        void SetResponseCode(int responseCode);
        void SetDomainUserName(String domainUserName);

       

        void RemoveLease(String leaseName);

        IdentityValidationStatus IdentityValidationStatus { get; set; }

        void ClearActors();

        void SetDataspaceIdentifier(Guid dataspaceIdentifier);

        void ResetActivityId();

        void SetE2EId(Guid identifier);

        void SetOrchestrationId(String identifier);
    }

    [Flags]
    internal enum IdentityValidationStatus
    {
        None = 0,

        /// <summary>
        /// Identity validation has occurred.
        /// </summary>
        Validated = 1,

        /// <summary>
        /// We performed identity validation not in VssfAuthenticationModule, but later in the pipeline.
        /// </summary>
        DelayedIdentityValidation = 2
    }

    internal interface IWebRequestContextInternal
    {
     
        void SetAuthenticationType(String authenticationType);
    }
    #endregion
}