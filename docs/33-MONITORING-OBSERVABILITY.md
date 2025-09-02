# Admin & Observability Modules Documentation

This document provides comprehensive technical architecture for the Admin and Observability modules of the DriveOps platform.

## Overview

The Admin and Observability modules are critical infrastructure components that enable SaaS operations, multi-tenant management, monitoring, and system observability across all DriveOps instances.

---

## 1. SaaS Admin Panel Module

### Architecture Overview

The SaaS Admin Panel follows Domain Driven Design (DDD) principles with clean architecture layers:

```
DriveOps.Admin/
├── Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Aggregates/
│   ├── DomainEvents/
│   └── Repositories/
├── Application/
│   ├── Commands/
│   ├── Queries/
│   ├── Handlers/
│   ├── DTOs/
│   └── Services/
├── Infrastructure/
│   ├── Persistence/
│   ├── External/
│   ├── Messaging/
│   └── Configuration/
└── Presentation/
    ├── Controllers/
    ├── Pages/
    └── Components/
```

### 1.1 Domain Layer

#### Domain Entities

```csharp
namespace DriveOps.Admin.Domain.Entities
{
    public class Tenant : AggregateRoot
    {
        public TenantId Id { get; private set; }
        public string CompanyName { get; private set; }
        public string Subdomain { get; private set; }
        public TenantStatus Status { get; private set; }
        public ContactInfo ContactInfo { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }
        
        private readonly List<TenantSubscription> _subscriptions = new();
        public IReadOnlyCollection<TenantSubscription> Subscriptions => _subscriptions.AsReadOnly();
        
        private readonly List<TenantInfrastructure> _infrastructure = new();
        public IReadOnlyCollection<TenantInfrastructure> Infrastructure => _infrastructure.AsReadOnly();

        public void ActivateTenant()
        {
            if (Status != TenantStatus.Pending)
                throw new InvalidOperationException("Only pending tenants can be activated");
                
            Status = TenantStatus.Active;
            ActivatedAt = DateTime.UtcNow;
            AddDomainEvent(new TenantActivatedEvent(Id));
        }

        public void AddSubscription(ModuleType moduleType, SubscriptionTier tier)
        {
            var subscription = new TenantSubscription(Id, moduleType, tier);
            _subscriptions.Add(subscription);
            AddDomainEvent(new SubscriptionAddedEvent(Id, moduleType, tier));
        }
    }

    public class TenantSubscription : Entity
    {
        public TenantSubscriptionId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public ModuleType ModuleType { get; private set; }
        public SubscriptionTier Tier { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime? EndDate { get; private set; }
        public SubscriptionStatus Status { get; private set; }
        public decimal MonthlyPrice { get; private set; }
        
        public TenantSubscription(TenantId tenantId, ModuleType moduleType, SubscriptionTier tier)
        {
            Id = TenantSubscriptionId.New();
            TenantId = tenantId;
            ModuleType = moduleType;
            Tier = tier;
            StartDate = DateTime.UtcNow;
            Status = SubscriptionStatus.Active;
            MonthlyPrice = CalculatePrice(moduleType, tier);
        }
    }

    public class TenantInfrastructure : Entity
    {
        public TenantInfrastructureId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string DatabaseConnectionString { get; private set; }
        public string MongoConnectionString { get; private set; }
        public string RedisConnectionString { get; private set; }
        public string KeycloakRealm { get; private set; }
        public string ApplicationUrl { get; private set; }
        public InfrastructureStatus Status { get; private set; }
        public DateTime DeployedAt { get; private set; }
        
        public void MarkAsDeployed()
        {
            Status = InfrastructureStatus.Deployed;
            DeployedAt = DateTime.UtcNow;
        }
    }

    public class SupportTicket : AggregateRoot
    {
        public SupportTicketId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string Subject { get; private set; }
        public string Description { get; private set; }
        public TicketPriority Priority { get; private set; }
        public TicketStatus Status { get; private set; }
        public UserId CreatedBy { get; private set; }
        public UserId? AssignedTo { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ResolvedAt { get; private set; }
        
        private readonly List<TicketComment> _comments = new();
        public IReadOnlyCollection<TicketComment> Comments => _comments.AsReadOnly();

        public void AssignToSupport(UserId supportUserId)
        {
            AssignedTo = supportUserId;
            AddDomainEvent(new TicketAssignedEvent(Id, supportUserId));
        }

        public void AddComment(string content, UserId userId)
        {
            var comment = new TicketComment(Id, content, userId);
            _comments.Add(comment);
            AddDomainEvent(new TicketCommentAddedEvent(Id, userId));
        }
    }
}
```

#### Value Objects

```csharp
namespace DriveOps.Admin.Domain.ValueObjects
{
    public record TenantId(Guid Value)
    {
        public static TenantId New() => new(Guid.NewGuid());
        public static TenantId From(Guid value) => new(value);
    }

    public record ContactInfo(
        string Email,
        string Phone,
        string FirstName,
        string LastName,
        Address Address
    );

    public record Address(
        string Street,
        string City,
        string PostalCode,
        string Country
    );

    public enum TenantStatus
    {
        Pending = 0,
        Active = 1,
        Suspended = 2,
        Cancelled = 3
    }

    public enum ModuleType
    {
        UsersPermissions = 1,
        Vehicles = 2,
        Notifications = 3,
        Files = 4,
        Garage = 10,
        Breakdown = 11,
        Rental = 12,
        Fleet = 13,
        Accounting = 14,
        CRM = 15
    }

    public enum SubscriptionTier
    {
        Starter = 1,
        Professional = 2,
        Enterprise = 3
    }
}
```

### 1.2 Application Layer

#### Commands and Handlers

