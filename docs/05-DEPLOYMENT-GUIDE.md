# Deployment Guide

This guide covers production deployment strategies, infrastructure setup, and operational procedures for the DriveOps platform.

---

## 🎯 Overview

DriveOps is designed as a cloud-native, multi-tenant SaaS platform with the following deployment characteristics:
- **One instance per client** for complete data isolation
- **Modular architecture** allowing selective feature deployment
- **Horizontal scaling** with container orchestration
- **Infrastructure as Code** for consistent deployments

---

## 🏗️ Architecture Overview

### Deployment Models

#### 1. Single-Tenant Deployment (Recommended)
Each client gets their own complete infrastructure stack:

```
Client A Infrastructure
├── Application Layer
│   ├── API Services (DriveOps.Api)
│   ├── Web Application (DriveOps.Web)
│   └── Background Services
├── Data Layer
│   ├── PostgreSQL Database
│   ├── MongoDB Instance
│   └── Redis Cache
├── Security Layer
│   ├── Keycloak Instance
│   └── SSL Certificates
└── Infrastructure
    ├── Load Balancer
    ├── Container Runtime
    └── Monitoring Stack
```

#### 2. Shared Infrastructure (Development/Staging)
For non-production environments:

```
Shared Development Infrastructure
├── Shared Services
│   ├── Keycloak (Multiple Realms)
│   ├── Monitoring Stack
│   └── CI/CD Pipeline
├── Tenant-Specific
│   ├── Application Instances
│   ├── Databases (Per Tenant)
│   └── File Storage
```

---

## 🚀 Deployment Options

### Cloud Providers

#### Azure (Recommended)
```yaml
# Azure resources for single tenant
Resource Group: driveops-{client-name}-{environment}
├── App Services
│   ├── driveops-api-{client}-{env}
│   └── driveops-web-{client}-{env}
├── Databases
│   ├── PostgreSQL Flexible Server
│   └── Cosmos DB (MongoDB API)
├── Storage
│   ├── Blob Storage (Files)
│   └── Redis Cache
├── Security
│   ├── Key Vault
│   ├── Application Gateway
│   └── Azure AD B2C (Optional)
└── Monitoring
    ├── Application Insights
    └── Log Analytics
```

#### AWS
```yaml
# AWS resources for single tenant
VPC: driveops-{client-name}-{environment}
├── Compute
│   ├── ECS/Fargate Services
│   └── Application Load Balancer
├── Databases
│   ├── RDS PostgreSQL
│   └── DocumentDB (MongoDB)
├── Storage
│   ├── S3 Buckets
│   └── ElastiCache Redis
├── Security
│   ├── Secrets Manager
│   ├── Certificate Manager
│   └── Cognito (Optional)
└── Monitoring
    ├── CloudWatch
    └── X-Ray
```

#### Google Cloud Platform
```yaml
# GCP resources for single tenant
Project: driveops-{client-name}-{environment}
├── Compute
│   ├── Cloud Run Services
│   └── Cloud Load Balancing
├── Databases
│   ├── Cloud SQL PostgreSQL
│   └── Firestore (MongoDB Compatible)
├── Storage
│   ├── Cloud Storage
│   └── Memorystore Redis
├── Security
│   ├── Secret Manager
│   ├── Cloud IAM
│   └── Identity Platform
└── Monitoring
    ├── Cloud Monitoring
    └── Cloud Trace
```

---

## 🐳 Container Deployment

### Docker Configuration

#### Multi-Stage Dockerfile
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/DriveOps.Api/DriveOps.Api.csproj", "src/DriveOps.Api/"]
COPY ["src/DriveOps.Shared/DriveOps.Shared.csproj", "src/DriveOps.Shared/"]
RUN dotnet restore "src/DriveOps.Api/DriveOps.Api.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/src/DriveOps.Api"
RUN dotnet build "DriveOps.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "DriveOps.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Copy published application
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl --fail http://localhost:8080/health || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "DriveOps.Api.dll"]
```

#### Docker Compose for Production
```yaml
version: '3.8'

