using FluentResults;
using Kosh.Config.Models;
using Kosh.Core.Definitions;

namespace Kosh.Config.Internal;

internal static class HostsBuilder
{
    public static Result<List<HostDefinition>> BuildHosts(ICollection<YamlHost> yamlHosts)
    {
        var hosts = new List<HostDefinition>();
        if (yamlHosts.Count == 0)
            return hosts;

        foreach (var host in yamlHosts)
        {
            if (string.IsNullOrWhiteSpace(host.Domain))
                continue;

            hosts.Add(new HostDefinition(host.Domain));
        }
        
        return hosts;
    }
}