```csharp
namespace DriveOps.Admin.Application.Commands
{
    public record CreateTenantCommand(
        string CompanyName,
        string Subdomain,
        ContactInfo ContactInfo,
        List<ModuleType> RequestedModules,
        SubscriptionTier Tier
    ) : IRequest<Result<TenantId>>;

    public class CreateTenantHandler : IRequestHandler<CreateTenantCommand, Result<TenantId>>
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IDeploymentService _deploymentService;
        private readonly IUnitOfWork _unitOfWork;

        public CreateTenantHandler(
            ITenantRepository tenantRepository,
            IDeploymentService deploymentService,
            IUnitOfWork unitOfWork)
        {
            _tenantRepository = tenantRepository;
            _deploymentService = deploymentService;
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<TenantId>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
        {
            // Validate subdomain availability
            if (await _tenantRepository.SubdomainExistsAsync(request.Subdomain))
                return Result.Failure<TenantId>("Subdomain already exists");

            // Create tenant
            var tenant = new Tenant(
                request.CompanyName,
                request.Subdomain,
                request.ContactInfo
            );

            // Add subscriptions
            foreach (var module in request.RequestedModules)
            {
                tenant.AddSubscription(module, request.Tier);
            }

            await _tenantRepository.AddAsync(tenant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Trigger deployment process
            await _deploymentService.ScheduleDeploymentAsync(tenant.Id);

            return Result.Success(tenant.Id);
        }
    }

    public record DeployTenantInfrastructureCommand(TenantId TenantId) : IRequest<Result>;

    public class DeployTenantInfrastructureHandler : IRequestHandler<DeployTenantInfrastructureCommand, Result>
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IInfrastructureOrchestrator _orchestrator;
        private readonly IUnitOfWork _unitOfWork;

        public async Task<Result> Handle(DeployTenantInfrastructureCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepository.GetByIdAsync(request.TenantId);
            if (tenant == null)
                return Result.Failure("Tenant not found");

            // Deploy infrastructure
            var deploymentResult = await _orchestrator.DeployAsync(tenant);
            if (!deploymentResult.IsSuccess)
                return Result.Failure(deploymentResult.Error);

            // Create infrastructure record
            var infrastructure = new TenantInfrastructure(
                tenant.Id,
                deploymentResult.Value.DatabaseConnectionString,
                deploymentResult.Value.MongoConnectionString,
                deploymentResult.Value.RedisConnectionString,
                deploymentResult.Value.KeycloakRealm,
                deploymentResult.Value.ApplicationUrl
            );

            infrastructure.MarkAsDeployed();
            tenant.ActivateTenant();

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
```

### 1.3 Infrastructure Layer

#### Deployment Orchestrator

```csharp
namespace DriveOps.Admin.Infrastructure.Deployment
{
    public interface IInfrastructureOrchestrator
    {
        Task<Result<DeploymentResult>> DeployAsync(Tenant tenant);
        Task<Result> UpdateAsync(TenantId tenantId, List<ModuleType> modules);
        Task<Result> ScaleAsync(TenantId tenantId, ScalingParameters parameters);
    }

    public class KubernetesInfrastructureOrchestrator : IInfrastructureOrchestrator
    {
        private readonly IKubernetesClient _k8sClient;
        private readonly IHelmService _helmService;
        private readonly IPostgreSQLProvisioner _postgresProvisioner;
        private readonly IMongoProvisioner _mongoProvisioner;
        private readonly IKeycloakProvisioner _keycloakProvisioner;

        public async Task<Result<DeploymentResult>> DeployAsync(Tenant tenant)
        {
            var deploymentId = Guid.NewGuid().ToString("N")[..8];
            var namespace = $"driveops-{tenant.Subdomain}";

            try
            {
                // 1. Create Kubernetes namespace
                await _k8sClient.CreateNamespaceAsync(namespace);

                // 2. Provision databases
                var dbResult = await _postgresProvisioner.ProvisionAsync(tenant.Id, namespace);
                var mongoResult = await _mongoProvisioner.ProvisionAsync(tenant.Id, namespace);

                // 3. Deploy Redis
                await _helmService.InstallRedisAsync(namespace);

                // 4. Configure Keycloak realm
                var keycloakResult = await _keycloakProvisioner.CreateRealmAsync(tenant);

                // 5. Deploy application with enabled modules
                var appResult = await DeployApplicationAsync(tenant, namespace, dbResult, mongoResult, keycloakResult);

                return Result.Success(new DeploymentResult(
                    dbResult.ConnectionString,
                    mongoResult.ConnectionString,
                    $"redis://{namespace}-redis:6379",
                    keycloakResult.RealmName,
                    appResult.ApplicationUrl
                ));
            }
            catch (Exception ex)
            {
                // Cleanup on failure
                await CleanupDeploymentAsync(namespace);
                return Result.Failure<DeploymentResult>($"Deployment failed: {ex.Message}");
            }
        }

        private async Task<ApplicationDeploymentResult> DeployApplicationAsync(
            Tenant tenant, 
            string namespace, 
            DatabaseResult dbResult, 
            MongoResult mongoResult, 
            KeycloakResult keycloakResult)
        {
            var helmValues = new
            {
                image = new { tag = "latest" },
                ingress = new 
                { 
                    enabled = true,
                    hosts = new[] { $"{tenant.Subdomain}.driveops.com" }
                },
                database = new
                {
                    connectionString = dbResult.ConnectionString
                },
                mongodb = new
                {
                    connectionString = mongoResult.ConnectionString
                },
                keycloak = new
                {
                    realm = keycloakResult.RealmName,
                    clientId = keycloakResult.ClientId,
                    clientSecret = keycloakResult.ClientSecret
                },
                modules = tenant.Subscriptions.Select(s => s.ModuleType.ToString().ToLower()).ToArray()
            };

            await _helmService.InstallApplicationAsync(namespace, "driveops", helmValues);

            return new ApplicationDeploymentResult($"https://{tenant.Subdomain}.driveops.com");
        }
    }
}
```

### 1.4 Blazor Server UI