services:
  driveops-api:
    image: driveops/api:${VERSION}
    container_name: driveops-api-${CLIENT_NAME}
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=${CLIENT_DB};Username=${DB_USER};Password=${DB_PASSWORD}
      - ConnectionStrings__MongoConnection=mongodb://mongo:27017/${CLIENT_DB}
      - Keycloak__Authority=http://keycloak:8080/realms/${CLIENT_REALM}
      - TenantId=${TENANT_ID}
    depends_on:
      - postgres
      - mongo
      - keycloak
    networks:
      - driveops-network
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  driveops-web:
    image: driveops/web:${VERSION}
    container_name: driveops-web-${CLIENT_NAME}
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ApiBaseUrl=http://driveops-api:8080
    depends_on:
      - driveops-api
    networks:
      - driveops-network
    restart: unless-stopped

  postgres:
    image: postgres:16
    container_name: postgres-${CLIENT_NAME}
    environment:
      - POSTGRES_DB=${CLIENT_DB}
      - POSTGRES_USER=${DB_USER}
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./backups:/backups
    networks:
      - driveops-network
    restart: unless-stopped

  mongo:
    image: mongo:7
    container_name: mongo-${CLIENT_NAME}
    environment:
      - MONGO_INITDB_DATABASE=${CLIENT_DB}
    volumes:
      - mongo_data:/data/db
      - ./backups:/backups
    networks:
      - driveops-network
    restart: unless-stopped

  keycloak:
    image: quay.io/keycloak/keycloak:23.0
    container_name: keycloak-${CLIENT_NAME}
    environment:
      - KEYCLOAK_ADMIN=${KC_ADMIN_USER}
      - KEYCLOAK_ADMIN_PASSWORD=${KC_ADMIN_PASSWORD}
      - KC_DB=postgres
      - KC_DB_URL=jdbc:postgresql://postgres:5432/${KC_DB_NAME}
      - KC_DB_USERNAME=${DB_USER}
      - KC_DB_PASSWORD=${DB_PASSWORD}
    command: ["start", "--optimized"]
    depends_on:
      - postgres
    networks:
      - driveops-network
    restart: unless-stopped

  nginx:
    image: nginx:alpine
    container_name: nginx-${CLIENT_NAME}
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
      - ./ssl:/etc/nginx/ssl
    depends_on:
      - driveops-web
      - driveops-api
    networks:
      - driveops-network
    restart: unless-stopped

volumes:
  postgres_data:
  mongo_data:

networks:
  driveops-network:
    driver: bridge
```

---

## ☸️ Kubernetes Deployment

### Helm Chart Structure
```
charts/driveops/
├── Chart.yaml
├── values.yaml
├── values-production.yaml
├── templates/
│   ├── deployment.yaml
│   ├── service.yaml
│   ├── ingress.yaml
│   ├── configmap.yaml
│   ├── secret.yaml
│   ├── hpa.yaml
│   └── pdb.yaml
└── charts/
    ├── postgresql/
    └── mongodb/
```

#### Main Deployment Template
```yaml
# templates/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "driveops.fullname" . }}-api
  labels:
    {{- include "driveops.labels" . | nindent 4 }}
    app.kubernetes.io/component: api
spec:
  replicas: {{ .Values.api.replicaCount }}
  selector:
    matchLabels:
      {{- include "driveops.selectorLabels" . | nindent 6 }}
      app.kubernetes.io/component: api
  template:
    metadata:
      labels:
        {{- include "driveops.selectorLabels" . | nindent 8 }}
        app.kubernetes.io/component: api
    spec:
      serviceAccountName: {{ include "driveops.serviceAccountName" . }}
      containers:
        - name: api
          image: "{{ .Values.api.image.repository }}:{{ .Values.api.image.tag }}"
          imagePullPolicy: {{ .Values.api.image.pullPolicy }}
          ports:
            - name: http
              containerPort: 8080
              protocol: TCP
          livenessProbe:
            httpGet:
              path: /health
              port: http
            initialDelaySeconds: 30
            periodSeconds: 30
          readinessProbe:
            httpGet:
              path: /health/ready
              port: http
            initialDelaySeconds: 5
            periodSeconds: 10
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: "Production"
            - name: ConnectionStrings__DefaultConnection
              valueFrom:
                secretKeyRef:
                  name: {{ include "driveops.fullname" . }}-secrets
                  key: postgres-connection-string
            - name: ConnectionStrings__MongoConnection
              valueFrom:
                secretKeyRef:
                  name: {{ include "driveops.fullname" . }}-secrets
                  key: mongo-connection-string
          resources:
            {{- toYaml .Values.api.resources | nindent 12 }}
```

#### Production Values
```yaml
# values-production.yaml
global:
  environment: production
  clientName: "acme-garage"
  tenantId: "550e8400-e29b-41d4-a716-446655440001"

