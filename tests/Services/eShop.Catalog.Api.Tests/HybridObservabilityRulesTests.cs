using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class HybridObservabilityRulesTests
{
    [Fact]
    public void AlertRules_FireOnRedHeapAndCpu_ButNotOnEmptyIndexOrYellowReplicas()
    {
        var rulesPath = Path.Combine(
            RepoRoot.Path,
            "deploy",
            "grafana",
            "provisioning",
            "alerting",
            "es-cluster.yaml");

        Assert.True(File.Exists(rulesPath), $"Expected Grafana alert rules at {rulesPath}");

        var rules = File.ReadAllText(rulesPath);
        Assert.False(string.IsNullOrWhiteSpace(rules));

        Assert.Contains("elasticsearch_cluster_health_status", rules, StringComparison.Ordinal);
        Assert.Contains("color=\"red\"", rules, StringComparison.Ordinal);

        Assert.Contains("elasticsearch_jvm_memory_used_bytes", rules, StringComparison.Ordinal);
        Assert.Contains("elasticsearch_jvm_memory_max_bytes", rules, StringComparison.Ordinal);
        Assert.Contains("area=\"heap\"", rules, StringComparison.Ordinal);
        Assert.Contains("0.90", rules, StringComparison.Ordinal);
        Assert.Contains("2m", rules, StringComparison.Ordinal);

        Assert.Contains("elasticsearch_process_cpu_percent", rules, StringComparison.Ordinal);
        Assert.Contains("> 90", rules, StringComparison.Ordinal);
        Assert.Contains("5m", rules, StringComparison.Ordinal);

        Assert.DoesNotContain("number_of_documents", rules, StringComparison.Ordinal);
        Assert.DoesNotContain("color=\"yellow\"", rules, StringComparison.Ordinal);
        Assert.DoesNotContain("unassigned_shards", rules, StringComparison.Ordinal);
        Assert.DoesNotContain("slack", rules, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", rules, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpsInfraDashboard_ShowsElasticsearchKafkaAndCatalogMetrics()
    {
        var dashboardPath = Path.Combine(
            RepoRoot.Path,
            "deploy",
            "grafana",
            "provisioning",
            "dashboards",
            "ops-infra.json");
        Assert.True(File.Exists(dashboardPath), dashboardPath);

        var dashboard = File.ReadAllText(dashboardPath);
        Assert.Contains("\"title\": \"Ops/Infra\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("eshop-ops-infra", dashboard, StringComparison.Ordinal);
        Assert.Contains("prometheus", dashboard, StringComparison.Ordinal);

        Assert.Contains("elasticsearch_cluster_health_status", dashboard, StringComparison.Ordinal);
        Assert.Contains("elasticsearch_process_cpu_percent", dashboard, StringComparison.Ordinal);
        Assert.Contains("elasticsearch_jvm_memory_used_bytes", dashboard, StringComparison.Ordinal);

        Assert.Contains("kafka_brokers", dashboard, StringComparison.Ordinal);
        Assert.Contains("kafka_topic_partitions", dashboard, StringComparison.Ordinal);
        Assert.Contains("kafka_consumergroup_lag", dashboard, StringComparison.Ordinal);

        Assert.Contains("eShop.Catalog.Api", dashboard, StringComparison.Ordinal);
        Assert.Contains("http_server_request_duration", dashboard, StringComparison.Ordinal);
        Assert.Contains("catalog_search_duration_milliseconds", dashboard, StringComparison.Ordinal);
        Assert.Contains("catalog_search_sync_lag_seconds", dashboard, StringComparison.Ordinal);
    }

    [Fact]
    public void Prometheus_ScrapesElasticsearchKafkaStoreAndCadvisor()
    {
        var prometheusPath = Path.Combine(RepoRoot.Path, "deploy", "prometheus", "prometheus.yaml");
        Assert.True(File.Exists(prometheusPath), prometheusPath);

        var prometheus = File.ReadAllText(prometheusPath);
        Assert.Contains("job_name: elasticsearch", prometheus, StringComparison.Ordinal);
        Assert.Contains("elasticsearch-exporter:9114", prometheus, StringComparison.Ordinal);
        Assert.Contains("job_name: kafka", prometheus, StringComparison.Ordinal);
        Assert.Contains("kafka-exporter:9308", prometheus, StringComparison.Ordinal);
        Assert.Contains("job_name: catalogdb", prometheus, StringComparison.Ordinal);
        Assert.Contains("postgres-exporter:9187", prometheus, StringComparison.Ordinal);
        Assert.Contains("job_name: containers", prometheus, StringComparison.Ordinal);
        Assert.Contains("cadvisor:8080", prometheus, StringComparison.Ordinal);
        Assert.Contains("job_name: grafana", prometheus, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:3000", prometheus, StringComparison.Ordinal);
        Assert.Contains("job_name: lgtm-prometheus", prometheus, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:9090", prometheus, StringComparison.Ordinal);
        Assert.Contains("otlp:", prometheus, StringComparison.Ordinal);
        Assert.DoesNotContain("stat_statements", prometheus, StringComparison.Ordinal);
        Assert.DoesNotContain("include_query", prometheus, StringComparison.Ordinal);

        var compose = File.ReadAllText(Path.Combine(RepoRoot.Path, "deploy", "compose.yaml"));
        var overrideText = File.ReadAllText(Path.Combine(RepoRoot.Path, "deploy", "compose.override.yaml"));
        Assert.Contains("kafka-exporter:", compose, StringComparison.Ordinal);
        Assert.Contains("danielqsj/kafka-exporter:", compose, StringComparison.Ordinal);
        Assert.Contains("eshop_kafka_exporter", overrideText, StringComparison.Ordinal);
        Assert.DoesNotContain("kafka-exporter", CatalogDependsOn(overrideText), StringComparison.Ordinal);
        Assert.DoesNotContain("stat_statements", overrideText, StringComparison.Ordinal);
        Assert.DoesNotContain("include_query", overrideText, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_WiresCadvisorAndPostgresExporterWithoutGatingCatalog()
    {
        var compose = File.ReadAllText(Path.Combine(RepoRoot.Path, "deploy", "compose.yaml"));
        var overrideText = File.ReadAllText(Path.Combine(RepoRoot.Path, "deploy", "compose.override.yaml"));

        Assert.True(
            compose.Contains("gcr.io/cadvisor/cadvisor:", StringComparison.Ordinal)
            || compose.Contains("ghcr.io/google/cadvisor:", StringComparison.Ordinal),
            "cadvisor image is missing from compose.yaml");
        Assert.Contains("postgres-exporter:", compose, StringComparison.Ordinal);
        Assert.Contains("quay.io/prometheuscommunity/postgres-exporter:", compose, StringComparison.Ordinal);

        Assert.Contains("eshop_cadvisor", overrideText, StringComparison.Ordinal);
        Assert.Contains("eshop_postgres_exporter", overrideText, StringComparison.Ordinal);
        Assert.Contains("/var/run/docker.sock", ServiceBlock(overrideText, "cadvisor"), StringComparison.Ordinal);
        Assert.Contains("catalogdb", ServiceBlock(overrideText, "postgres-exporter"), StringComparison.Ordinal);
        Assert.Contains("DATA_SOURCE_NAME", ServiceBlock(overrideText, "postgres-exporter"), StringComparison.Ordinal);

        var dependsOn = CatalogDependsOn(overrideText);
        Assert.DoesNotContain("cadvisor", dependsOn, StringComparison.Ordinal);
        Assert.DoesNotContain("postgres-exporter", dependsOn, StringComparison.Ordinal);
        Assert.DoesNotContain("lgtm", dependsOn, StringComparison.Ordinal);
        Assert.Contains("catalogdb", dependsOn, StringComparison.Ordinal);
    }

    [Fact]
    public void OpsInfraDashboard_ShowsProductStackOverviewAndStoreDetail()
    {
        var dashboardPath = Path.Combine(
            RepoRoot.Path,
            "deploy",
            "grafana",
            "provisioning",
            "dashboards",
            "ops-infra.json");
        Assert.True(File.Exists(dashboardPath), dashboardPath);

        var dashboard = File.ReadAllText(dashboardPath);
        Assert.Contains("\"title\": \"Ops/Infra\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("eshop-ops-infra", dashboard, StringComparison.Ordinal);
        Assert.Contains("prometheus", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\": \"service\"", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("var-service", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("$service", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("Detail ($service)", dashboard, StringComparison.Ordinal);

        foreach (var identity in new[]
                 {
                     "catalogapi", "kafka", "connect", "elasticsearch", "kibana",
                     "kafka-ui", "elasticsearch-exporter", "kafka-exporter", "postgres-exporter"
                 })
        {
            Assert.Contains(identity, dashboard, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("\"log-collector\"", dashboard, StringComparison.Ordinal);

        Assert.Contains("pg_up", dashboard, StringComparison.Ordinal);
        Assert.Contains("pg_stat_database_numbackends", dashboard, StringComparison.Ordinal);
        Assert.Contains("pg_database_size_bytes", dashboard, StringComparison.Ordinal);
        Assert.Contains("datname", dashboard, StringComparison.Ordinal);
        Assert.Contains("catalog", dashboard, StringComparison.Ordinal);
        Assert.Contains("container_cpu_usage_seconds_total", dashboard, StringComparison.Ordinal);
        Assert.Contains("container_memory_working_set_bytes", dashboard, StringComparison.Ordinal);
        Assert.Contains("not not-healthy", dashboard, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("elasticsearch_cluster_health_status", dashboard, StringComparison.Ordinal);
        Assert.Contains("elasticsearch_process_cpu_percent", dashboard, StringComparison.Ordinal);
        Assert.Contains("elasticsearch_jvm_memory_used_bytes", dashboard, StringComparison.Ordinal);
        Assert.Contains("kafka_brokers", dashboard, StringComparison.Ordinal);
        Assert.Contains("kafka_topic_partitions", dashboard, StringComparison.Ordinal);
        Assert.Contains("kafka_consumergroup_lag", dashboard, StringComparison.Ordinal);
        Assert.Contains("eShop.Catalog.Api", dashboard, StringComparison.Ordinal);
        Assert.Contains("http_server_request_duration", dashboard, StringComparison.Ordinal);
        Assert.Contains("catalog_search_duration_milliseconds", dashboard, StringComparison.Ordinal);
        Assert.Contains("catalog_search_sync_lag_seconds", dashboard, StringComparison.Ordinal);
        Assert.Contains("\"title\": \"Catalog Db\"", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("catalogdb|", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("Catalog store detail", dashboard, StringComparison.Ordinal);
        var catalogApiRow = dashboard.IndexOf("\"title\": \"Catalog.Api\"", StringComparison.Ordinal);
        var catalogDbRow = dashboard.IndexOf("\"title\": \"Catalog Db\"", StringComparison.Ordinal);
        Assert.True(catalogApiRow >= 0 && catalogDbRow > catalogApiRow, "Catalog Db row must sit below Catalog.Api");
        Assert.Contains("prometheus_tsdb_wal_storage_size_bytes", dashboard, StringComparison.Ordinal);
        Assert.Contains("\"title\": \"Grafana / LGTM disk\"", dashboard, StringComparison.Ordinal);
        var grafanaDiskRow = dashboard.IndexOf("\"title\": \"Grafana / LGTM disk\"", StringComparison.Ordinal);
        Assert.True(grafanaDiskRow > catalogDbRow, "Grafana / LGTM disk must sit below Catalog Db");

        Assert.DoesNotContain("stat_statements", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("SQL statement", dashboard, StringComparison.Ordinal);
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

    private static string CatalogDependsOn(string composeYaml)
    {
        var lines = composeYaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line == "  catalogapi:");
        Assert.True(start >= 0, "catalogapi service is missing");

        var section = new List<string> { lines[start] };
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("  ", StringComparison.Ordinal)
                && !line.StartsWith("   ", StringComparison.Ordinal)
                && !line.StartsWith("  #", StringComparison.Ordinal))
            {
                break;
            }

            section.Add(line);
        }

        var catalog = string.Join('\n', section);
        var depStart = catalog.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var idx = Array.FindIndex(depStart, line => line.StartsWith("    depends_on:", StringComparison.Ordinal));
        Assert.True(idx >= 0, "catalogapi depends_on is missing");
        var taken = new List<string> { depStart[idx] };
        for (var i = idx + 1; i < depStart.Length; i++)
        {
            var line = depStart[i];
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