#### Tenant Management Pages

```csharp
namespace DriveOps.Admin.Presentation.Pages
{
    public partial class TenantManagement : ComponentBase
    {
        [Inject] private IMediator Mediator { get; set; } = default!;
        [Inject] private INotificationService NotificationService { get; set; } = default!;

        private List<TenantDto> tenants = new();
        private bool isLoading = true;

        protected override async Task OnInitializedAsync()
        {
            await LoadTenantsAsync();
        }

        private async Task LoadTenantsAsync()
        {
            isLoading = true;
            var result = await Mediator.Send(new GetAllTenantsQuery());
            if (result.IsSuccess)
            {
                tenants = result.Value;
            }
            isLoading = false;
            StateHasChanged();
        }

        private async Task CreateTenantAsync()
        {
            // Navigate to create tenant modal/page
        }

        private async Task DeployTenantAsync(TenantId tenantId)
        {
            var result = await Mediator.Send(new DeployTenantInfrastructureCommand(tenantId));
            if (result.IsSuccess)
            {
                await NotificationService.ShowSuccessAsync("Deployment initiated successfully");
                await LoadTenantsAsync();
            }
            else
            {
                await NotificationService.ShowErrorAsync($"Deployment failed: {result.Error}");
            }
        }
    }
}
```

```razor
@page "/admin/tenants"
@using DriveOps.Admin.Application.DTOs

<PageTitle>Tenant Management</PageTitle>

<RadzenStack Gap="1rem">
    <RadzenRow AlignItems="AlignItems.Center">
        <RadzenColumn Size="8">
            <RadzenText TextStyle="TextStyle.H3">Tenant Management</RadzenText>
        </RadzenColumn>
        <RadzenColumn Size="4" class="text-right">
            <RadzenButton ButtonStyle="ButtonStyle.Primary" 
                         Icon="add" 
                         Text="Create Tenant" 
                         Click="CreateTenantAsync" />
        </RadzenColumn>
    </RadzenRow>

    @if (isLoading)
    {
        <RadzenProgressBarCircular ShowValue="true" Value="100" Mode="ProgressBarMode.Indeterminate" />
    }
    else
    {
        <RadzenDataGrid @ref="tenantsGrid" Data="@tenants" TItem="TenantDto" 
                       AllowPaging="true" PageSize="20"
                       AllowSorting="true" AllowFiltering="true">
            <Columns>
                <RadzenDataGridColumn TItem="TenantDto" Property="CompanyName" Title="Company" />
                <RadzenDataGridColumn TItem="TenantDto" Property="Subdomain" Title="Subdomain" />
                <RadzenDataGridColumn TItem="TenantDto" Property="Status" Title="Status">
                    <Template Context="tenant">
                        <RadzenBadge BadgeStyle="@GetStatusBadgeStyle(tenant.Status)" 
                                   Text="@tenant.Status.ToString()" />
                    </Template>
                </RadzenDataGridColumn>
                <RadzenDataGridColumn TItem="TenantDto" Property="CreatedAt" Title="Created" 
                                    FormatString="{0:yyyy-MM-dd}" />
                <RadzenDataGridColumn TItem="TenantDto" Property="SubscriptionCount" Title="Modules" />
                <RadzenDataGridColumn TItem="TenantDto" Title="Actions" Sortable="false" Filterable="false">
                    <Template Context="tenant">
                        <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem">
                            <RadzenButton ButtonStyle="ButtonStyle.Success" 
                                        Icon="cloud_upload" 
                                        Size="ButtonSize.Small"
                                        Text="Deploy"
                                        Disabled="@(tenant.Status != TenantStatus.Pending)"
                                        Click="@(() => DeployTenantAsync(tenant.Id))" />
                            <RadzenButton ButtonStyle="ButtonStyle.Secondary" 
                                        Icon="settings" 
                                        Size="ButtonSize.Small"
                                        Text="Configure" />
                            <RadzenButton ButtonStyle="ButtonStyle.Info" 
                                        Icon="visibility" 
                                        Size="ButtonSize.Small"
                                        Text="View" />
                        </RadzenStack>
                    </Template>
                </RadzenDataGridColumn>
            </Columns>
        </RadzenDataGrid>
    }
</RadzenStack>
```

---

## 2. Observability & Monitoring Module

### 2.1 Metrics Collection Architecture

```csharp
namespace DriveOps.Observability.Domain.Entities
{
    public class SystemMetric : Entity
    {
        public SystemMetricId Id { get; private set; }
        public TenantId? TenantId { get; private set; } // null for platform metrics
        public string MetricName { get; private set; }
        public string MetricType { get; private set; } // Counter, Gauge, Histogram
        public double Value { get; private set; }
        public Dictionary<string, string> Labels { get; private set; }
        public DateTime Timestamp { get; private set; }
        public string Source { get; private set; } // application, infrastructure, business

        public SystemMetric(
            TenantId? tenantId,
            string metricName,
            string metricType,
            double value,
            Dictionary<string, string> labels,
            string source)
        {
            Id = SystemMetricId.New();
            TenantId = tenantId;
            MetricName = metricName;
            MetricType = metricType;
            Value = value;
            Labels = labels ?? new Dictionary<string, string>();
            Timestamp = DateTime.UtcNow;
            Source = source;
        }
    }

    public class Alert : AggregateRoot
    {
        public AlertId Id { get; private set; }
        public AlertRuleId AlertRuleId { get; private set; }
        public TenantId? TenantId { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public AlertSeverity Severity { get; private set; }
        public AlertStatus Status { get; private set; }
        public DateTime TriggeredAt { get; private set; }
        public DateTime? ResolvedAt { get; private set; }
        public Dictionary<string, object> Context { get; private set; }

        private readonly List<AlertNotification> _notifications = new();
        public IReadOnlyCollection<AlertNotification> Notifications => _notifications.AsReadOnly();

        public void Resolve(UserId resolvedBy, string resolution)
        {
            Status = AlertStatus.Resolved;
            ResolvedAt = DateTime.UtcNow;
            AddDomainEvent(new AlertResolvedEvent(Id, resolvedBy, resolution));
        }

        public void SendNotification(NotificationChannel channel, string recipient)
        {
            var notification = new AlertNotification(Id, channel, recipient);
            _notifications.Add(notification);
            AddDomainEvent(new AlertNotificationSentEvent(Id, channel, recipient));
        }
    }

    public class AlertRule : Entity
    {
        public AlertRuleId Id { get; private set; }
        public TenantId? TenantId { get; private set; } // null for platform rules
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string MetricQuery { get; private set; } // PromQL or similar
        public AlertCondition Condition { get; private set; }
        public TimeSpan EvaluationInterval { get; private set; }
        public TimeSpan AlertDuration { get; private set; }
        public AlertSeverity Severity { get; private set; }
        public bool IsEnabled { get; private set; }
        
        private readonly List<NotificationTarget> _notificationTargets = new();
        public IReadOnlyCollection<NotificationTarget> NotificationTargets => _notificationTargets.AsReadOnly();
    }
}
```

