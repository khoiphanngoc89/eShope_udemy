using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class HybridLogCollectorRulesTests
{
    [Fact]
    public void Compose_WiresLogCollectorWithoutGatingCatalogOrKafka()
    {
        var composePath = Path.Combine(RepoRoot.Path, "deploy", "compose.yaml");
        var overridePath = Path.Combine(RepoRoot.Path, "deploy", "compose.override.yaml");
        Assert.True(File.Exists(composePath), composePath);
        Assert.True(File.Exists(overridePath), overridePath);

        var compose = File.ReadAllText(composePath);
        Assert.Contains("log-collector:", compose, StringComparison.Ordinal);
        Assert.Contains("grafana/alloy:", compose, StringComparison.Ordinal);

        var overrideText = File.ReadAllText(overridePath);
        var catalog = ServiceBlock(overrideText, "catalogapi");
        var kafka = ServiceBlock(overrideText, "kafka");
        var collector = ServiceBlock(overrideText, "log-collector");

        Assert.Contains("catalogdb", DependsOnBlock(catalog), StringComparison.Ordinal);
        Assert.DoesNotContain("log-collector", DependsOnBlock(catalog), StringComparison.Ordinal);
        Assert.DoesNotContain("lgtm", DependsOnBlock(catalog), StringComparison.Ordinal);

        Assert.DoesNotContain("log-collector", kafka, StringComparison.Ordinal);
        Assert.DoesNotContain("depends_on:", kafka, StringComparison.Ordinal);

        Assert.Contains("eshop_log_collector", collector, StringComparison.Ordinal);
        Assert.Contains("/var/run/docker.sock", collector, StringComparison.Ordinal);
        Assert.Contains("config.alloy", collector, StringComparison.Ordinal);
        Assert.Contains("lgtm", DependsOnBlock(collector), StringComparison.Ordinal);
        Assert.DoesNotContain("catalogapi", DependsOnBlock(collector), StringComparison.Ordinal);
        Assert.DoesNotContain("kafka:", DependsOnBlock(collector), StringComparison.Ordinal);
    }

    [Fact]
    public void AlloyConfig_DefaultCollectsEshopNetworkAndDropsApplicationExporters()
    {
        var alloyPath = Path.Combine(RepoRoot.Path, "deploy", "alloy", "config.alloy");
        Assert.True(File.Exists(alloyPath), $"Expected Alloy config at {alloyPath}");

        var alloy = File.ReadAllText(alloyPath);
        Assert.False(string.IsNullOrWhiteSpace(alloy));

        Assert.Contains("eshop", alloy, StringComparison.Ordinal);
        Assert.Contains("__meta_docker_network_name", alloy, StringComparison.Ordinal);
        Assert.Contains("catalogapi", alloy, StringComparison.Ordinal);
        Assert.Contains("log-collector", alloy, StringComparison.Ordinal);
        Assert.Contains("lgtm", alloy, StringComparison.Ordinal);
        Assert.Contains("__meta_docker_container_label_com_docker_compose_service", alloy, StringComparison.Ordinal);
        Assert.Contains("compose_service", alloy, StringComparison.Ordinal);
        Assert.Contains("http://lgtm:3100/loki/api/v1/push", alloy, StringComparison.Ordinal);

        Assert.DoesNotContain("eshop_catalog_api", alloy, StringComparison.Ordinal);
        Assert.DoesNotContain("eshop_kafka", alloy, StringComparison.Ordinal);
        Assert.DoesNotContain("eshop.catalog.sync", alloy, StringComparison.Ordinal);
        Assert.DoesNotContain("subscribe", alloy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("consumer", alloy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/var/lib/kafka", alloy, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_DocumentsAllContainerLogqlWithoutCatalogDuplicatePath()
    {
        var readmePath = Path.Combine(RepoRoot.Path, "README.md");
        Assert.True(File.Exists(readmePath), readmePath);

        var readme = File.ReadAllText(readmePath);
        Assert.Contains("{compose_service=\"catalogdb\"}", readme, StringComparison.Ordinal);
        Assert.Contains("{compose_service=\"kafka\"}", readme, StringComparison.Ordinal);
        Assert.Contains("{compose_service=\"connect\"}", readme, StringComparison.Ordinal);
        Assert.Contains("{compose_service=\"elasticsearch\"}", readme, StringComparison.Ordinal);
        Assert.Contains("{compose_service=\"kibana\"}", readme, StringComparison.Ordinal);
        Assert.Contains("{compose_service=\"kafka-ui\"}", readme, StringComparison.Ordinal);
        Assert.Contains("{service_name=~\".*eShop.Catalog.Api.*\"}", readme, StringComparison.Ordinal);
        Assert.Contains("|= \"Error\"", readme, StringComparison.Ordinal);
        Assert.Contains("|= \"failed\"", readme, StringComparison.Ordinal);
        Assert.Contains("http://localhost:8080", readme, StringComparison.Ordinal);
        Assert.Contains("log-collector", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("{compose_service=\"catalogapi\"}", readme, StringComparison.Ordinal);
    }

    private static string ServiceBlock(string composeYaml, string serviceName)
    {
        var lines = composeYaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var header = $"  {serviceName}:";
        var start = Array.FindIndex(lines, line => line == header);
        Assert.True(start >= 0, $"{serviceName} service is missing from compose");

        var taken = new List<string> { lines[start] };
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("  ", StringComparison.Ordinal)
                && !line.StartsWith("   ", StringComparison.Ordinal)
                && !line.StartsWith("  #", StringComparison.Ordinal))
            {
                break;
            }

            taken.Add(line);
        }

        return string.Join('\n', taken);
    }

    private static string DependsOnBlock(string serviceSection)
    {
        var lines = serviceSection.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith("    depends_on:", StringComparison.Ordinal));
        Assert.True(start >= 0, "depends_on is missing");

        var taken = new List<string> { lines[start] };
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("    ", StringComparison.Ordinal) && !line.StartsWith("     ", StringComparison.Ordinal))
            {
                break;
            }

            taken.Add(line);
        }

        return string.Join('\n', taken);
    }
}