api:
  replicaCount: 3
  image:
    repository: driveops/api
    tag: "1.0.0"
    pullPolicy: IfNotPresent
  resources:
    limits:
      cpu: 1000m
      memory: 2Gi
    requests:
      cpu: 500m
      memory: 1Gi
  autoscaling:
    enabled: true
    minReplicas: 3
    maxReplicas: 10
    targetCPUUtilizationPercentage: 70
    targetMemoryUtilizationPercentage: 80

web:
  replicaCount: 2
  image:
    repository: driveops/web
    tag: "1.0.0"
    pullPolicy: IfNotPresent
  resources:
    limits:
      cpu: 500m
      memory: 1Gi
    requests:
      cpu: 250m
      memory: 512Mi

postgresql:
  enabled: true
  auth:
    database: driveops_acme
    username: driveops
    existingSecret: postgres-credentials
  primary:
    persistence:
      enabled: true
      size: 100Gi
      storageClass: premium-ssd
    resources:
      limits:
        cpu: 2000m
        memory: 4Gi
      requests:
        cpu: 1000m
        memory: 2Gi

mongodb:
  enabled: true
  auth:
    enabled: false
  persistence:
    enabled: true
    size: 50Gi
    storageClass: premium-ssd
  resources:
    limits:
      cpu: 1000m
      memory: 2Gi
    requests:
      cpu: 500m
      memory: 1Gi

ingress:
  enabled: true
  className: nginx
  annotations:
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
  hosts:
    - host: acme.driveops.com
      paths:
        - path: /
          pathType: Prefix
          service: web
        - path: /api
          pathType: Prefix
          service: api
  tls:
    - secretName: driveops-tls
      hosts:
        - acme.driveops.com
```

### Deployment Commands
```bash
# Install/Upgrade with Helm
helm upgrade --install driveops-acme ./charts/driveops \
  --namespace driveops-acme \
  --create-namespace \
  --values ./charts/driveops/values-production.yaml \
  --set global.clientName=acme-garage \
  --set global.tenantId=550e8400-e29b-41d4-a716-446655440001

# Verify deployment
kubectl get pods -n driveops-acme
kubectl get services -n driveops-acme
kubectl get ingress -n driveops-acme

# Check application health
kubectl port-forward -n driveops-acme svc/driveops-acme-api 8080:80
curl http://localhost:8080/health
```

---

## 🔧 Infrastructure as Code

### Terraform Configuration

#### Azure Example
```hcl
# main.tf
terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = "driveops-${var.client_name}-${var.environment}"
  location = var.location

  tags = {
    Environment = var.environment
    Client      = var.client_name
    Project     = "DriveOps"
  }
}

# App Service Plan
resource "azurerm_service_plan" "main" {
  name                = "plan-driveops-${var.client_name}-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location           = azurerm_resource_group.main.location
  os_type            = "Linux"
  sku_name           = var.app_service_sku

  tags = azurerm_resource_group.main.tags
}

# PostgreSQL Server
resource "azurerm_postgresql_flexible_server" "main" {
  name                   = "psql-driveops-${var.client_name}-${var.environment}"
  resource_group_name    = azurerm_resource_group.main.name
  location              = azurerm_resource_group.main.location
  version               = "16"
  administrator_login    = var.db_admin_username
  administrator_password = var.db_admin_password
  zone                  = "1"

  storage_mb   = var.postgresql_storage_mb
  sku_name     = var.postgresql_sku

  backup_retention_days        = 35
  geo_redundant_backup_enabled = true

  tags = azurerm_resource_group.main.tags
}