### 2.2 Metrics Collection Service

```csharp
namespace DriveOps.Observability.Application.Services
{
    public interface IMetricsCollectionService
    {
        Task CollectApplicationMetricsAsync(TenantId tenantId);
        Task CollectInfrastructureMetricsAsync(TenantId tenantId);
        Task CollectBusinessMetricsAsync(TenantId tenantId);
    }

    public class MetricsCollectionService : IMetricsCollectionService
    {
        private readonly IPrometheusClient _prometheusClient;
        private readonly IInfluxDBClient _influxClient;
        private readonly ISystemMetricRepository _metricRepository;
        private readonly IServiceProvider _serviceProvider;

        public async Task CollectApplicationMetricsAsync(TenantId tenantId)
        {
            var metrics = new List<SystemMetric>();

            // Collect HTTP request metrics
            var httpMetrics = await _prometheusClient.QueryAsync(
                $"http_requests_total{{tenant_id=\"{tenantId.Value}\"}}[5m]"
            );

            foreach (var sample in httpMetrics.Data.Result)
            {
                metrics.Add(new SystemMetric(
                    tenantId,
                    "http_requests_total",
                    "counter",
                    sample.Value,
                    sample.Metric,
                    "application"
                ));
            }

            // Collect memory and CPU metrics
            var memoryMetrics = await _prometheusClient.QueryAsync(
                $"process_resident_memory_bytes{{tenant_id=\"{tenantId.Value}\"}}"
            );

            foreach (var sample in memoryMetrics.Data.Result)
            {
                metrics.Add(new SystemMetric(
                    tenantId,
                    "memory_usage_bytes",
                    "gauge",
                    sample.Value,
                    sample.Metric,
                    "application"
                ));
            }

            await _metricRepository.BulkInsertAsync(metrics);
        }

        public async Task CollectBusinessMetricsAsync(TenantId tenantId)
        {
            var metrics = new List<SystemMetric>();

            // Get tenant-specific modules and collect their metrics
            var tenant = await GetTenantAsync(tenantId);
            
            foreach (var subscription in tenant.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active))
            {
                var moduleMetricsCollector = _serviceProvider
                    .GetRequiredService<IModuleMetricsCollector>(subscription.ModuleType.ToString());
                
                var moduleMetrics = await moduleMetricsCollector.CollectAsync(tenantId);
                metrics.AddRange(moduleMetrics);
            }

            await _metricRepository.BulkInsertAsync(metrics);
        }
    }

    // Module-specific metrics collectors
    public interface IModuleMetricsCollector
    {
        Task<List<SystemMetric>> CollectAsync(TenantId tenantId);
    }

    public class VehiclesModuleMetricsCollector : IModuleMetricsCollector
    {
        private readonly IVehicleRepository _vehicleRepository;

        public async Task<List<SystemMetric>> CollectAsync(TenantId tenantId)
        {
            var metrics = new List<SystemMetric>();

            // Total vehicles count
            var totalVehicles = await _vehicleRepository.CountByTenantAsync(tenantId);
            metrics.Add(new SystemMetric(
                tenantId,
                "vehicles_total",
                "gauge",
                totalVehicles,
                new Dictionary<string, string> { { "module", "vehicles" } },
                "business"
            ));

            // Active vehicles count
            var activeVehicles = await _vehicleRepository.CountActiveByTenantAsync(tenantId);
            metrics.Add(new SystemMetric(
                tenantId,
                "vehicles_active",
                "gauge",
                activeVehicles,
                new Dictionary<string, string> { { "module", "vehicles", "status", "active" } },
                "business"
            ));

            return metrics;
        }
    }
}
```

### 2.3 Centralized Logging

