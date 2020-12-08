using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClusterTest.Messages;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using Xunit;

namespace Proto.Cluster.Tests
{
    public interface IClusterFixture
    {
        IList<Cluster> Members { get; }

        public Task<Cluster> SpawnNode();

        Task RemoveNode(Cluster member, bool graceful = true);
    }

    public abstract class ClusterFixture : IAsyncLifetime, IClusterFixture
    {
        private readonly int _clusterSize;
        private readonly Func<ClusterConfig, ClusterConfig> _configure;
        private readonly ILogger _logger = Log.CreateLogger(nameof(GetType));
        private readonly string _clusterName;

        protected ClusterFixture(int clusterSize, Func<ClusterConfig, ClusterConfig> configure = null)
        {
            _clusterSize = clusterSize;
            _configure = configure;
            _clusterName = $"test-cluster-{Guid.NewGuid().ToString().Substring(0, 6)}";
        }

        protected virtual (string, Props)[] ClusterKinds => new[]
        {
            (EchoActor.Kind, EchoActor.Props),
            (EchoActor.Kind2, EchoActor.Props)
        };

        public async Task InitializeAsync()
        {
            Members = await SpawnClusterNodes(_clusterSize, _configure);
        }

        public async Task DisposeAsync()
        {
            try
            {
                await Task.WhenAll(Members?.Select(cluster => cluster.ShutdownAsync()) ?? new[] {Task.CompletedTask});
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to shutdown gracefully");
                throw;
            }
        }

        public async Task RemoveNode(Cluster member, bool graceful = true)
        {
            if (Members.Contains(member))
            {
                Members.Remove(member);
                await member.ShutdownAsync(graceful);
            }
            else throw new ArgumentException("No such member");
        }

        /// <summary>
        /// Spawns a node, adds it to the cluster and member list
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task<Cluster> SpawnNode()
        {
            var newMember = await SpawnClusterMember(_configure);
            Members.Add(newMember);
            return newMember;
        }

        public IList<Cluster> Members { get; private set; }

        private async Task<IList<Cluster>> SpawnClusterNodes(
            int count,
            Func<ClusterConfig, ClusterConfig> configure = null
        ) => (await Task.WhenAll(
            Enumerable.Range(0, count)
                .Select(_ => SpawnClusterMember(configure))
        )).ToList();

        private async Task<Cluster> SpawnClusterMember(Func<ClusterConfig, ClusterConfig> configure)
        {
            var config = ClusterConfig.Setup(
                    _clusterName,
                    GetClusterProvider(),
                    GetIdentityLookup(_clusterName)
                )
                .WithClusterKinds(ClusterKinds);

            config = configure?.Invoke(config) ?? config;
            var system = new ActorSystem();

            RegisterRemote(system);

            var cluster = new Cluster(system, config);

            await cluster.StartMemberAsync();
            return cluster;
        }

        protected virtual void RegisterRemote(ActorSystem system)
        {
            var remoteConfig = GrpcCoreRemoteConfig.BindToLocalhost().WithProtoMessages(MessagesReflection.Descriptor);
            var _ = new GrpcCoreRemote(system, remoteConfig);
        }

        protected abstract IClusterProvider GetClusterProvider();

        protected virtual IIdentityLookup GetIdentityLookup(string clusterName) => new PartitionIdentityLookup();
    }

    public abstract class BaseInMemoryClusterFixture : ClusterFixture
    {
        private readonly Lazy<InMemAgent> _inMemAgent = new(() => new InMemAgent());

        protected BaseInMemoryClusterFixture(int clusterSize, Func<ClusterConfig, ClusterConfig> configure = null) :
            base(
                clusterSize,
                configure
            )
        {
        }

        private InMemAgent InMemAgent => _inMemAgent.Value;

        protected override IClusterProvider GetClusterProvider() =>
            new TestProvider(new TestProviderOptions(), InMemAgent);
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class InMemoryClusterFixtureGrpcNet : BaseInMemoryClusterFixture
    {
        public InMemoryClusterFixtureGrpcNet() : base(3)
        {
        }

        protected override void RegisterRemote(ActorSystem system)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var remoteConfig = GrpcNetRemoteConfig.BindToLocalhost().WithProtoMessages(MessagesReflection.Descriptor);
            var _ = new GrpcNetRemote(system, remoteConfig);
        }
    }

    public class InMemoryClusterFixtureGrpcCore : BaseInMemoryClusterFixture
    {
        public InMemoryClusterFixtureGrpcCore() : base(3)
        {
        }

        protected override void RegisterRemote(ActorSystem system)
        {
            var remoteConfig = GrpcCoreRemoteConfig.BindToLocalhost().WithProtoMessages(MessagesReflection.Descriptor);
            var _ = new GrpcCoreRemote(system, remoteConfig);
        }
    }
}