# PostgreSQL Database
resource "azurerm_postgresql_flexible_server_database" "main" {
  name      = "driveops_${var.client_name}"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

# App Service for API
resource "azurerm_linux_web_app" "api" {
  name                = "app-driveops-api-${var.client_name}-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  location           = azurerm_service_plan.main.location
  service_plan_id    = azurerm_service_plan.main.id

  site_config {
    always_on = true
    
    application_stack {
      dotnet_version = "8.0"
    }

    health_check_path = "/health"
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT" = var.environment
    "ConnectionStrings__DefaultConnection" = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Database=${azurerm_postgresql_flexible_server_database.main.name};Username=${var.db_admin_username};Password=${var.db_admin_password};SSL Mode=Require;"
    "TenantId" = var.tenant_id
  }

  tags = azurerm_resource_group.main.tags
}

# Variables
variable "client_name" {
  description = "Name of the client"
  type        = string
}

variable "environment" {
  description = "Environment (dev, staging, prod)"
  type        = string
}

variable "tenant_id" {
  description = "Tenant ID for the client"
  type        = string
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "app_service_sku" {
  description = "App Service SKU"
  type        = string
  default     = "P1v3"
}

variable "postgresql_sku" {
  description = "PostgreSQL SKU"
  type        = string
  default     = "GP_Standard_D2s_v3"
}

variable "postgresql_storage_mb" {
  description = "PostgreSQL storage in MB"
  type        = number
  default     = 32768
}
```

#### Deployment Script
```bash
#!/bin/bash
# deploy.sh

CLIENT_NAME=$1
ENVIRONMENT=$2
TENANT_ID=$3

if [ -z "$CLIENT_NAME" ] || [ -z "$ENVIRONMENT" ] || [ -z "$TENANT_ID" ]; then
    echo "Usage: $0 <client_name> <environment> <tenant_id>"
    exit 1
fi

echo "Deploying DriveOps for client: $CLIENT_NAME, environment: $ENVIRONMENT"

# Initialize Terraform
terraform init

# Plan deployment
terraform plan \
    -var="client_name=$CLIENT_NAME" \
    -var="environment=$ENVIRONMENT" \
    -var="tenant_id=$TENANT_ID" \
    -out=tfplan

# Apply if plan looks good
read -p "Apply this plan? (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    terraform apply tfplan
    
    # Get outputs
    API_URL=$(terraform output -raw api_url)
    DB_HOST=$(terraform output -raw db_host)
    
    echo "Deployment complete!"
    echo "API URL: $API_URL"
    echo "Database Host: $DB_HOST"
else
    echo "Deployment cancelled"
fi
```

---

## 🚀 CI/CD Pipeline

### GitHub Actions Workflow

```yaml
# .github/workflows/deploy-production.yml
name: Production Deployment

on:
  push:
    branches: [main]
    tags: ['v*']
  workflow_dispatch:
    inputs:
      client_name:
        description: 'Client name for deployment'
        required: true
        type: string
      environment:
        description: 'Environment to deploy to'
        required: true
        type: choice
        options:
          - staging
          - production

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  build:
    runs-on: ubuntu-latest
    outputs:
      version: ${{ steps.version.outputs.version }}
      image-tag: ${{ steps.image.outputs.tags }}
    
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
          
      - name: Restore dependencies
        run: dotnet restore
        
      - name: Run tests
        run: dotnet test --no-restore --verbosity normal
        
      - name: Get version
        id: version
        run: |
          if [[ ${{ github.ref }} == refs/tags/* ]]; then
            VERSION=${GITHUB_REF#refs/tags/}
          else
            VERSION=${GITHUB_SHA::8}
          fi
          echo "version=$VERSION" >> $GITHUB_OUTPUT
          
      - name: Log in to Container Registry
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
          
      - name: Build and push Docker image
        uses: docker/build-push-action@v5
        with:
          context: .
          push: true
          tags: |
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}/api:${{ steps.version.outputs.version }}
            ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}/api:latest

  deploy-staging:
    needs: build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    environment: staging
    
    steps:
      - name: Deploy to Staging
        run: |
          echo "Deploying version ${{ needs.build.outputs.version }} to staging"
          # Add deployment commands here

  deploy-production:
    needs: build
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/')
    environment: production
    
    steps:
      - name: Deploy to Production
        run: |
          echo "Deploying version ${{ needs.build.outputs.version }} to production"
          # Add deployment commands here
```

---

## 📊 Monitoring and Observability

### Application Monitoring

#### Health Checks
```csharp
// Program.cs - Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres")
    .AddMongoDb(mongoConnectionString, name: "mongodb")
    .AddUrlGroup(new Uri($"{keycloakUrl}/health"), name: "keycloak")
    .AddCheck<TenantHealthCheck>("tenant-validation");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

#### Custom Health Check
```csharp
public class TenantHealthCheck : IHealthCheck
{
    private readonly ITenantService _tenantService;

    public TenantHealthCheck(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = context.Registration.Tags.FirstOrDefault(t => t.StartsWith("tenant:"))?.Substring(7);
            if (string.IsNullOrEmpty(tenantId))
                return HealthCheckResult.Healthy("No specific tenant validation required");

            var tenant = await _tenantService.GetByIdAsync(new TenantId(Guid.Parse(tenantId)));
            
            return tenant != null 
                ? HealthCheckResult.Healthy($"Tenant {tenantId} is active")
                : HealthCheckResult.Unhealthy($"Tenant {tenantId} not found or inactive");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Tenant validation failed", ex);
        }
    }
}
```

### Prometheus Metrics
```csharp
// Add Prometheus metrics
builder.Services.AddOpenTelemetry()
    .WithMetrics(builder =>
    {
        builder.AddPrometheusExporter();
        builder.AddMeter("DriveOps.Api");
        builder.AddAspNetCoreInstrumentation();
        builder.AddHttpClientInstrumentation();
    });

// Custom metrics
public class InterventionMetrics
{
    private static readonly Counter InterventionsCreated = Metrics
        .CreateCounter("driveops_interventions_created_total", "Total interventions created", "tenant_id");
        
    private static readonly Histogram InterventionDuration = Metrics
        .CreateHistogram("driveops_intervention_duration_hours", "Intervention duration in hours", "tenant_id");

    public void RecordInterventionCreated(string tenantId)
    {
        InterventionsCreated.WithLabels(tenantId).Inc();
    }

    public void RecordInterventionCompleted(string tenantId, double durationHours)
    {
        InterventionDuration.WithLabels(tenantId).Observe(durationHours);
    }
}
```

---

## 🔐 Security Configuration

### SSL/TLS Setup

#### Nginx Configuration
```nginx
# nginx.conf
server {
    listen 80;
    server_name *.driveops.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name ~^(?<client>.+)\.driveops\.com$;

    ssl_certificate /etc/nginx/ssl/$client.crt;
    ssl_certificate_key /etc/nginx/ssl/$client.key;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-RSA-AES256-GCM-SHA512:DHE-RSA-AES256-GCM-SHA512;
    ssl_prefer_server_ciphers off;

    # Security headers
    add_header Strict-Transport-Security "max-age=63072000" always;
    add_header X-Frame-Options DENY always;
    add_header X-Content-Type-Options nosniff always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # API routes
    location /api/ {
        proxy_pass http://driveops-api:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Web application
    location / {
        proxy_pass http://driveops-web:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Secret Management

#### Azure Key Vault Integration
```csharp
// Program.cs
if (builder.Environment.IsProduction())
{
    var keyVaultUrl = builder.Configuration["KeyVault:VaultUrl"];
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUrl),
        new DefaultAzureCredential());
}

// Use secrets in configuration
builder.Services.Configure<DatabaseOptions>(options =>
{
    options.ConnectionString = builder.Configuration["ConnectionStrings--DefaultConnection"];
    options.MongoConnectionString = builder.Configuration["ConnectionStrings--MongoConnection"];
});
```

---

## 📋 Deployment Checklist

### Pre-Deployment
- [ ] **Infrastructure provisioned** and tested
- [ ] **Database migrations** reviewed and tested
- [ ] **Environment variables** configured
- [ ] **SSL certificates** installed and valid
- [ ] **DNS records** configured
- [ ] **Monitoring** setup and alerts configured
- [ ] **Backup strategy** implemented
- [ ] **Disaster recovery** plan tested

### Deployment Process
- [ ] **Application built** and tested in CI/CD
- [ ] **Container images** pushed to registry
- [ ] **Database migrations** applied
- [ ] **Configuration** updated
- [ ] **Health checks** passing
- [ ] **Smoke tests** completed
- [ ] **Performance tests** passed
- [ ] **Security scan** completed

### Post-Deployment
- [ ] **Application accessible** via public URL
- [ ] **Authentication** working correctly
- [ ] **Core functionality** verified
- [ ] **Monitoring dashboards** showing healthy metrics
- [ ] **Logs** being collected properly
- [ ] **Backup jobs** running successfully
- [ ] **Documentation** updated
- [ ] **Team notified** of successful deployment

---

## 🔄 Maintenance and Updates

### Rolling Updates
```bash
# Kubernetes rolling update
kubectl set image deployment/driveops-api api=driveops/api:v1.1.0 -n driveops-client

# Monitor rollout
kubectl rollout status deployment/driveops-api -n driveops-client

# Rollback if needed
kubectl rollout undo deployment/driveops-api -n driveops-client
```

### Database Migrations
```bash
# Run migrations in production
kubectl exec -it deployment/driveops-api -n driveops-client -- \
  dotnet DriveOps.Migrator.dll --environment Production
```

### Backup and Recovery
```bash
# PostgreSQL backup
kubectl exec -it postgres-client -- \
  pg_dump -U driveops -d driveops_client > backup-$(date +%Y%m%d).sql

# MongoDB backup
kubectl exec -it mongo-client -- \
  mongodump --db driveops_client --out /backups/mongo-$(date +%Y%m%d)
```

---

**Ready for production deployment! Follow this guide for secure, scalable DriveOps deployments. 🚀**

---

*Last updated: 2024-12-19*  
*Version: 1.0.0*