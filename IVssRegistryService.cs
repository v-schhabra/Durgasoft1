using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Services.RegistryService.Server;

namespace Microsoft.TeamFoundation.Framework.Server
{
    [DefaultServiceImplementation(typeof(CachedRegistryService), typeof(VirtualCachedRegistryService))]
    public interface IVssRegistryService : IVssFrameworkService
    {
        /// <summary>
        /// Reads entries from the registry matching the provided query.
        /// </summary>
        IEnumerable<RegistryItem> Read(IVssRequestContext requestContext, in RegistryQuery query);

        /// <summary>
        /// Reads entries from the registry matching the provided queries.
        /// </summary>
        IEnumerable<IEnumerable<RegistryItem>> Read(IVssRequestContext requestContext, IEnumerable<RegistryQuery> queries);

        /// <summary>
        /// Writes the provided entries to the registry.
        /// </summary>
        void Write(IVssRequestContext requestContext, IEnumerable<RegistryItem> items);

        /// <summary>
        /// Gets the parent registry service of the same flavor.
        /// </summary>
        IVssRegistryService GetParent(IVssRequestContext requestContext);

        /// <summary>
        /// Registers a callback with the registry service. The provided delegate will be invoked on a notification
        /// thread when a change occurs to this service host's registry which matches any of the provided filters. If the
        /// fallThru parameter is true, then the delegate will also be invoked when a matching change occurs in the
        /// registry of any parent service host.
        ///
        /// You must provide at least one filter in order to register a callback.
        ///
        /// If the provided delegate has already been registered, then a subsequent call to RegisterNotification
        /// with the same delegate will amend the callback registration, adding to the set of possible filters
        /// which can result in the callback's execution.
        ///
        /// Notifications must be unregistered by calling UnregisterNotification. If fallThru was set to true when
        /// RegisterNotification was called, then fallThru must also be set to true when UnregisterNotification is
        /// called.
        /// </summary>
        /// <param name="requestContext">A request context for this service host</param>
        /// <param name="callback">The delegate to invoke when a registry change occurs</param>
        /// <param name="fallThru">True to invoke the delegate when registry changes occur to parent service hosts; false otherwise</param>
        /// <param name="filters">The filter queries to match; at least one must be provided</param>
        /// <param name="serviceHostId">The service hostId to register against, defaults to the request context's service host.</param>
        void RegisterNotification(
            IVssRequestContext requestContext,
            RegistrySettingsChangedCallback callback,
            bool fallThru,
            IEnumerable<RegistryQuery> filters,
            Guid serviceHostId = default(Guid));

        /// <summary>
        /// Unregisters the given callback to stop receiving notifications
        /// </summary>
        /// <param name="requestContext"></param>
        /// <param name="callback"></param>
        void UnregisterNotification(
            IVssRequestContext requestContext,
            RegistrySettingsChangedCallback callback);
    }

    /// <summary>
    /// Delegate for the RegistrySettingsChanged callback. Notifies a subscriber
    /// that the given registry entries were changed
    /// </summary>
    /// <param name="requestContext"></param>
    /// <param name="registryEntries"></param>
    public delegate void RegistrySettingsChangedCallback(IVssRequestContext requestContext, RegistryEntryCollection changedEntries);
}