```csharp
namespace DriveOps.Observability.Infrastructure.Logging
{
    public interface ICentralizedLoggingService
    {
        Task IndexLogAsync(TenantId? tenantId, LogEntry logEntry);
        Task<List<LogEntry>> SearchLogsAsync(LogSearchQuery query);
        Task<LogAnalytics> AnalyzeLogsAsync(LogAnalyticsQuery query);
    }

    public class ElasticsearchLoggingService : ICentralizedLoggingService
    {
        private readonly IElasticClient _elasticClient;
        private readonly ILogger<ElasticsearchLoggingService> _logger;

        public async Task IndexLogAsync(TenantId? tenantId, LogEntry logEntry)
        {
            var indexName = tenantId.HasValue 
                ? $"driveops-logs-{tenantId.Value:N}-{DateTime.UtcNow:yyyy.MM}"
                : $"driveops-platform-logs-{DateTime.UtcNow:yyyy.MM}";

            var document = new
            {
                timestamp = logEntry.Timestamp,
                level = logEntry.Level,
                message = logEntry.Message,
                logger = logEntry.Logger,
                tenant_id = tenantId?.Value.ToString(),
                module = logEntry.Module,
                trace_id = logEntry.TraceId,
                span_id = logEntry.SpanId,
                user_id = logEntry.UserId?.ToString(),
                properties = logEntry.Properties,
                exception = logEntry.Exception != null ? new
                {
                    type = logEntry.Exception.GetType().Name,
                    message = logEntry.Exception.Message,
                    stack_trace = logEntry.Exception.StackTrace
                } : null
            };

            await _elasticClient.IndexAsync(document, i => i.Index(indexName));
        }

        public async Task<List<LogEntry>> SearchLogsAsync(LogSearchQuery query)
        {
            var searchResponse = await _elasticClient.SearchAsync<dynamic>(s => s
                .Index($"driveops-logs-{query.TenantId:N}-*")
                .Query(q => q
                    .Bool(b => b
                        .Must(BuildSearchQueries(query))
                    )
                )
                .Sort(sort => sort.Descending("timestamp"))
                .Size(query.PageSize)
                .From(query.PageNumber * query.PageSize)
            );

            return searchResponse.Documents
                .Select(MapToLogEntry)
                .ToList();
        }
    }

    // MongoDB alternative for log storage
    public class MongoLoggingService : ICentralizedLoggingService
    {
        private readonly IMongoDatabase _database;

        public async Task IndexLogAsync(TenantId? tenantId, LogEntry logEntry)
        {
            var collectionName = tenantId.HasValue 
                ? $"logs_tenant_{tenantId.Value:N}"
                : "logs_platform";

            var collection = _database.GetCollection<BsonDocument>(collectionName);
            
            var document = new BsonDocument
            {
                ["timestamp"] = logEntry.Timestamp,
                ["level"] = logEntry.Level,
                ["message"] = logEntry.Message,
                ["logger"] = logEntry.Logger,
                ["module"] = logEntry.Module,
                ["trace_id"] = logEntry.TraceId,
                ["span_id"] = logEntry.SpanId,
                ["user_id"] = logEntry.UserId?.ToString(),
                ["properties"] = BsonDocument.Parse(JsonSerializer.Serialize(logEntry.Properties)),
                ["exception"] = logEntry.Exception != null ? new BsonDocument
                {
                    ["type"] = logEntry.Exception.GetType().Name,
                    ["message"] = logEntry.Exception.Message,
                    ["stack_trace"] = logEntry.Exception.StackTrace
                } : BsonNull.Value
            };

            await collection.InsertOneAsync(document);
        }
    }
}
```

### 2.4 Alert Management System

```csharp
namespace DriveOps.Observability.Application.Services
{
    public interface IAlertService
    {
        Task EvaluateRulesAsync();
        Task TriggerAlertAsync(AlertRuleId ruleId, Dictionary<string, object> context);
        Task ResolveAlertAsync(AlertId alertId, UserId resolvedBy, string resolution);
    }

    public class AlertService : IAlertService
    {
        private readonly IAlertRuleRepository _alertRuleRepository;
        private readonly IAlertRepository _alertRepository;
        private readonly INotificationService _notificationService;
        private readonly IPrometheusClient _prometheusClient;

        public async Task EvaluateRulesAsync()
        {
            var activeRules = await _alertRuleRepository.GetActiveRulesAsync();

            var evaluationTasks = activeRules.Select(EvaluateRuleAsync);
            await Task.WhenAll(evaluationTasks);
        }

        private async Task EvaluateRuleAsync(AlertRule rule)
        {
            try
            {
                var queryResult = await _prometheusClient.QueryAsync(rule.MetricQuery);
                
                foreach (var sample in queryResult.Data.Result)
                {
                    if (rule.Condition.IsMet(sample.Value))
                    {
                        // Check if alert is already active
                        var existingAlert = await _alertRepository
                            .GetActiveAlertByRuleAsync(rule.Id, rule.TenantId);

                        if (existingAlert == null)
                        {
                            await TriggerAlertAsync(rule.Id, new Dictionary<string, object>
                            {
                                ["metric_value"] = sample.Value,
                                ["metric_labels"] = sample.Metric,
                                ["threshold"] = rule.Condition.Threshold
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log evaluation error
                _logger.LogError(ex, "Failed to evaluate alert rule {RuleId}", rule.Id);
            }
        }

        public async Task TriggerAlertAsync(AlertRuleId ruleId, Dictionary<string, object> context)
        {
            var rule = await _alertRuleRepository.GetByIdAsync(ruleId);
            if (rule == null) return;

            var alert = new Alert(
                ruleId,
                rule.TenantId,
                GenerateAlertTitle(rule, context),
                GenerateAlertDescription(rule, context),
                rule.Severity,
                context
            );

            await _alertRepository.AddAsync(alert);

            // Send notifications
            foreach (var target in rule.NotificationTargets)
            {
                await SendNotificationAsync(alert, target);
            }
        }

        private async Task SendNotificationAsync(Alert alert, NotificationTarget target)
        {
            switch (target.Channel)
            {
                case NotificationChannel.Email:
                    await _notificationService.SendEmailAlertAsync(alert, target.Address);
                    break;
                case NotificationChannel.Slack:
                    await _notificationService.SendSlackAlertAsync(alert, target.Address);
                    break;
                case NotificationChannel.Webhook:
                    await _notificationService.SendWebhookAlertAsync(alert, target.Address);
                    break;
            }

            alert.SendNotification(target.Channel, target.Address);
        }
    }
}
```

### 2.5 Health Checks System

