using eShop.Catalog.Api.Tests.Support;

namespace eShop.Catalog.Api.Tests;

public class LocalK8sRulesTests
{
    [Fact]
    public void NamespaceYaml_DeclaresCatalogSearchAndViewer()
    {
        var yaml = ReadRequired("deploy", "k8s", "namespace.yaml");

        Assert.Contains("kind: Namespace", yaml, StringComparison.Ordinal);
        Assert.Contains("name: catalog", yaml, StringComparison.Ordinal);
        Assert.Contains("name: search", yaml, StringComparison.Ordinal);
        Assert.Contains("name: viewer", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogPvc_IsClusterClaimNotComposeVolume()
    {
        var yaml = ReadRequired("deploy", "k8s", "catalog", "catalogdb-pvc.yaml");
        var overlay = ReadOverlay("deploy", "k8s", "catalog");

        Assert.Contains("kind: PersistentVolumeClaim", yaml, StringComparison.Ordinal);
        Assert.Contains("name: catalogdb-data", yaml, StringComparison.Ordinal);
        Assert.Contains("namespace: catalog", yaml, StringComparison.Ordinal);
        Assert.Contains("ReadWriteOnce", yaml, StringComparison.Ordinal);
        Assert.Contains("1Gi", yaml, StringComparison.Ordinal);
        Assert.Contains("local-path", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("catalog-pgdata", overlay, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogDb_MatchesComposeStoreOnCluster()
    {
        var yaml = ReadRequired("deploy", "k8s", "catalog", "catalogdb.yaml");

        Assert.Contains("kind: Deployment", yaml, StringComparison.Ordinal);
        Assert.Contains("kind: Service", yaml, StringComparison.Ordinal);
        Assert.Contains("name: catalogdb", yaml, StringComparison.Ordinal);
        Assert.Contains("postgres:16.15", yaml, StringComparison.Ordinal);
        Assert.Contains("replicas: 1", yaml, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_USER", yaml, StringComparison.Ordinal);
        Assert.Contains("catalog", yaml, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_PASSWORD", yaml, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_DB", yaml, StringComparison.Ordinal);
        Assert.Contains("wal_level=logical", yaml, StringComparison.Ordinal);
        Assert.Contains("type: NodePort", yaml, StringComparison.Ordinal);
        Assert.Contains("nodePort: 30432", yaml, StringComparison.Ordinal);
        Assert.Contains("5432", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("nodePort: 5000", yaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pg_isready -U catalog -d catalog", yaml, StringComparison.Ordinal);
        Assert.Contains("catalogdb-data", yaml, StringComparison.Ordinal);
        Assert.Contains("/var/lib/postgresql/data", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogApi_WaitsOnStoreOnlyAndExposesClusterIp8080()
    {
        var yaml = ReadRequired("deploy", "k8s", "catalog", "catalogapi.yaml");

        Assert.Contains("kind: Deployment", yaml, StringComparison.Ordinal);
        Assert.Contains("kind: Service", yaml, StringComparison.Ordinal);
        Assert.Contains("name: catalogapi", yaml, StringComparison.Ordinal);
        Assert.Contains("eshop-catalog-api:local", yaml, StringComparison.Ordinal);
        Assert.Contains("imagePullPolicy: Never", yaml, StringComparison.Ordinal);
        Assert.Contains("replicas: 1", yaml, StringComparison.Ordinal);
        Assert.Contains("8080", yaml, StringComparison.Ordinal);
        Assert.Contains("type: NodePort", yaml, StringComparison.Ordinal);
        Assert.Contains("nodePort: 30050", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain("nodePort: 5000", yaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ConnectionStrings__CatalogDb", yaml, StringComparison.Ordinal);
        Assert.Contains("Host=catalogdb;Port=5432;Database=catalog;Username=catalog;Password=catalog", yaml, StringComparison.Ordinal);
        Assert.Contains("initContainers", yaml, StringComparison.Ordinal);
        Assert.Contains("pg_isready -h catalogdb -U catalog -d catalog", yaml, StringComparison.Ordinal);
        Assert.True(
            yaml.Contains("tcpSocket:", StringComparison.Ordinal)
            || yaml.Contains("httpGet:", StringComparison.Ordinal),
            "catalogapi readiness must be HTTP or TCP on 8080");
        Assert.Contains("readinessProbe", yaml, StringComparison.Ordinal);

        var init = Section(yaml, "initContainers:");
        Assert.DoesNotContain("lgtm", init, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kafka", init, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("elasticsearch", init, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CatalogKustomization_ListsStoreAndApiInCatalogNamespace()
    {
        var yaml = ReadRequired("deploy", "k8s", "catalog", "kustomization.yaml");

        Assert.Contains("namespace: catalog", yaml, StringComparison.Ordinal);
        Assert.Contains("catalogdb-pvc.yaml", yaml, StringComparison.Ordinal);
        Assert.Contains("catalogdb.yaml", yaml, StringComparison.Ordinal);
        Assert.Contains("catalogapi.yaml", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchOverlay_UsesComposeImagesInSearchNamespace()
    {
        var kustomization = ReadRequired("deploy", "k8s", "search", "kustomization.yaml");
        var overlay = ReadOverlay("deploy", "k8s", "search");

        Assert.Contains("namespace: search", kustomization, StringComparison.Ordinal);
        Assert.Contains("apache/kafka:3.8.1", overlay, StringComparison.Ordinal);
        Assert.Contains("1@127.0.0.1:9093", overlay, StringComparison.Ordinal);
        Assert.Contains("CONTROLLER://127.0.0.1:9093", overlay, StringComparison.Ordinal);
        Assert.Contains("PLAINTEXT://kafka.search.svc:9092", overlay, StringComparison.Ordinal);
        Assert.Contains("quay.io/debezium/connect:2.7.3.Final", overlay, StringComparison.Ordinal);
        Assert.Contains("docker.elastic.co/elasticsearch/elasticsearch:8.15.3", overlay, StringComparison.Ordinal);
        Assert.Contains("docker.elastic.co/kibana/kibana:8.15.3", overlay, StringComparison.Ordinal);
        Assert.Contains("provectuslabs/kafka-ui:v0.7.2", overlay, StringComparison.Ordinal);
        Assert.Contains("eshop-catalog-product", overlay, StringComparison.Ordinal);
        Assert.Contains("mt_doc_product", overlay, StringComparison.Ordinal);
        Assert.Contains("catalogdb.catalog.svc", overlay, StringComparison.Ordinal);
        Assert.Contains("wait-product-table", overlay, StringComparison.Ordinal);
        Assert.Contains("to_regclass('public.mt_doc_product')", overlay, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchOverlay_StartsDebeziumOnlyAfterProductTableExists()
    {
        var connect = ReadRequired("deploy", "k8s", "search", "connect.yaml");
        var catalogDi = ReadRequired("src", "Services", "eShop.Catalog.Api", "DependencyInjection.cs");
        var catalogdb = ReadRequired("deploy", "k8s", "catalog", "catalogdb.yaml");
        var readme = ReadRequired("README.md");

        Assert.Contains("POSTGRES_DB", catalogdb, StringComparison.Ordinal);
        Assert.Contains("Schema.For<Product>", catalogDi, StringComparison.Ordinal);
        Assert.Contains("ApplyAllConfiguredChangesToDatabaseAsync", catalogDi, StringComparison.Ordinal);
        Assert.Contains("BootstrapServers", catalogDi, StringComparison.Ordinal);
        Assert.Contains("wait-product-table", connect, StringComparison.Ordinal);
        Assert.Contains("to_regclass('public.mt_doc_product')", connect, StringComparison.Ordinal);
        Assert.Contains("initContainers:", connect, StringComparison.Ordinal);
        Assert.Contains("auto create", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("then", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Debezium", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogApi_StartGateStillDoesNotWaitOnSearch()
    {
        var yaml = ReadRequired("deploy", "k8s", "catalog", "catalogapi.yaml");
        var init = Section(yaml, "initContainers:");

        Assert.Contains("catalogdb", init, StringComparison.Ordinal);
        Assert.DoesNotContain("kafka", init, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connect", init, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("elasticsearch", init, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewerOverlayAndRoot_HostComposeObservabilityWithoutGatingCatalog()
    {
        var overlay = ReadOverlay("deploy", "k8s", "viewer");
        var alloy = ReadRequired("deploy", "k8s", "viewer", "config.alloy");
        var catalogApi = ReadRequired("deploy", "k8s", "catalog", "catalogapi.yaml");
        var root = ReadRequired("deploy", "k8s", "kustomization.yaml");
        var init = Section(catalogApi, "initContainers:");

        Assert.Contains("grafana/otel-lgtm:0.38.0", overlay, StringComparison.Ordinal);
        Assert.Contains("grafana/alloy:v1.21.3", overlay, StringComparison.Ordinal);
        Assert.Contains("quay.io/prometheuscommunity/postgres-exporter:v0.17.1", overlay, StringComparison.Ordinal);
        Assert.Contains("quay.io/prometheuscommunity/elasticsearch-exporter:v1.8.0", overlay, StringComparison.Ordinal);
        Assert.Contains("danielqsj/kafka-exporter:v1.11.0", overlay, StringComparison.Ordinal);
        var kafkaExporter = ReadRequired("deploy", "k8s", "viewer", "exporters.yaml");
        Assert.Contains("wait-kafka", kafkaExporter, StringComparison.Ordinal);
        Assert.Contains("kafka.search.svc", kafkaExporter, StringComparison.Ordinal);
        Assert.Contains("ghcr.io/google/cadvisor:v0.60.6", overlay, StringComparison.Ordinal);

        Assert.Contains("discovery.kubernetes", alloy, StringComparison.Ordinal);
        Assert.Contains("catalog", alloy, StringComparison.Ordinal);
        Assert.Contains("search", alloy, StringComparison.Ordinal);
        Assert.Contains("viewer", alloy, StringComparison.Ordinal);
        Assert.DoesNotContain("docker.sock", alloy, StringComparison.Ordinal);
        Assert.DoesNotContain("discovery.docker", alloy, StringComparison.Ordinal);
        Assert.Contains("/run/k3s/containerd/containerd.sock", overlay, StringComparison.Ordinal);
        Assert.DoesNotContain("/var/run/docker.sock", overlay, StringComparison.Ordinal);

        Assert.DoesNotContain("lgtm", init, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OTLP", init, StringComparison.Ordinal);
        Assert.DoesNotContain("4318", init, StringComparison.Ordinal);

        Assert.Contains("catalog/", root, StringComparison.Ordinal);
        Assert.Contains("search/", root, StringComparison.Ordinal);
        Assert.Contains("viewer/", root, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewerPrometheus_ScrapesExportersAndRelabelsComposeService()
    {
        var yaml = ReadRequired("deploy", "k8s", "viewer", "prometheus.yaml");

        Assert.Contains("postgres-exporter:9187", yaml, StringComparison.Ordinal);
        Assert.Contains("elasticsearch-exporter:9114", yaml, StringComparison.Ordinal);
        Assert.Contains("kafka-exporter:9308", yaml, StringComparison.Ordinal);
        Assert.Contains("cadvisor:8080", yaml, StringComparison.Ordinal);
        Assert.Contains("job_name: containers", yaml, StringComparison.Ordinal);
        Assert.Contains("compose_service", yaml, StringComparison.Ordinal);
        Assert.Contains("catalogdb", yaml, StringComparison.Ordinal);
        Assert.Contains("catalogapi", yaml, StringComparison.Ordinal);
        Assert.Contains("kafka", yaml, StringComparison.Ordinal);
        Assert.Contains("connect", yaml, StringComparison.Ordinal);
        Assert.Contains("elasticsearch", yaml, StringComparison.Ordinal);
        Assert.Contains("kibana", yaml, StringComparison.Ordinal);
        Assert.Contains("kafka-ui", yaml, StringComparison.Ordinal);
        Assert.Contains("elasticsearch-exporter", yaml, StringComparison.Ordinal);
        Assert.Contains("kafka-exporter", yaml, StringComparison.Ordinal);
        Assert.Contains("postgres-exporter", yaml, StringComparison.Ordinal);
        Assert.Contains("otlp:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Lgtm_MountsPrometheusAndOpsInfraWithoutGatingCatalog()
    {
        var lgtm = ReadRequired("deploy", "k8s", "viewer", "lgtm.yaml");
        var kustomization = ReadRequired("deploy", "k8s", "viewer", "kustomization.yaml");
        var catalogApi = ReadRequired("deploy", "k8s", "catalog", "catalogapi.yaml");
        var init = Section(catalogApi, "initContainers:");
        var mounts = lgtm + "\n" + kustomization;

        Assert.Contains("/otel-lgtm/prometheus.yaml", mounts, StringComparison.Ordinal);
        Assert.Contains("ops-infra.json", mounts, StringComparison.Ordinal);
        Assert.Contains("/otel-lgtm/eshop-dashboards", mounts, StringComparison.Ordinal);
        Assert.DoesNotContain("../grafana", kustomization, StringComparison.Ordinal);
        Assert.Equal(
            ReadRequired("deploy", "grafana", "provisioning", "dashboards", "ops-infra.json"),
            ReadRequired("deploy", "k8s", "viewer", "ops-infra.json"));
        Assert.Equal(
            ReadRequired("deploy", "grafana", "provisioning", "dashboards", "dashboards.yaml"),
            ReadRequired("deploy", "k8s", "viewer", "eshop-dashboards.yaml"));
        Assert.DoesNotContain("kube-prometheus", mounts, StringComparison.Ordinal);
        Assert.DoesNotContain("lgtm", init, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OTLP", init, StringComparison.Ordinal);
    }

    [Fact]
    public void OtherHostUis_AreNodePortInYaml()
    {
        var lgtm = ReadRequired("deploy", "k8s", "viewer", "lgtm.yaml");
        var kafkaUi = ReadRequired("deploy", "k8s", "search", "kafka-ui.yaml");
        var kibana = ReadRequired("deploy", "k8s", "search", "kibana.yaml");
        var catalogApi = ReadRequired("deploy", "k8s", "catalog", "catalogapi.yaml");

        Assert.Contains("type: NodePort", lgtm, StringComparison.Ordinal);
        Assert.Contains("nodePort: 30000", lgtm, StringComparison.Ordinal);
        Assert.Contains("type: NodePort", kafkaUi, StringComparison.Ordinal);
        Assert.Contains("nodePort: 30080", kafkaUi, StringComparison.Ordinal);
        Assert.Contains("type: NodePort", kibana, StringComparison.Ordinal);
        Assert.Contains("nodePort: 30561", kibana, StringComparison.Ordinal);

        Assert.Contains("type: NodePort", catalogApi, StringComparison.Ordinal);
        Assert.Contains("nodePort: 30050", catalogApi, StringComparison.Ordinal);
        Assert.DoesNotContain("nodePort: 5000", catalogApi, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PortForwardScript_ForwardsComposeHostPortsToLocalhost()
    {
        var script = ReadRequired("deploy", "k8s", "port-forward-localhost.sh");

        Assert.Contains("kubectl -n catalog port-forward svc/catalogapi 5000:8080", script, StringComparison.Ordinal);
        Assert.Contains("kubectl -n catalog port-forward svc/catalogdb 5432:5432", script, StringComparison.Ordinal);
        Assert.Contains("kubectl -n search port-forward svc/kafka-ui 8080:8080", script, StringComparison.Ordinal);
        Assert.Contains("kubectl -n search port-forward svc/kibana 5601:5601", script, StringComparison.Ordinal);
        Assert.Contains("kubectl -n viewer port-forward svc/lgtm 3000:3000", script, StringComparison.Ordinal);
        Assert.Contains("--address", script, StringComparison.Ordinal);
        Assert.Contains("sudo k3s kubectl", script, StringComparison.Ordinal);
        Assert.DoesNotContain("minikube start", script, StringComparison.Ordinal);
        Assert.DoesNotContain("kubectl apply", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_DocumentsWsl2K3sFullStackWithoutComposeRun()
    {
        var readme = ReadRequired("README.md");

        Assert.Contains("INSTALL_K3S_EXEC=\"--disable traefik --disable servicelb\"", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("kind create cluster", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("k3d cluster create", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("minikube start", readme, StringComparison.Ordinal);
        Assert.Contains("Docker Desktop Kubernetes", readme, StringComparison.Ordinal);
        Assert.Contains("/t:PublishContainer", readme, StringComparison.Ordinal);
        Assert.Contains("k3s ctr images import", readme, StringComparison.Ordinal);
        Assert.Contains("eshop-catalog-api:local", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl apply -f deploy/k8s/namespace.yaml", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl apply -k deploy/k8s", readme, StringComparison.Ordinal);
        Assert.Contains("deploy/k8s/port-forward-localhost.sh", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n catalog port-forward svc/catalogapi 5000:8080", readme, StringComparison.Ordinal);
        Assert.Contains("http://localhost:5000", readme, StringComparison.Ordinal);
        Assert.Contains("catalogdb-data", readme, StringComparison.Ordinal);
        Assert.Contains("systemctl stop k3s", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl delete pvc catalogdb-data -n catalog", readme, StringComparison.Ordinal);
        Assert.Contains("`catalog`", readme, StringComparison.Ordinal);
        Assert.Contains("`search`", readme, StringComparison.Ordinal);
        Assert.Contains("`viewer`", readme, StringComparison.Ordinal);
        Assert.Contains("compose files stay", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("http://localhost:3000/d/eshop-ops-infra/ops-infra", readme, StringComparison.Ordinal);
        Assert.Contains("hostname -I", readme, StringComparison.Ordinal);
        Assert.Contains(":30000", readme, StringComparison.Ordinal);
        Assert.Contains(":30080", readme, StringComparison.Ordinal);
        Assert.Contains(":30561", readme, StringComparison.Ordinal);
        Assert.Contains(":30050", readme, StringComparison.Ordinal);
        Assert.Contains(":30432", readme, StringComparison.Ordinal);
        Assert.Contains("EXTERNAL-IP", readme, StringComparison.Ordinal);
        Assert.Contains("<none>", readme, StringComparison.Ordinal);
        Assert.Contains("8080:30050/TCP", readme, StringComparison.Ordinal);
        Assert.Contains("5432:30432/TCP", readme, StringComparison.Ordinal);
        Assert.Contains("empty", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("defect", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("still be starting", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("minikube image load", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("minikube stop", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("minikube delete", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_DocumentsK3sCheckScripts()
    {
        var readme = ReadRequired("README.md");

        Assert.Contains("systemctl is-active k3s", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl get pods -A", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl get pods,svc -n catalog", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl get pods,svc -n search", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl get pods,svc -n viewer", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n catalog rollout status deploy/catalogdb", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n catalog rollout status deploy/catalogapi", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n search rollout status deploy/elasticsearch", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n search rollout status deploy/kafka", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n search rollout status deploy/connect", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n viewer rollout status deploy/lgtm", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n viewer rollout status deploy/postgres-exporter", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n viewer rollout status deploy/elasticsearch-exporter", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n viewer rollout status deploy/kafka-exporter", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n viewer rollout status deploy/cadvisor", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n catalog get pvc catalogdb-data", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl get events -A", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n catalog describe pod -l app=catalogapi", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n catalog logs -l app=catalogapi", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n search logs job/connect-register", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl -n catalog exec deploy/catalogdb --", readme, StringComparison.Ordinal);
        Assert.Contains("SELECT count(*) FROM mt_doc_product", readme, StringComparison.Ordinal);
        Assert.Contains("_cat/indices", readme, StringComparison.Ordinal);
        Assert.Contains("k3s ctr images ls", readme, StringComparison.Ordinal);
        Assert.Contains("/products?pageNumber=1", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_DocumentsCatalogRedeploy()
    {
        var readme = ReadRequired("README.md");

        Assert.Contains("-t:PublishContainer", readme, StringComparison.Ordinal);
        Assert.Contains("-p:ContainerRepository=eshop-catalog-api", readme, StringComparison.Ordinal);
        Assert.Contains("-p:ContainerImageTag=local", readme, StringComparison.Ordinal);
        Assert.Contains("k3s ctr images import", readme, StringComparison.Ordinal);
        Assert.Contains("rollout restart deploy/catalogapi", readme, StringComparison.Ordinal);
        Assert.Contains("imagePullPolicy: Never", readme, StringComparison.Ordinal);
        Assert.Contains("wsl.exe -d Ubuntu -u root", readme, StringComparison.Ordinal);
        Assert.Contains("eshop-catalog-api-local.tar", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void Readme_DocumentsFullStackDeploy()
    {
        var readme = ReadRequired("README.md");

        Assert.Contains("kubectl apply -f deploy/k8s/namespace.yaml", readme, StringComparison.Ordinal);
        Assert.Contains("kubectl apply -k deploy/k8s", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/catalogdb", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/catalogapi", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/elasticsearch", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/kafka", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/connect", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/kibana", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/kafka-ui", readme, StringComparison.Ordinal);
        Assert.Contains("job/connect-register", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/lgtm", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/log-collector", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/postgres-exporter", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/elasticsearch-exporter", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/kafka-exporter", readme, StringComparison.Ordinal);
        Assert.Contains("rollout status deploy/cadvisor", readme, StringComparison.Ordinal);
        Assert.Contains("rollout restart deploy/catalogdb", readme, StringComparison.Ordinal);
        Assert.Contains("rollout restart deploy/kafka", readme, StringComparison.Ordinal);
        Assert.Contains("rollout restart deploy/lgtm", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void IsolatedTestSource_DoesNotStartClusterOrCompose()
    {
        var source = ReadRequired("tests", "Services", "eShop.Catalog.Api.Tests", "LocalK8sRulesTests.cs");

        Assert.DoesNotContain("using System." + "Diagnostics", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new " + "Process", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Docker." + "DotNet", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Kubernetes" + "Client", source, StringComparison.Ordinal);
        Assert.DoesNotContain("I" + "Kubernetes", source, StringComparison.Ordinal);
    }

    private static string ReadRequired(params string[] relativeParts)
    {
        var path = Path.Combine(new[] { RepoRoot.Path }.Concat(relativeParts).ToArray());
        Assert.True(File.Exists(path), $"Expected file at {path}");
        var text = File.ReadAllText(path);
        Assert.False(string.IsNullOrWhiteSpace(text), $"{path} is empty");
        return text;
    }

    private static string ReadOverlay(params string[] relativeParts)
    {
        var dir = Path.Combine(new[] { RepoRoot.Path }.Concat(relativeParts).ToArray());
        Assert.True(Directory.Exists(dir), $"Expected overlay at {dir}");
        return string.Join('\n', Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Select(File.ReadAllText));
    }

    private static string Section(string yaml, string header)
    {
        var lines = yaml.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line.TrimStart().StartsWith(header, StringComparison.Ordinal));
        if (start < 0)
        {
            return string.Empty;
        }

        var indent = lines[start].TakeWhile(char.IsWhiteSpace).Count();
        var taken = new List<string> { lines[start] };
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0)
            {
                taken.Add(line);
                continue;
            }

            var lineIndent = line.TakeWhile(char.IsWhiteSpace).Count();
            if (lineIndent <= indent && line.TrimStart().Length > 0)
            {
                break;
            }

            taken.Add(line);
        }

        return string.Join('\n', taken);
    }
}
