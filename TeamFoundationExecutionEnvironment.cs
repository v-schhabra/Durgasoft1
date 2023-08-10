using System;

namespace Microsoft.TeamFoundation.Framework.Server
{
    [Flags]
    public enum ExecutionEnvironmentFlags
    {
        None = 0x0,
        OnPremisesDeployment = 0x1,
        DevFabricDeployment = 0x2,
        CloudDeployment = 0x4,
        SslOnly = 0x8,
        OnPremisesProxy = 0x10
    }

    /// <summary>
    /// Provides an information about an environment associated with a request context.
    /// </summary>
    public struct TeamFoundationExecutionEnvironment
    {
        public TeamFoundationExecutionEnvironment(ExecutionEnvironmentFlags flags)
        {
            Flags = flags;
        }

        /// <summary>
        /// Returns true in on-premises Azure DevOps Server environment including on-premises Proxy; otherwise, false.
        /// </summary>
        public Boolean IsOnPremisesDeployment => (Flags & ExecutionEnvironmentFlags.OnPremisesDeployment) != 0;

        /// <summary>
        /// Returns true in DevFabric environment; otherwise, false.
        /// </summary>
        public Boolean IsDevFabricDeployment => (Flags & ExecutionEnvironmentFlags.DevFabricDeployment) != 0;

        /// <summary>
        /// Returns true in Azure environment (Vmss); otherwise, false.
        /// </summary>
        public Boolean IsCloudDeployment => (Flags & ExecutionEnvironmentFlags.CloudDeployment) != 0;

        /// <summary>
        /// Returns true in Azure and DevFabric environments; otherwise, false.
        /// </summary>
        public Boolean IsHostedDeployment => (Flags & (ExecutionEnvironmentFlags.CloudDeployment | ExecutionEnvironmentFlags.DevFabricDeployment)) != 0;

        /// <summary>
        /// Returns true in on-premises Proxy; otherwise, false;
        /// </summary>
        public Boolean IsOnPremisesProxy => (Flags & ExecutionEnvironmentFlags.OnPremisesProxy) != 0;

        /// <summary>
        /// Returns true if only SSL endpoint should process traffic.
        /// http endpoint should redirect to https.
        /// </summary>
        public Boolean IsSslOnly => (Flags & ExecutionEnvironmentFlags.SslOnly) != 0;

        internal Boolean IsInitialized => Flags != ExecutionEnvironmentFlags.None;

        public readonly ExecutionEnvironmentFlags Flags;
    }
}