```csharp
namespace DriveOps.Observability.Infrastructure.HealthChecks
{
    public class TenantHealthCheckService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TenantHealthCheckService> _logger;
        private Timer? _timer;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(PerformHealthChecks, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        private async void PerformHealthChecks(object? state)
        {
            using var scope = _serviceProvider.CreateScope();
            var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
            var healthCheckService = scope.ServiceProvider.GetRequiredService<IHealthCheckService>();

            var activeTenants = await tenantRepository.GetActiveTenantsAsync();

            var healthCheckTasks = activeTenants.Select(async tenant =>
            {
                var healthResult = await CheckTenantHealthAsync(tenant, healthCheckService);
                await RecordHealthCheckAsync(tenant.Id, healthResult);
            });

            await Task.WhenAll(healthCheckTasks);
        }

        private async Task<HealthCheckResult> CheckTenantHealthAsync(Tenant tenant, IHealthCheckService healthCheckService)
        {
            var checks = new List<IndividualHealthCheck>
            {
                await CheckDatabaseConnectivityAsync(tenant),
                await CheckApplicationResponseAsync(tenant),
                await CheckKeycloakConnectivityAsync(tenant),
                await CheckModuleHealthAsync(tenant)
            };

            var overallStatus = checks.All(c => c.Status == HealthStatus.Healthy) 
                ? HealthStatus.Healthy 
                : checks.Any(c => c.Status == HealthStatus.Unhealthy) 
                    ? HealthStatus.Unhealthy 
                    : HealthStatus.Degraded;

            return new HealthCheckResult(tenant.Id, overallStatus, checks);
        }

        private async Task<IndividualHealthCheck> CheckDatabaseConnectivityAsync(Tenant tenant)
        {
            try
            {
                var infrastructure = tenant.Infrastructure.FirstOrDefault();
                if (infrastructure == null)
                    return new IndividualHealthCheck("Database", HealthStatus.Unhealthy, "No infrastructure found");

                using var connection = new NpgsqlConnection(infrastructure.DatabaseConnectionString);
                await connection.OpenAsync();
                
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync();

                return new IndividualHealthCheck("Database", HealthStatus.Healthy, "Database connection successful");
            }
            catch (Exception ex)
            {
                return new IndividualHealthCheck("Database", HealthStatus.Unhealthy, ex.Message);
            }
        }

        private async Task<IndividualHealthCheck> CheckApplicationResponseAsync(Tenant tenant)
        {
            try
            {
                var infrastructure = tenant.Infrastructure.FirstOrDefault();
                if (infrastructure == null)
                    return new IndividualHealthCheck("Application", HealthStatus.Unhealthy, "No infrastructure found");

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                var response = await httpClient.GetAsync($"{infrastructure.ApplicationUrl}/health");
                
                if (response.IsSuccessStatusCode)
                {
                    var responseTime = response.Headers.Date?.Subtract(DateTime.UtcNow).TotalMilliseconds ?? 0;
                    return new IndividualHealthCheck("Application", HealthStatus.Healthy, 
                        $"Application responding in {responseTime:F2}ms");
                }
                else
                {
                    return new IndividualHealthCheck("Application", HealthStatus.Unhealthy, 
                        $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                return new IndividualHealthCheck("Application", HealthStatus.Unhealthy, ex.Message);
            }
        }
    }

    public record HealthCheckResult(
        TenantId TenantId,
        HealthStatus OverallStatus,
        List<IndividualHealthCheck> Checks
    );

    public record IndividualHealthCheck(
        string CheckName,
        HealthStatus Status,
        string Message
    );

    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }
}
```

### 2.6 Real-time Dashboards

```csharp
namespace DriveOps.Observability.Presentation.Hubs
{
    public class MetricsHub : Hub
    {
        private readonly IMetricsService _metricsService;

        public async Task JoinTenantGroup(string tenantId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
        }

        public async Task LeaveTenantGroup(string tenantId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
        }

        public async Task<DashboardData> GetDashboardData(string tenantId, string timeRange)
        {
            var tenantIdValue = TenantId.From(Guid.Parse(tenantId));
            return await _metricsService.GetDashboardDataAsync(tenantIdValue, timeRange);
        }
    }

    public class MetricsUpdateService : IHostedService
    {
        private readonly IHubContext<MetricsHub> _hubContext;
        private readonly IMetricsService _metricsService;
        private Timer? _timer;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(BroadcastMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        private async void BroadcastMetrics(object? state)
        {
            var activeTenants = await GetActiveTenantsAsync();
            
            foreach (var tenant in activeTenants)
            {
                var dashboardData = await _metricsService.GetRealtimeDashboardDataAsync(tenant.Id);
                
                await _hubContext.Clients.Group($"tenant_{tenant.Id}")
                    .SendAsync("MetricsUpdate", dashboardData);
            }
        }
    }
}
```

---

## 3. Database Schemas

### 3.1 PostgreSQL Schemas

