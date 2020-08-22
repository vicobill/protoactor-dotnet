using System;
using System.Collections.Generic;
using System.Linq;
using Proto.Cluster.Data;

namespace Proto.Cluster.Events
{
    public class ClusterTopologyEvent
    {
        public ClusterTopologyEvent(IEnumerable<Member> statuses)
        {
            Statuses = statuses?.ToArray() ?? throw new ArgumentNullException(nameof(statuses));
        }

        public IReadOnlyCollection<Member> Statuses { get; }
    }
}