```sql
-- Admin Module Tables
CREATE SCHEMA IF NOT EXISTS admin;

-- Tenants table
CREATE TABLE admin.tenants (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_name VARCHAR(255) NOT NULL,
    subdomain VARCHAR(100) NOT NULL UNIQUE,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Pending, 1: Active, 2: Suspended, 3: Cancelled
    contact_email VARCHAR(255) NOT NULL,
    contact_phone VARCHAR(50),
    contact_first_name VARCHAR(100) NOT NULL,
    contact_last_name VARCHAR(100) NOT NULL,
    contact_street VARCHAR(255),
    contact_city VARCHAR(100),
    contact_postal_code VARCHAR(20),
    contact_country VARCHAR(100),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    activated_at TIMESTAMP WITH TIME ZONE,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Tenant infrastructure table
CREATE TABLE admin.tenant_infrastructure (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    database_connection_string TEXT NOT NULL,
    mongo_connection_string TEXT NOT NULL,
    redis_connection_string TEXT NOT NULL,
    keycloak_realm VARCHAR(100) NOT NULL,
    application_url VARCHAR(500) NOT NULL,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Deploying, 1: Deployed, 2: Failed
    deployed_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Tenant subscriptions table
CREATE TABLE admin.tenant_subscriptions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    module_type INTEGER NOT NULL, -- 1: Users, 2: Vehicles, 3: Notifications, 4: Files, 10+: Business modules
    subscription_tier INTEGER NOT NULL, -- 1: Starter, 2: Professional, 3: Enterprise
    monthly_price DECIMAL(10,2) NOT NULL,
    start_date TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    end_date TIMESTAMP WITH TIME ZONE,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: Suspended, 3: Cancelled
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, module_type)
);

-- Billing invoices table
CREATE TABLE admin.billing_invoices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    invoice_number VARCHAR(50) NOT NULL UNIQUE,
    billing_period_start DATE NOT NULL,
    billing_period_end DATE NOT NULL,
    subtotal DECIMAL(10,2) NOT NULL,
    tax_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    total_amount DECIMAL(10,2) NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'USD',
    status INTEGER NOT NULL DEFAULT 0, -- 0: Draft, 1: Sent, 2: Paid, 3: Overdue, 4: Cancelled
    due_date DATE NOT NULL,
    paid_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Billing invoice items table
CREATE TABLE admin.billing_invoice_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    invoice_id UUID NOT NULL REFERENCES admin.billing_invoices(id) ON DELETE CASCADE,
    subscription_id UUID NOT NULL REFERENCES admin.tenant_subscriptions(id),
    description TEXT NOT NULL,
    quantity INTEGER NOT NULL DEFAULT 1,
    unit_price DECIMAL(10,2) NOT NULL,
    total_price DECIMAL(10,2) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Support tickets table
CREATE TABLE admin.support_tickets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    ticket_number VARCHAR(20) NOT NULL UNIQUE,
    subject VARCHAR(500) NOT NULL,
    description TEXT NOT NULL,
    priority INTEGER NOT NULL DEFAULT 2, -- 1: Low, 2: Medium, 3: High, 4: Critical
    status INTEGER NOT NULL DEFAULT 1, -- 1: Open, 2: In Progress, 3: Waiting, 4: Resolved, 5: Closed
    created_by UUID NOT NULL, -- Reference to user in tenant's system
    assigned_to UUID, -- Reference to support staff user
    resolved_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Support ticket comments table
CREATE TABLE admin.support_ticket_comments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id UUID NOT NULL REFERENCES admin.support_tickets(id) ON DELETE CASCADE,
    user_id UUID NOT NULL,
    content TEXT NOT NULL,
    is_internal BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Observability Module Tables
CREATE SCHEMA IF NOT EXISTS observability;

-- System metrics table
CREATE TABLE observability.system_metrics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID REFERENCES admin.tenants(id), -- NULL for platform metrics
    metric_name VARCHAR(255) NOT NULL,
    metric_type VARCHAR(50) NOT NULL, -- counter, gauge, histogram
    value DOUBLE PRECISION NOT NULL,
    labels JSONB NOT NULL DEFAULT '{}',
    source VARCHAR(50) NOT NULL, -- application, infrastructure, business
    timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Alert rules table
CREATE TABLE observability.alert_rules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID REFERENCES admin.tenants(id), -- NULL for platform rules
    name VARCHAR(255) NOT NULL,
    description TEXT,
    metric_query TEXT NOT NULL, -- PromQL or similar query
    condition_operator VARCHAR(20) NOT NULL, -- gt, lt, eq, ne, gte, lte
    condition_threshold DOUBLE PRECISION NOT NULL,
    evaluation_interval INTERVAL NOT NULL DEFAULT '1 minute',
    alert_duration INTERVAL NOT NULL DEFAULT '5 minutes',
    severity INTEGER NOT NULL, -- 1: Info, 2: Warning, 3: Critical
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Alert rule notification targets table
CREATE TABLE observability.alert_rule_notification_targets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_rule_id UUID NOT NULL REFERENCES observability.alert_rules(id) ON DELETE CASCADE,
    channel INTEGER NOT NULL, -- 1: Email, 2: Slack, 3: Webhook, 4: SMS
    address TEXT NOT NULL, -- email, slack webhook, etc.
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Alerts table
CREATE TABLE observability.alerts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_rule_id UUID NOT NULL REFERENCES observability.alert_rules(id),
    tenant_id UUID REFERENCES admin.tenants(id),
    title VARCHAR(500) NOT NULL,
    description TEXT NOT NULL,
    severity INTEGER NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: Resolved, 3: Suppressed
    context JSONB NOT NULL DEFAULT '{}',
    triggered_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    resolved_at TIMESTAMP WITH TIME ZONE,
    resolved_by UUID,
    resolution TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Alert notifications table
CREATE TABLE observability.alert_notifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_id UUID NOT NULL REFERENCES observability.alerts(id) ON DELETE CASCADE,
    channel INTEGER NOT NULL,
    recipient TEXT NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Pending, 2: Sent, 3: Failed, 4: Delivered
    sent_at TIMESTAMP WITH TIME ZONE,
    delivered_at TIMESTAMP WITH TIME ZONE,
    error_message TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Health checks table
CREATE TABLE observability.health_checks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id),
    check_name VARCHAR(100) NOT NULL,
    overall_status INTEGER NOT NULL, -- 1: Healthy, 2: Degraded, 3: Unhealthy
    checks JSONB NOT NULL, -- Array of individual check results
    response_time_ms INTEGER,
    checked_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Indexes for performance
CREATE INDEX idx_tenants_subdomain ON admin.tenants(subdomain);
CREATE INDEX idx_tenants_status ON admin.tenants(status);
CREATE INDEX idx_tenant_subscriptions_tenant_module ON admin.tenant_subscriptions(tenant_id, module_type);
CREATE INDEX idx_tenant_subscriptions_status ON admin.tenant_subscriptions(status);
CREATE INDEX idx_billing_invoices_tenant_id ON admin.billing_invoices(tenant_id);
CREATE INDEX idx_billing_invoices_status ON admin.billing_invoices(status);
CREATE INDEX idx_billing_invoices_due_date ON admin.billing_invoices(due_date);
CREATE INDEX idx_support_tickets_tenant_id ON admin.support_tickets(tenant_id);
CREATE INDEX idx_support_tickets_status ON admin.support_tickets(status);
CREATE INDEX idx_support_tickets_assigned_to ON admin.support_tickets(assigned_to);

CREATE INDEX idx_system_metrics_tenant_timestamp ON observability.system_metrics(tenant_id, timestamp DESC);
CREATE INDEX idx_system_metrics_name_timestamp ON observability.system_metrics(metric_name, timestamp DESC);
CREATE INDEX idx_system_metrics_labels ON observability.system_metrics USING GIN(labels);
CREATE INDEX idx_alert_rules_tenant_enabled ON observability.alert_rules(tenant_id, is_enabled);
CREATE INDEX idx_alerts_tenant_status ON observability.alerts(tenant_id, status);
CREATE INDEX idx_alerts_triggered_at ON observability.alerts(triggered_at DESC);
CREATE INDEX idx_health_checks_tenant_checked ON observability.health_checks(tenant_id, checked_at DESC);
```

### 3.2 MongoDB Schemas

```javascript
// MongoDB Collections for Centralized Logging

// logs_platform collection (for platform-wide logs)
db.logs_platform.createIndex({ "timestamp": -1 });
db.logs_platform.createIndex({ "level": 1, "timestamp": -1 });
db.logs_platform.createIndex({ "logger": 1, "timestamp": -1 });
db.logs_platform.createIndex({ "trace_id": 1 });

// logs_tenant_{tenant_id} collections (for tenant-specific logs)
// Note: Collections are created dynamically per tenant
// Example indexes for tenant log collections:
db.logs_tenant_sample.createIndex({ "timestamp": -1 });
db.logs_tenant_sample.createIndex({ "level": 1, "timestamp": -1 });
db.logs_tenant_sample.createIndex({ "module": 1, "timestamp": -1 });
db.logs_tenant_sample.createIndex({ "user_id": 1, "timestamp": -1 });
db.logs_tenant_sample.createIndex({ "trace_id": 1 });

// Example log document structure:
{
  "_id": ObjectId("..."),
  "timestamp": ISODate("2024-09-01T10:30:00.000Z"),
  "level": "ERROR",
  "message": "Failed to process vehicle registration",
  "logger": "DriveOps.Vehicles.Application.Handlers.RegisterVehicleHandler",
  "module": "vehicles",
  "trace_id": "4bf92f3577b34da6a3ce929d0e0e4736",
  "span_id": "00f067aa0ba902b7",
  "user_id": "550e8400-e29b-41d4-a716-446655440000",
  "properties": {
    "vehicle_id": "123e4567-e89b-12d3-a456-426614174000",
    "make": "Toyota",
    "model": "Camry",
    "error_code": "VEH_001"
  },
  "exception": {
    "type": "ValidationException",
    "message": "VIN number already exists",
    "stack_trace": "..."
  }
}

// metrics_aggregated collection (for pre-aggregated metrics)
db.metrics_aggregated.createIndex({ "tenant_id": 1, "timestamp": -1 });
db.metrics_aggregated.createIndex({ "metric_name": 1, "interval": 1, "timestamp": -1 });

// Example aggregated metrics document:
{
  "_id": ObjectId("..."),
  "tenant_id": "550e8400-e29b-41d4-a716-446655440000",
  "metric_name": "http_requests_total",
  "interval": "5m", // 1m, 5m, 1h, 1d
  "timestamp": ISODate("2024-09-01T10:30:00.000Z"),
  "value": 1250,
  "labels": {
    "method": "GET",
    "status": "200",
    "endpoint": "/api/vehicles"
  },
  "aggregation_type": "sum" // sum, avg, min, max, count
}

// audit_logs collection (for compliance and audit trails)
db.audit_logs.createIndex({ "tenant_id": 1, "timestamp": -1 });
db.audit_logs.createIndex({ "user_id": 1, "timestamp": -1 });
db.audit_logs.createIndex({ "action": 1, "timestamp": -1 });
db.audit_logs.createIndex({ "resource_type": 1, "resource_id": 1, "timestamp": -1 });

// Example audit log document:
{
  "_id": ObjectId("..."),
  "tenant_id": "550e8400-e29b-41d4-a716-446655440000",
  "user_id": "123e4567-e89b-12d3-a456-426614174000",
  "action": "UPDATE",
  "resource_type": "Vehicle",
  "resource_id": "789e0123-e89b-12d3-a456-426614174000",
  "timestamp": ISODate("2024-09-01T10:30:00.000Z"),
  "ip_address": "192.168.1.100",
  "user_agent": "Mozilla/5.0...",
  "changes": {
    "old_values": {
      "status": "active",
      "mileage": 25000
    },
    "new_values": {
      "status": "maintenance",
      "mileage": 25100
    }
  },
  "metadata": {
    "session_id": "abc123def456",
    "correlation_id": "xyz789uvw123"
  }
}
```

---

## Conclusion

This comprehensive documentation provides the foundation for implementing robust Admin and Observability modules in the DriveOps platform. The architecture follows modern software engineering principles including:

- **Domain Driven Design (DDD)** for clear separation of concerns
- **CQRS and MediatR** for scalable command/query handling
- **Multi-tenant isolation** at both application and data levels
- **Comprehensive monitoring** with metrics, logging, and alerting
- **Infrastructure as Code** approach for deployment automation
- **Real-time dashboards** for operational visibility

The provided database schemas support full multi-tenancy with proper indexing for performance, while the C# code examples demonstrate production-ready implementations following .NET best practices.

---

*Document created: 2024-09-01*  
*Last updated: 2024-09-01*