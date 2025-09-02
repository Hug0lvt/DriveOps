# Core Modules Documentation

This document provides detailed technical information about the 6 core modules of the DriveOps platform. These modules form the foundation of every DriveOps tenant instance and provide essential functionality for the automotive industry SaaS platform.

## 1. Users & Permissions Module

### Overview
The Users & Permissions module is responsible for managing user accounts, authentication, authorization, and role-based access control within each tenant's DriveOps instance. This module integrates with Keycloak for enterprise-grade identity management and supports multi-tenant isolation.

### Features
- **Multi-tenant User Management**: Complete user lifecycle management with tenant isolation
- **Keycloak Integration**: SSO, RBAC, and OAuth 2.0/OpenID Connect authentication
- **Role-based Permissions**: Granular permission system across all modules
- **Profile Management**: User profiles, preferences, and settings
- **Audit Trail**: Complete user activity tracking for compliance
- **Session Management**: Secure session handling with timeout policies

### Multi-tenant Justification for tenant_id
The inclusion of `tenant_id` in user records serves critical architectural purposes:

1. **Multi-environment Support**: Tenants may have development, staging, and production environments requiring separate user spaces
2. **Data Isolation**: Ensures complete separation of user data between tenants for security and compliance
3. **Performance Optimization**: Enables tenant-specific database partitioning and indexing strategies
4. **Regulatory Compliance**: Supports GDPR, HIPAA, and automotive industry regulations requiring data segregation
5. **Backup and Recovery**: Allows tenant-specific backup policies and point-in-time recovery
6. **Scalability**: Enables horizontal scaling with tenant-aware load balancing

### Technical Architecture

#### Domain Layer
```csharp
namespace DriveOps.Users.Domain.Entities
{
    public class User : AggregateRoot
    {
        public UserId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string Email { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string? Phone { get; private set; }
        public Guid KeycloakUserId { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime? LastLoginAt { get; private set; }
        public DateTime? PasswordChangedAt { get; private set; }
        public UserPreferences Preferences { get; private set; }
        
        private readonly List<UserRole> _roles = new();
        public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

        public void AssignRole(string roleName, UserId assignedBy)
        {
            if (_roles.Any(r => r.RoleName == roleName && r.IsActive))
                throw new InvalidOperationException($"User already has role: {roleName}");

            var role = new UserRole(TenantId, Id, roleName, assignedBy);
            _roles.Add(role);
            AddDomainEvent(new UserRoleAssignedEvent(TenantId, Id, roleName));
        }

        public void UpdateLastLogin()
        {
            LastLoginAt = DateTime.UtcNow;
            AddDomainEvent(new UserLoggedInEvent(TenantId, Id));
        }
    }

    public class UserRole : Entity
    {
        public UserRoleId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public UserId UserId { get; private set; }
        public string RoleName { get; private set; }
        public UserId AssignedBy { get; private set; }
        public DateTime AssignedAt { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public bool IsActive { get; private set; }
    }

    public record UserPreferences(
        string Language,
        string TimeZone,
        string DateFormat,
        bool EmailNotifications,
        bool SmsNotifications,
        string Theme
    );
}
```

#### Database Schema
```sql
-- Users module schema
CREATE SCHEMA IF NOT EXISTS users;

-- Users table with tenant isolation
CREATE TABLE users.users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL, -- Foreign key to admin.tenants(id)
    email VARCHAR(255) NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    phone VARCHAR(50),
    keycloak_user_id UUID NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    last_login_at TIMESTAMP WITH TIME ZONE,
    password_changed_at TIMESTAMP WITH TIME ZONE,
    preferences JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    -- Tenant isolation constraints
    UNIQUE(tenant_id, email),
    UNIQUE(tenant_id, keycloak_user_id)
);

-- User roles table
CREATE TABLE users.user_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    user_id UUID NOT NULL REFERENCES users.users(id) ON DELETE CASCADE,
    role_name VARCHAR(100) NOT NULL,
    assigned_by UUID NOT NULL REFERENCES users.users(id),
    assigned_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMP WITH TIME ZONE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, user_id, role_name)
);

-- User sessions table for tracking
CREATE TABLE users.user_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    user_id UUID NOT NULL REFERENCES users.users(id) ON DELETE CASCADE,
    session_token VARCHAR(500) NOT NULL,
    ip_address INET,
    user_agent TEXT,
    started_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    last_activity_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- Indexes for performance
CREATE INDEX idx_users_tenant_email ON users.users(tenant_id, email);
CREATE INDEX idx_users_tenant_active ON users.users(tenant_id, is_active) WHERE is_active = TRUE;
CREATE INDEX idx_user_roles_tenant_user ON users.user_roles(tenant_id, user_id, is_active);
CREATE INDEX idx_user_sessions_tenant_user_active ON users.user_sessions(tenant_id, user_id, is_active);
```

### Keycloak Integration Details

#### Realm Configuration per Tenant
```csharp
namespace DriveOps.Users.Infrastructure.Identity
{
    public class KeycloakService : IIdentityService
    {
        private readonly KeycloakClient _keycloakClient;
        private readonly ITenantContext _tenantContext;

        public async Task<Result<User>> CreateUserAsync(CreateUserRequest request)
        {
            var realmName = $"driveops-{_tenantContext.TenantSubdomain}";
            
            var keycloakUser = new UserRepresentation
            {
                Username = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Enabled = true,
                EmailVerified = false,
                Attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["tenant_id"] = new[] { _tenantContext.TenantId },
                    ["phone"] = string.IsNullOrEmpty(request.Phone) ? 
                        Array.Empty<string>() : new[] { request.Phone }
                }
            };

            var keycloakUserId = await _keycloakClient.CreateUserAsync(realmName, keycloakUser);
            
            // Create user in our domain
            var user = new User(
                TenantId.From(Guid.Parse(_tenantContext.TenantId)),
                request.Email,
                request.FirstName,
                request.LastName,
                request.Phone,
                keycloakUserId
            );

            return Result.Success(user);
        }

        public async Task AssignRoleToUserAsync(Guid userId, string roleName)
        {
            var realmName = $"driveops-{_tenantContext.TenantSubdomain}";
            await _keycloakClient.AssignRealmRoleAsync(realmName, userId, roleName);
        }
    }
}
```

### Permission Matrix
```csharp
namespace DriveOps.Users.Domain.Permissions
{
    public static class UserPermissions
    {
        // Basic user operations
        public const string ReadUsers = "users:read";
        public const string WriteUsers = "users:write";
        public const string DeleteUsers = "users:delete";
        public const string ManageUsers = "users:manage";
        
        // Role management
        public const string ViewRoles = "users:view-roles";
        public const string AssignRoles = "users:assign-roles";
        public const string ManageRoles = "users:manage-roles";
        
        // Profile operations
        public const string ViewProfiles = "users:view-profiles";
        public const string EditOwnProfile = "users:edit-own-profile";
        public const string EditAllProfiles = "users:edit-all-profiles";
    }

    public static class RolePermissionMapping
    {
        public static readonly Dictionary<string, string[]> DefaultRolePermissions = new()
        {
            ["tenant-admin"] = new[]
            {
                UserPermissions.ManageUsers,
                UserPermissions.ManageRoles,
                UserPermissions.EditAllProfiles
            },
            ["user-manager"] = new[]
            {
                UserPermissions.ReadUsers,
                UserPermissions.WriteUsers,
                UserPermissions.AssignRoles,
                UserPermissions.ViewProfiles
            },
            ["regular-user"] = new[]
            {
                UserPermissions.ViewProfiles,
                UserPermissions.EditOwnProfile
            }
        };
    }
}
```

### API Endpoints
- `POST /api/users`: Create a new user account
- `GET /api/users/{id}`: Retrieve user profile information
- `PUT /api/users/{id}`: Update user profile
- `DELETE /api/users/{id}`: Deactivate user account
- `POST /api/users/{id}/roles`: Assign role to user
- `DELETE /api/users/{id}/roles/{roleName}`: Remove role from user
- `GET /api/users/{id}/permissions`: Get user permissions
- `POST /api/users/authenticate`: Authenticate user (OAuth flow)
- `POST /api/users/refresh`: Refresh authentication token

## 2. Vehicles Module

### Overview
The Vehicles module manages vehicle information, fleet tracking, and maintenance records within the DriveOps system. It maintains a separation between global reference data (brands/models) and tenant-specific vehicle instances.

### Features
- **Vehicle Registration**: Complete vehicle lifecycle management with VIN validation
- **Fleet Management**: Multi-vehicle tracking and organization
- **Maintenance Scheduling**: Preventive and reactive maintenance tracking
- **Status Management**: Real-time vehicle status and location tracking
- **Integration with Files Module**: Document management for registration, insurance, and maintenance records

### Architecture Principle: Global vs. Tenant-Specific Data

#### Global Reference Data (No tenant_id)
Vehicle brands and models are maintained as global reference data shared across all tenants:

**Justification for removing tenant_id from vehicle_brands:**
1. **Standardization**: Automotive brands are global entities (Toyota, Ford, BMW, etc.)
2. **Data Consistency**: Prevents duplicate brand entries across tenants
3. **Maintenance Efficiency**: Single point of maintenance for brand information
4. **Storage Optimization**: Reduces database size and improves performance
5. **Integration Readiness**: Enables easier integration with automotive APIs and services

#### Tenant-Specific Data (With tenant_id)
Individual vehicle instances belong to specific tenants and contain tenant_id for isolation.

### Technical Architecture

#### Domain Layer
```csharp
namespace DriveOps.Vehicles.Domain.Entities
{
    public class Vehicle : AggregateRoot
    {
        public VehicleId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string Vin { get; private set; }
        public string LicensePlate { get; private set; }
        public VehicleBrandId BrandId { get; private set; }
        public VehicleModelId? ModelId { get; private set; }
        public string? CustomModel { get; private set; } // For non-standard models
        public int Year { get; private set; }
        public string Color { get; private set; }
        public VehicleStatus Status { get; private set; }
        public int Mileage { get; private set; }
        public UserId OwnerId { get; private set; }
        public DateTime? PurchaseDate { get; private set; }
        public decimal? PurchasePrice { get; private set; }
        public FileId? RegistrationDocumentId { get; private set; } // Link to Files module
        public FileId? InsuranceDocumentId { get; private set; }
        
        private readonly List<MaintenanceRecord> _maintenanceRecords = new();
        public IReadOnlyCollection<MaintenanceRecord> MaintenanceRecords => _maintenanceRecords.AsReadOnly();

        public void UpdateMileage(int newMileage, UserId updatedBy)
        {
            if (newMileage < Mileage)
                throw new InvalidOperationException("New mileage cannot be less than current mileage");

            var previousMileage = Mileage;
            Mileage = newMileage;
            AddDomainEvent(new VehicleMileageUpdatedEvent(TenantId, Id, previousMileage, newMileage, updatedBy));
        }

        public void ScheduleMaintenance(MaintenanceType type, DateTime scheduledDate, UserId scheduledBy)
        {
            var maintenance = new MaintenanceRecord(TenantId, Id, type, scheduledDate, scheduledBy);
            _maintenanceRecords.Add(maintenance);
            AddDomainEvent(new MaintenanceScheduledEvent(TenantId, Id, type, scheduledDate));
        }

        public void AttachDocument(DocumentType documentType, FileId fileId)
        {
            switch (documentType)
            {
                case DocumentType.Registration:
                    RegistrationDocumentId = fileId;
                    break;
                case DocumentType.Insurance:
                    InsuranceDocumentId = fileId;
                    break;
            }
            AddDomainEvent(new VehicleDocumentAttachedEvent(TenantId, Id, documentType, fileId));
        }
    }

    // Global reference entity (no tenant_id)
    public class VehicleBrand : Entity
    {
        public VehicleBrandId Id { get; private set; }
        public string Name { get; private set; }
        public string Country { get; private set; }
        public FileId? LogoFileId { get; private set; } // Logo stored via Files module
        public bool IsActive { get; private set; }
        
        private readonly List<VehicleModel> _models = new();
        public IReadOnlyCollection<VehicleModel> Models => _models.AsReadOnly();

        public void UpdateLogo(FileId logoFileId)
        {
            LogoFileId = logoFileId;
            AddDomainEvent(new VehicleBrandLogoUpdatedEvent(Id, logoFileId));
        }
    }

    // Global reference entity (no tenant_id)
    public class VehicleModel : Entity
    {
        public VehicleModelId Id { get; private set; }
        public VehicleBrandId BrandId { get; private set; }
        public string Name { get; private set; }
        public VehicleCategory Category { get; private set; }
        public FuelType FuelType { get; private set; }
        public TransmissionType TransmissionType { get; private set; }
        public int? YearStart { get; private set; }
        public int? YearEnd { get; private set; }
        public bool IsActive { get; private set; }
    }

    public class MaintenanceRecord : Entity
    {
        public MaintenanceRecordId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public VehicleId VehicleId { get; private set; }
        public MaintenanceType Type { get; private set; }
        public string Description { get; private set; }
        public DateTime ScheduledDate { get; private set; }
        public DateTime? CompletedDate { get; private set; }
        public int? MileageAtService { get; private set; }
        public decimal? Cost { get; private set; }
        public string? ServiceProvider { get; private set; }
        public UserId CreatedBy { get; private set; }
        
        private readonly List<FileId> _attachedDocuments = new();
        public IReadOnlyCollection<FileId> AttachedDocuments => _attachedDocuments.AsReadOnly();
    }

    public enum VehicleStatus
    {
        Active = 1,
        Maintenance = 2,
        Inactive = 3,
        Sold = 4
    }

    public enum VehicleCategory
    {
        Sedan,
        SUV,
        Truck,
        Van,
        Motorcycle,
        Bus,
        Trailer
    }

    public enum FuelType
    {
        Gasoline,
        Diesel,
        Electric,
        Hybrid,
        PluginHybrid,
        Hydrogen
    }

    public enum DocumentType
    {
        Registration,
        Insurance,
        MaintenanceReport,
        Inspection
    }
}
```

#### Database Schema
```sql
-- Vehicles module schema
CREATE SCHEMA IF NOT EXISTS vehicles;

-- Global vehicle brands table (no tenant_id - shared across all tenants)
CREATE TABLE public.vehicle_brands (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    country VARCHAR(100),
    logo_file_id UUID, -- References files module
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Global vehicle models table (no tenant_id - shared across all tenants)
CREATE TABLE public.vehicle_models (
    id SERIAL PRIMARY KEY,
    brand_id INTEGER NOT NULL REFERENCES public.vehicle_brands(id),
    name VARCHAR(100) NOT NULL,
    category INTEGER NOT NULL, -- 1: Sedan, 2: SUV, 3: Truck, etc.
    fuel_type INTEGER NOT NULL, -- 1: Gasoline, 2: Diesel, 3: Electric, etc.
    transmission_type INTEGER NOT NULL, -- 1: Manual, 2: Automatic, 3: CVT
    year_start INTEGER,
    year_end INTEGER,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(brand_id, name)
);

-- Tenant-specific vehicles table (with tenant_id for isolation)
CREATE TABLE vehicles.vehicles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL, -- Foreign key to admin.tenants(id)
    vin VARCHAR(17) NOT NULL,
    license_plate VARCHAR(20) NOT NULL,
    brand_id INTEGER NOT NULL REFERENCES public.vehicle_brands(id),
    model_id INTEGER REFERENCES public.vehicle_models(id),
    custom_model VARCHAR(100), -- For models not in reference table
    year INTEGER NOT NULL,
    color VARCHAR(50),
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: Maintenance, 3: Inactive, 4: Sold
    mileage INTEGER DEFAULT 0,
    owner_id UUID NOT NULL, -- References users.users(id)
    purchase_date DATE,
    purchase_price DECIMAL(12,2),
    registration_document_id UUID, -- References files module
    insurance_document_id UUID, -- References files module
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    -- Tenant isolation constraints
    UNIQUE(tenant_id, vin),
    UNIQUE(tenant_id, license_plate),
    
    -- Ensure either model_id or custom_model is provided
    CHECK ((model_id IS NOT NULL) OR (custom_model IS NOT NULL AND custom_model != ''))
);

-- Maintenance records table (tenant-specific)
CREATE TABLE vehicles.maintenance_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    vehicle_id UUID NOT NULL REFERENCES vehicles.vehicles(id) ON DELETE CASCADE,
    type INTEGER NOT NULL, -- 1: Oil Change, 2: Tire Rotation, 3: Brake Service, etc.
    description TEXT,
    scheduled_date TIMESTAMP WITH TIME ZONE NOT NULL,
    completed_date TIMESTAMP WITH TIME ZONE,
    mileage_at_service INTEGER,
    cost DECIMAL(10,2),
    service_provider VARCHAR(255),
    next_service_due_date TIMESTAMP WITH TIME ZONE,
    next_service_due_mileage INTEGER,
    created_by UUID NOT NULL, -- References users.users(id)
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Junction table for maintenance documents
CREATE TABLE vehicles.maintenance_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    maintenance_record_id UUID NOT NULL REFERENCES vehicles.maintenance_records(id) ON DELETE CASCADE,
    file_id UUID NOT NULL, -- References files module
    document_type INTEGER NOT NULL, -- 1: Invoice, 2: Report, 3: Photos, etc.
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(maintenance_record_id, file_id)
);

-- Performance indexes
CREATE INDEX idx_vehicles_tenant_owner ON vehicles.vehicles(tenant_id, owner_id);
CREATE INDEX idx_vehicles_tenant_status ON vehicles.vehicles(tenant_id, status);
CREATE INDEX idx_vehicles_tenant_brand ON vehicles.vehicles(tenant_id, brand_id);
CREATE INDEX idx_vehicles_vin_hash ON vehicles.vehicles USING HASH(vin);
CREATE INDEX idx_vehicle_brands_name ON public.vehicle_brands(name) WHERE is_active = TRUE;
CREATE INDEX idx_vehicle_models_brand_name ON public.vehicle_models(brand_id, name) WHERE is_active = TRUE;
CREATE INDEX idx_maintenance_tenant_vehicle_date ON vehicles.maintenance_records(tenant_id, vehicle_id, scheduled_date DESC);
```

### Integration with Files Module

#### Document Management
```csharp
namespace DriveOps.Vehicles.Application.Services
{
    public interface IVehicleDocumentService
    {
        Task<Result> AttachDocumentAsync(VehicleId vehicleId, DocumentType documentType, 
            IFormFile file, UserId uploadedBy);
        Task<Result<Stream>> DownloadDocumentAsync(VehicleId vehicleId, DocumentType documentType);
        Task<Result> RemoveDocumentAsync(VehicleId vehicleId, DocumentType documentType, UserId removedBy);
        Task<Result<List<FileMetadata>>> GetVehicleDocumentsAsync(VehicleId vehicleId);
    }

    public class VehicleDocumentService : IVehicleDocumentService
    {
        private readonly IVehicleRepository _vehicleRepository;
        private readonly IFileService _fileService;
        private readonly ITenantContext _tenantContext;

        public async Task<Result> AttachDocumentAsync(
            VehicleId vehicleId, 
            DocumentType documentType, 
            IFormFile file, 
            UserId uploadedBy)
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
            if (vehicle == null)
                return Result.Failure("Vehicle not found");

            // Upload file via Files module
            var uploadResult = await _fileService.UploadFileAsync(new UploadFileRequest
            {
                TenantId = _tenantContext.TenantId,
                File = file,
                EntityType = "Vehicle",
                EntityId = vehicleId.Value.ToString(),
                Category = documentType.ToString(),
                UploadedBy = uploadedBy.Value.ToString(),
                Tags = new[] { "vehicle-document", documentType.ToString().ToLower() }
            });

            if (!uploadResult.IsSuccess)
                return Result.Failure($"File upload failed: {uploadResult.Error}");

            // Attach document to vehicle
            vehicle.AttachDocument(documentType, FileId.From(uploadResult.Value.FileId));
            await _vehicleRepository.UpdateAsync(vehicle);

            return Result.Success();
        }
    }
}
```

### API Endpoints
- `POST /api/vehicles`: Register a new vehicle
- `GET /api/vehicles/{id}`: Get vehicle details including maintenance history
- `PUT /api/vehicles/{id}`: Update vehicle information
- `DELETE /api/vehicles/{id}`: Deactivate vehicle
- `GET /api/vehicles`: List vehicles with filtering and pagination
- `POST /api/vehicles/{id}/maintenance`: Schedule maintenance
- `PUT /api/vehicles/{id}/maintenance/{maintenanceId}`: Update maintenance record
- `POST /api/vehicles/{id}/documents`: Upload vehicle document
- `GET /api/vehicles/{id}/documents`: Get vehicle documents
- `GET /api/vehicle-brands`: Get available vehicle brands
- `GET /api/vehicle-brands/{brandId}/models`: Get models for a brand

## 3. Notifications Module

### Overview
The Notifications module provides a comprehensive multi-channel notification system that handles email, SMS, push notifications, and real-time in-app messaging across all DriveOps modules.

### Features
- **Multi-channel Delivery**: Email, SMS, push notifications, and in-app messages
- **Template Management**: Configurable notification templates with variables
- **Real-time Notifications**: WebSocket-based real-time messaging via SignalR
- **Delivery Tracking**: Complete delivery status tracking with retry mechanisms
- **User Preferences**: Per-user notification preferences and opt-out management
- **Scheduling**: Delayed and scheduled notification delivery
- **Internationalization**: Multi-language template support

### Technical Architecture

#### Domain Layer
```csharp
namespace DriveOps.Notifications.Domain.Entities
{
    public class Notification : AggregateRoot
    {
        public NotificationId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string Title { get; private set; }
        public string Message { get; private set; }
        public NotificationType Type { get; private set; }
        public NotificationPriority Priority { get; private set; }
        public string? TemplateId { get; private set; }
        public Dictionary<string, string> Variables { get; private set; }
        public UserId? SenderId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ScheduledFor { get; private set; }
        public string? CorrelationId { get; private set; }
        
        private readonly List<NotificationRecipient> _recipients = new();
        public IReadOnlyCollection<NotificationRecipient> Recipients => _recipients.AsReadOnly();
        
        private readonly List<NotificationDelivery> _deliveries = new();
        public IReadOnlyCollection<NotificationDelivery> Deliveries => _deliveries.AsReadOnly();

        public void AddRecipient(UserId userId, NotificationChannel channel, string address)
        {
            var recipient = new NotificationRecipient(Id, userId, channel, address);
            _recipients.Add(recipient);
        }

        public void RecordDeliveryAttempt(NotificationChannel channel, string recipient, 
            DeliveryStatus status, string? errorMessage = null)
        {
            var delivery = new NotificationDelivery(Id, channel, recipient, status, errorMessage);
            _deliveries.Add(delivery);
            
            if (status == DeliveryStatus.Delivered)
            {
                AddDomainEvent(new NotificationDeliveredEvent(TenantId, Id, channel, recipient));
            }
            else if (status == DeliveryStatus.Failed)
            {
                AddDomainEvent(new NotificationFailedEvent(TenantId, Id, channel, recipient, errorMessage));
            }
        }
    }

    public class NotificationRecipient : Entity
    {
        public NotificationRecipientId Id { get; private set; }
        public NotificationId NotificationId { get; private set; }
        public UserId UserId { get; private set; }
        public NotificationChannel Channel { get; private set; }
        public string Address { get; private set; } // Email, phone, device token, etc.
        public bool IsRead { get; private set; }
        public DateTime? ReadAt { get; private set; }

        public void MarkAsRead()
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
        }
    }

    public class NotificationTemplate : Entity
    {
        public NotificationTemplateId Id { get; private set; }
        public TenantId? TenantId { get; private set; } // null for global templates
        public string Name { get; private set; }
        public string Subject { get; private set; }
        public string Body { get; private set; }
        public NotificationType Type { get; private set; }
        public string Language { get; private set; }
        public bool IsActive { get; private set; }
        public Dictionary<string, string> DefaultVariables { get; private set; }
    }

    public class UserNotificationPreferences : Entity
    {
        public UserNotificationPreferencesId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public UserId UserId { get; private set; }
        public bool EmailEnabled { get; private set; }
        public bool SmsEnabled { get; private set; }
        public bool PushEnabled { get; private set; }
        public bool InAppEnabled { get; private set; }
        public Dictionary<NotificationType, bool> TypePreferences { get; private set; }
        public string? QuietHoursStart { get; private set; }
        public string? QuietHoursEnd { get; private set; }
        public string TimeZone { get; private set; }
    }

    public enum NotificationChannel
    {
        Email = 1,
        SMS = 2,
        Push = 3,
        InApp = 4,
        Webhook = 5
    }

    public enum NotificationType
    {
        System = 1,
        VehicleRegistered = 2,
        MaintenanceReminder = 3,
        MaintenanceCompleted = 4,
        UserInvited = 5,
        PasswordReset = 6,
        AccountLocked = 7,
        BillingReminder = 8,
        SupportTicketUpdated = 9
    }

    public enum NotificationPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Urgent = 4
    }

    public enum DeliveryStatus
    {
        Pending = 1,
        Sending = 2,
        Delivered = 3,
        Failed = 4,
        Bounced = 5,
        Suppressed = 6
    }
}
```

#### Application Layer - Notification Service
```csharp
namespace DriveOps.Notifications.Application.Services
{
    public interface INotificationService
    {
        Task<Result> SendNotificationAsync(SendNotificationRequest request);
        Task<Result> SendBulkNotificationsAsync(SendBulkNotificationsRequest request);
        Task<Result> ScheduleNotificationAsync(ScheduleNotificationRequest request);
        Task<Result> MarkAsReadAsync(NotificationId notificationId, UserId userId);
        Task<Result<PagedResult<NotificationDto>>> GetUserNotificationsAsync(GetUserNotificationsQuery query);
    }

    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserNotificationPreferencesRepository _preferencesRepository;
        private readonly INotificationTemplateRepository _templateRepository;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly IPushNotificationService _pushService;
        private readonly IRealtimeNotificationService _realtimeService;
        private readonly ITenantContext _tenantContext;

        public async Task<Result> SendNotificationAsync(SendNotificationRequest request)
        {
            // Get or create notification
            var notification = await CreateNotificationAsync(request);
            
            // Get user preferences for each recipient
            var recipientTasks = request.RecipientIds.Select(async userId =>
            {
                var preferences = await _preferencesRepository.GetByUserIdAsync(userId);
                return new { UserId = userId, Preferences = preferences };
            });

            var recipients = await Task.WhenAll(recipientTasks);

            // Send via each enabled channel
            var deliveryTasks = new List<Task>();

            foreach (var recipient in recipients)
            {
                if (ShouldSendNotification(notification, recipient.Preferences))
                {
                    // Determine which channels to use
                    var channels = GetEnabledChannels(recipient.Preferences, request.PreferredChannels);
                    
                    foreach (var channel in channels)
                    {
                        deliveryTasks.Add(SendViaChannelAsync(notification, recipient.UserId, channel));
                    }
                }
            }

            await Task.WhenAll(deliveryTasks);
            return Result.Success();
        }

        private async Task SendViaChannelAsync(Notification notification, UserId userId, NotificationChannel channel)
        {
            try
            {
                switch (channel)
                {
                    case NotificationChannel.Email:
                        await SendEmailNotificationAsync(notification, userId);
                        break;
                    case NotificationChannel.SMS:
                        await SendSmsNotificationAsync(notification, userId);
                        break;
                    case NotificationChannel.Push:
                        await SendPushNotificationAsync(notification, userId);
                        break;
                    case NotificationChannel.InApp:
                        await SendInAppNotificationAsync(notification, userId);
                        break;
                }

                notification.RecordDeliveryAttempt(channel, userId.Value.ToString(), DeliveryStatus.Delivered);
            }
            catch (Exception ex)
            {
                notification.RecordDeliveryAttempt(channel, userId.Value.ToString(), 
                    DeliveryStatus.Failed, ex.Message);
            }
        }

        private async Task SendEmailNotificationAsync(Notification notification, UserId userId)
        {
            var user = await GetUserAsync(userId);
            
            await _emailService.SendEmailAsync(new EmailRequest
            {
                ToAddress = user.Email,
                Subject = notification.Title,
                Body = notification.Message,
                IsHtml = true,
                TenantId = _tenantContext.TenantId
            });
        }

        private async Task SendInAppNotificationAsync(Notification notification, UserId userId)
        {
            await _realtimeService.SendToUserAsync(userId.Value.ToString(), "NewNotification", new
            {
                Id = notification.Id.Value,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type.ToString(),
                Priority = notification.Priority.ToString(),
                CreatedAt = notification.CreatedAt
            });
        }
    }
}
```

#### Database Schema
```sql
-- Notifications module schema
CREATE SCHEMA IF NOT EXISTS notifications;

-- Notifications table
CREATE TABLE notifications.notifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    title VARCHAR(500) NOT NULL,
    message TEXT NOT NULL,
    type INTEGER NOT NULL, -- NotificationType enum
    priority INTEGER NOT NULL DEFAULT 2, -- NotificationPriority enum
    template_id VARCHAR(100),
    variables JSONB NOT NULL DEFAULT '{}',
    sender_id UUID, -- References users.users(id), null for system notifications
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    scheduled_for TIMESTAMP WITH TIME ZONE,
    correlation_id VARCHAR(100),
    is_sent BOOLEAN NOT NULL DEFAULT FALSE,
    sent_at TIMESTAMP WITH TIME ZONE
);

-- Notification recipients table
CREATE TABLE notifications.notification_recipients (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notification_id UUID NOT NULL REFERENCES notifications.notifications(id) ON DELETE CASCADE,
    user_id UUID NOT NULL, -- References users.users(id)
    channel INTEGER NOT NULL, -- NotificationChannel enum
    address VARCHAR(500) NOT NULL, -- Email, phone, device token, etc.
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    read_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Notification deliveries table (for tracking delivery status)
CREATE TABLE notifications.notification_deliveries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notification_id UUID NOT NULL REFERENCES notifications.notifications(id) ON DELETE CASCADE,
    channel INTEGER NOT NULL,
    recipient VARCHAR(500) NOT NULL,
    status INTEGER NOT NULL, -- DeliveryStatus enum
    error_message TEXT,
    attempted_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    delivered_at TIMESTAMP WITH TIME ZONE
);

-- Notification templates table
CREATE TABLE notifications.notification_templates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID, -- NULL for global templates
    name VARCHAR(100) NOT NULL,
    subject VARCHAR(500) NOT NULL,
    body TEXT NOT NULL,
    type INTEGER NOT NULL,
    language CHAR(5) NOT NULL DEFAULT 'en-US',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    default_variables JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, name, language)
);

-- User notification preferences table
CREATE TABLE notifications.user_notification_preferences (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    user_id UUID NOT NULL, -- References users.users(id)
    email_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    sms_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    push_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    in_app_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    type_preferences JSONB NOT NULL DEFAULT '{}', -- Per-notification-type preferences
    quiet_hours_start TIME,
    quiet_hours_end TIME,
    time_zone VARCHAR(50) NOT NULL DEFAULT 'UTC',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, user_id)
);

-- Performance indexes
CREATE INDEX idx_notifications_tenant_type ON notifications.notifications(tenant_id, type, created_at DESC);
CREATE INDEX idx_notifications_scheduled ON notifications.notifications(scheduled_for) WHERE scheduled_for IS NOT NULL;
CREATE INDEX idx_notification_recipients_user_unread ON notifications.notification_recipients(user_id, is_read, created_at DESC);
CREATE INDEX idx_notification_deliveries_status ON notifications.notification_deliveries(status, attempted_at DESC);
CREATE INDEX idx_notification_templates_tenant_active ON notifications.notification_templates(tenant_id, is_active) WHERE is_active = TRUE;
```

### Real-time Notifications with SignalR

#### SignalR Hub Implementation
```csharp
namespace DriveOps.Notifications.Infrastructure.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<NotificationHub> _logger;

        public async Task JoinTenantGroup()
        {
            var tenantId = _tenantContext.TenantId;
            var userId = Context.UserIdentifier;
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            
            _logger.LogInformation("User {UserId} joined tenant {TenantId} notification groups", 
                userId, tenantId);
        }

        public async Task MarkNotificationAsRead(string notificationId)
        {
            var userId = Context.UserIdentifier;
            // Handle marking notification as read
            await Clients.User(userId).SendAsync("NotificationRead", notificationId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            _logger.LogInformation("User {UserId} disconnected from notifications", userId);
            await base.OnDisconnectedAsync(exception);
        }
    }

    public interface IRealtimeNotificationService
    {
        Task SendToUserAsync(string userId, string method, object data);
        Task SendToTenantAsync(string tenantId, string method, object data);
        Task SendToGroupAsync(string groupName, string method, object data);
    }

    public class SignalRNotificationService : IRealtimeNotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public async Task SendToUserAsync(string userId, string method, object data)
        {
            await _hubContext.Clients.User(userId).SendAsync(method, data);
        }

        public async Task SendToTenantAsync(string tenantId, string method, object data)
        {
            await _hubContext.Clients.Group($"tenant_{tenantId}").SendAsync(method, data);
        }

        public async Task SendToGroupAsync(string groupName, string method, object data)
        {
            await _hubContext.Clients.Group(groupName).SendAsync(method, data);
        }
    }
}
```

### API Endpoints
- `POST /api/notifications/send`: Send notification to specific users
- `POST /api/notifications/broadcast`: Broadcast notification to all tenant users
- `GET /api/notifications`: Get user's notifications with pagination
- `PUT /api/notifications/{id}/read`: Mark notification as read
- `PUT /api/notifications/read-all`: Mark all notifications as read
- `GET /api/notifications/preferences`: Get user notification preferences
- `PUT /api/notifications/preferences`: Update user notification preferences
- `GET /api/notifications/templates`: Get available notification templates
- `POST /api/notifications/templates`: Create custom notification template
- `GET /api/notifications/stats`: Get notification delivery statistics

## 4. Files Module

### Overview
The Files module provides comprehensive file storage and management capabilities with intelligent storage routing, virus scanning, and optimization services. It supports both MongoDB GridFS for primary storage and MinIO for secondary/archive storage based on file size and access patterns.

### Features
- **Hybrid Storage Architecture**: MongoDB GridFS for frequent access, MinIO for archival
- **Intelligent Storage Routing**: Size-based and access-pattern-based storage decisions
- **Virus Scanning**: Integrated antivirus scanning for all uploaded files
- **File Optimization**: Automatic image compression and document optimization
- **Metadata Extraction**: OCR for documents, EXIF for images, metadata for videos
- **Thumbnail Generation**: Automatic thumbnail creation for images and videos
- **Access Control**: Fine-grained permissions and sharing capabilities
- **Versioning**: File version control and history tracking

### Storage Strategy

#### Storage Routing Logic
```
File Upload → Size Check
├── < 16MB → MongoDB GridFS (Primary Storage)
│   ├── Frequent Access
│   ├── Fast Retrieval
│   └── Integrated with Database
└── ≥ 16MB → MinIO Object Storage (Secondary Storage)
    ├── Cost-effective for Large Files
    ├── S3-compatible API
    └── Better for Archive/Backup
```

#### Access Pattern Optimization
- **Hot Data** (accessed within 30 days): MongoDB GridFS
- **Warm Data** (accessed within 90 days): Can migrate to MinIO
- **Cold Data** (accessed beyond 90 days): Automatic migration to MinIO

### Technical Architecture

#### Domain Layer
```csharp
namespace DriveOps.Files.Domain.Entities
{
    public class FileEntity : AggregateRoot
    {
        public FileId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string Filename { get; private set; }
        public string ContentType { get; private set; }
        public long Size { get; private set; }
        public string Hash { get; private set; } // SHA-256 for deduplication
        public StorageLocation StorageLocation { get; private set; }
        public string? GridFSFileId { get; private set; }
        public string? MinioBucket { get; private set; }
        public string? MinioKey { get; private set; }
        public string EntityType { get; private set; } // Vehicle, User, Maintenance, etc.
        public string EntityId { get; private set; }
        public UserId UploadedBy { get; private set; }
        public FileStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime LastAccessedAt { get; private set; }
        public int AccessCount { get; private set; }
        
        private readonly List<FileTag> _tags = new();
        public IReadOnlyCollection<FileTag> Tags => _tags.AsReadOnly();
        
        private readonly List<FileVersion> _versions = new();
        public IReadOnlyCollection<FileVersion> Versions => _versions.AsReadOnly();

        public FileMetadata Metadata { get; private set; }
        public VirusScanResult? VirusScanResult { get; private set; }

        public void UpdateVirusScanResult(VirusScanStatus status, string? details = null)
        {
            VirusScanResult = new VirusScanResult(status, details, DateTime.UtcNow);
            
            if (status == VirusScanStatus.Infected)
            {
                Status = FileStatus.Quarantined;
                AddDomainEvent(new FileQuarantinedEvent(TenantId, Id, details));
            }
            else if (status == VirusScanStatus.Clean)
            {
                Status = FileStatus.Available;
                AddDomainEvent(new FileAvailableEvent(TenantId, Id));
            }
        }

        public void RecordAccess()
        {
            LastAccessedAt = DateTime.UtcNow;
            AccessCount++;
            
            // Trigger migration logic if needed
            if (ShouldMigrateToHotStorage())
            {
                AddDomainEvent(new FileMigrationRequiredEvent(TenantId, Id, StorageLocation.GridFS));
            }
        }

        public void MigrateStorage(StorageLocation newLocation, string? newStorageId)
        {
            var oldLocation = StorageLocation;
            StorageLocation = newLocation;

            if (newLocation == StorageLocation.GridFS)
            {
                GridFSFileId = newStorageId;
                MinioBucket = null;
                MinioKey = null;
            }
            else if (newLocation == StorageLocation.MinIO)
            {
                var parts = newStorageId?.Split('/');
                MinioBucket = parts?[0];
                MinioKey = string.Join('/', parts?.Skip(1) ?? Array.Empty<string>());
                GridFSFileId = null;
            }

            AddDomainEvent(new FileMigratedEvent(TenantId, Id, oldLocation, newLocation));
        }

        private bool ShouldMigrateToHotStorage()
        {
            return StorageLocation == StorageLocation.MinIO && 
                   AccessCount > 10 && 
                   DateTime.UtcNow.Subtract(LastAccessedAt).TotalDays < 7;
        }
    }

    public class FileMetadata : ValueObject
    {
        public Dictionary<string, object> Properties { get; }
        public string? ThumbnailFileId { get; }
        public string? OcrText { get; }
        public ImageMetadata? ImageMetadata { get; }
        public VideoMetadata? VideoMetadata { get; }
        public DocumentMetadata? DocumentMetadata { get; }

        public FileMetadata(
            Dictionary<string, object> properties,
            string? thumbnailFileId = null,
            string? ocrText = null,
            ImageMetadata? imageMetadata = null,
            VideoMetadata? videoMetadata = null,
            DocumentMetadata? documentMetadata = null)
        {
            Properties = properties ?? new Dictionary<string, object>();
            ThumbnailFileId = thumbnailFileId;
            OcrText = ocrText;
            ImageMetadata = imageMetadata;
            VideoMetadata = videoMetadata;
            DocumentMetadata = documentMetadata;
        }
    }

    public record ImageMetadata(
        int Width,
        int Height,
        string ColorSpace,
        int DPI,
        DateTime? DateTaken,
        string? CameraModel,
        GeoLocation? Location
    );

    public record VideoMetadata(
        TimeSpan Duration,
        int Width,
        int Height,
        string Codec,
        long Bitrate,
        double FrameRate
    );

    public record DocumentMetadata(
        int PageCount,
        string? Author,
        string? Title,
        DateTime? CreatedDate,
        DateTime? ModifiedDate,
        bool IsPasswordProtected
    );

    public record VirusScanResult(
        VirusScanStatus Status,
        string? Details,
        DateTime ScannedAt
    );

    public enum StorageLocation
    {
        GridFS = 1,
        MinIO = 2
    }

    public enum FileStatus
    {
        Processing = 1,
        Available = 2,
        Quarantined = 3,
        Archived = 4,
        Deleted = 5
    }

    public enum VirusScanStatus
    {
        Pending = 1,
        Scanning = 2,
        Clean = 3,
        Infected = 4,
        Error = 5
    }
}
```

#### Application Layer - File Service
```csharp
namespace DriveOps.Files.Application.Services
{
    public interface IFileService
    {
        Task<Result<FileUploadResult>> UploadFileAsync(UploadFileRequest request);
        Task<Result<Stream>> DownloadFileAsync(FileId fileId);
        Task<Result<FileMetadata>> GetFileMetadataAsync(FileId fileId);
        Task<Result> DeleteFileAsync(FileId fileId, UserId deletedBy);
        Task<Result<List<FileEntity>>> GetFilesByEntityAsync(string entityType, string entityId);
        Task<Result<Stream>> GetThumbnailAsync(FileId fileId);
    }

    public class FileService : IFileService
    {
        private readonly IFileRepository _fileRepository;
        private readonly IGridFSStorage _gridFSStorage;
        private readonly IMinIOStorage _minioStorage;
        private readonly IVirusScanService _virusScanService;
        private readonly IFileOptimizationService _optimizationService;
        private readonly IMetadataExtractionService _metadataService;
        private readonly ITenantContext _tenantContext;

        public async Task<Result<FileUploadResult>> UploadFileAsync(UploadFileRequest request)
        {
            try
            {
                // Calculate file hash for deduplication
                var hash = await CalculateFileHashAsync(request.FileStream);
                
                // Check for existing file with same hash
                var existingFile = await _fileRepository.GetByHashAsync(hash, request.TenantId);
                if (existingFile != null)
                {
                    return Result.Success(new FileUploadResult 
                    { 
                        FileId = existingFile.Id, 
                        IsDuplicate = true 
                    });
                }

                // Determine storage location based on file size
                var storageLocation = DetermineStorageLocation(request.FileStream.Length);
                
                // Create file entity
                var fileEntity = new FileEntity(
                    TenantId.From(Guid.Parse(request.TenantId)),
                    request.Filename,
                    request.ContentType,
                    request.FileStream.Length,
                    hash,
                    storageLocation,
                    request.EntityType,
                    request.EntityId,
                    UserId.From(Guid.Parse(request.UploadedBy))
                );

                // Upload to appropriate storage
                string storageId;
                if (storageLocation == StorageLocation.GridFS)
                {
                    storageId = await _gridFSStorage.UploadAsync(
                        request.FileStream, 
                        request.Filename, 
                        request.TenantId
                    );
                    fileEntity.SetGridFSFileId(storageId);
                }
                else
                {
                    var bucket = $"tenant-{request.TenantId}";
                    var key = $"{request.EntityType}/{request.EntityId}/{fileEntity.Id}_{request.Filename}";
                    await _minioStorage.UploadAsync(request.FileStream, bucket, key);
                    fileEntity.SetMinIOLocation(bucket, key);
                }

                // Save file entity
                await _fileRepository.AddAsync(fileEntity);

                // Queue background processing
                await QueueBackgroundProcessingAsync(fileEntity.Id);

                return Result.Success(new FileUploadResult 
                { 
                    FileId = fileEntity.Id, 
                    IsDuplicate = false 
                });
            }
            catch (Exception ex)
            {
                return Result.Failure<FileUploadResult>($"File upload failed: {ex.Message}");
            }
        }

        private StorageLocation DetermineStorageLocation(long fileSize)
        {
            // Use GridFS for files under 16MB, MinIO for larger files
            return fileSize < 16 * 1024 * 1024 ? StorageLocation.GridFS : StorageLocation.MinIO;
        }

        private async Task QueueBackgroundProcessingAsync(FileId fileId)
        {
            // Queue virus scanning
            await _virusScanService.QueueScanAsync(fileId);
            
            // Queue metadata extraction
            await _metadataService.QueueExtractionAsync(fileId);
            
            // Queue optimization (if applicable)
            await _optimizationService.QueueOptimizationAsync(fileId);
        }

        public async Task<Result<Stream>> DownloadFileAsync(FileId fileId)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null)
                return Result.Failure<Stream>("File not found");

            if (file.Status == FileStatus.Quarantined)
                return Result.Failure<Stream>("File is quarantined");

            // Record access for analytics and migration decisions
            file.RecordAccess();
            await _fileRepository.UpdateAsync(file);

            // Download from appropriate storage
            if (file.StorageLocation == StorageLocation.GridFS)
            {
                var stream = await _gridFSStorage.DownloadAsync(file.GridFSFileId!);
                return Result.Success(stream);
            }
            else
            {
                var stream = await _minioStorage.DownloadAsync(file.MinioBucket!, file.MinioKey!);
                return Result.Success(stream);
            }
        }
    }
}
```

#### Database Schema
```sql
-- Files module schema
CREATE SCHEMA IF NOT EXISTS files;

-- File metadata table (PostgreSQL for structured queries)
CREATE TABLE files.file_metadata (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    filename VARCHAR(500) NOT NULL,
    content_type VARCHAR(200) NOT NULL,
    size BIGINT NOT NULL,
    hash VARCHAR(64) NOT NULL, -- SHA-256 hash for deduplication
    storage_location INTEGER NOT NULL, -- 1: GridFS, 2: MinIO
    gridfs_file_id VARCHAR(100), -- MongoDB ObjectId as string
    minio_bucket VARCHAR(100),
    minio_key VARCHAR(500),
    entity_type VARCHAR(100) NOT NULL, -- Vehicle, User, Maintenance, etc.
    entity_id VARCHAR(100) NOT NULL,
    uploaded_by UUID NOT NULL, -- References users.users(id)
    status INTEGER NOT NULL DEFAULT 1, -- 1: Processing, 2: Available, 3: Quarantined, 4: Archived, 5: Deleted
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    last_accessed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    access_count INTEGER NOT NULL DEFAULT 0,
    
    -- Metadata fields (can be null until processing is complete)
    thumbnail_file_id UUID, -- Self-reference for thumbnails
    ocr_text TEXT,
    image_width INTEGER,
    image_height INTEGER,
    video_duration_seconds INTEGER,
    document_page_count INTEGER,
    
    -- Virus scan results
    virus_scan_status INTEGER, -- 1: Pending, 2: Scanning, 3: Clean, 4: Infected, 5: Error
    virus_scan_details TEXT,
    virus_scanned_at TIMESTAMP WITH TIME ZONE,
    
    -- Storage constraints
    CHECK ((storage_location = 1 AND gridfs_file_id IS NOT NULL) OR 
           (storage_location = 2 AND minio_bucket IS NOT NULL AND minio_key IS NOT NULL))
);

-- File tags table (for categorization and search)
CREATE TABLE files.file_tags (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    file_id UUID NOT NULL REFERENCES files.file_metadata(id) ON DELETE CASCADE,
    tag VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(file_id, tag)
);

-- File versions table (for version control)
CREATE TABLE files.file_versions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    file_id UUID NOT NULL REFERENCES files.file_metadata(id) ON DELETE CASCADE,
    version_number INTEGER NOT NULL,
    storage_location INTEGER NOT NULL,
    gridfs_file_id VARCHAR(100),
    minio_bucket VARCHAR(100),
    minio_key VARCHAR(500),
    size BIGINT NOT NULL,
    hash VARCHAR(64) NOT NULL,
    created_by UUID NOT NULL, -- References users.users(id)
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    is_current BOOLEAN NOT NULL DEFAULT FALSE,
    UNIQUE(file_id, version_number)
);

-- File shares table (for sharing and permissions)
CREATE TABLE files.file_shares (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    file_id UUID NOT NULL REFERENCES files.file_metadata(id) ON DELETE CASCADE,
    shared_with_user_id UUID, -- References users.users(id), null for public shares
    shared_by_user_id UUID NOT NULL, -- References users.users(id)
    permission_level INTEGER NOT NULL, -- 1: View, 2: Download, 3: Edit
    expires_at TIMESTAMP WITH TIME ZONE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Performance indexes
CREATE INDEX idx_file_metadata_tenant_entity ON files.file_metadata(tenant_id, entity_type, entity_id);
CREATE INDEX idx_file_metadata_tenant_status ON files.file_metadata(tenant_id, status);
CREATE INDEX idx_file_metadata_hash ON files.file_metadata(hash); -- For deduplication
CREATE INDEX idx_file_metadata_access_pattern ON files.file_metadata(last_accessed_at, access_count);
CREATE INDEX idx_file_metadata_virus_scan ON files.file_metadata(virus_scan_status) WHERE virus_scan_status IN (1, 2);
CREATE INDEX idx_file_tags_tag ON files.file_tags(tag);
CREATE INDEX idx_file_versions_current ON files.file_versions(file_id, is_current) WHERE is_current = TRUE;
CREATE INDEX idx_file_shares_user_active ON files.file_shares(shared_with_user_id, is_active) WHERE is_active = TRUE;

-- Full-text search index for OCR text
CREATE INDEX idx_file_metadata_ocr_text ON files.file_metadata USING GIN(to_tsvector('english', ocr_text));
```

#### MongoDB Collections
```javascript
// MongoDB collections for file storage and metadata

// GridFS collections (created automatically by MongoDB)
// fs.files - File metadata for GridFS
// fs.chunks - File data chunks for GridFS

// Additional indexes for GridFS
db.fs.files.createIndex({ "metadata.tenant_id": 1, "metadata.entity_type": 1 });
db.fs.files.createIndex({ "uploadDate": -1 });
db.fs.files.createIndex({ "metadata.hash": 1 }); // For deduplication

// Extended file metadata collection (for complex metadata that doesn't fit in PostgreSQL)
db.file_extended_metadata.createIndex({ "tenant_id": 1, "file_id": 1 });
db.file_extended_metadata.createIndex({ "tenant_id": 1, "entity_type": 1, "entity_id": 1 });

// Example extended metadata document
{
  "_id": ObjectId("..."),
  "tenant_id": "550e8400-e29b-41d4-a716-446655440000",
  "file_id": "123e4567-e89b-12d3-a456-426614174000",
  "entity_type": "Vehicle",
  "entity_id": "789e0123-e89b-12d3-a456-426614174000",
  "extracted_metadata": {
    "exif_data": {
      "camera_make": "Canon",
      "camera_model": "EOS R5",
      "focal_length": "85mm",
      "iso": 400,
      "aperture": "f/2.8",
      "gps_coordinates": {
        "latitude": 48.8566,
        "longitude": 2.3522
      }
    },
    "document_properties": {
      "author": "John Doe",
      "creation_date": ISODate("2024-01-15T10:30:00Z"),
      "modification_date": ISODate("2024-01-15T14:45:00Z"),
      "keywords": ["vehicle", "registration", "official"],
      "language": "en-US"
    }
  },
  "processing_history": [
    {
      "operation": "virus_scan",
      "timestamp": ISODate("2024-09-01T10:30:00Z"),
      "result": "clean",
      "scanner": "ClamAV"
    },
    {
      "operation": "ocr_extraction",
      "timestamp": ISODate("2024-09-01T10:32:00Z"),
      "result": "success",
      "text_length": 1247
    },
    {
      "operation": "thumbnail_generation",
      "timestamp": ISODate("2024-09-01T10:33:00Z"),
      "result": "success",
      "thumbnail_sizes": ["150x150", "300x300"]
    }
  ],
  "created_at": ISODate("2024-09-01T10:30:00Z"),
  "updated_at": ISODate("2024-09-01T10:33:00Z")
}
```

### Background Processing Services

#### Virus Scanning Service
```csharp
namespace DriveOps.Files.Infrastructure.Services
{
    public interface IVirusScanService
    {
        Task QueueScanAsync(FileId fileId);
        Task<VirusScanResult> ScanFileAsync(Stream fileStream);
    }

    public class ClamAVVirusScanService : IVirusScanService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IClamAVClient _clamAVClient;

        public async Task QueueScanAsync(FileId fileId)
        {
            _taskQueue.QueueBackgroundWorkItem(async token =>
            {
                await ProcessVirusScanAsync(fileId, token);
            });
        }

        private async Task ProcessVirusScanAsync(FileId fileId, CancellationToken cancellationToken)
        {
            var file = await _fileRepository.GetByIdAsync(fileId);
            if (file == null) return;

            file.UpdateVirusScanResult(VirusScanStatus.Scanning);
            await _fileRepository.UpdateAsync(file);

            try
            {
                using var fileStream = await GetFileStreamAsync(file);
                var scanResult = await ScanFileAsync(fileStream);
                
                file.UpdateVirusScanResult(scanResult.Status, scanResult.Details);
                await _fileRepository.UpdateAsync(file);
            }
            catch (Exception ex)
            {
                file.UpdateVirusScanResult(VirusScanStatus.Error, ex.Message);
                await _fileRepository.UpdateAsync(file);
            }
        }
    }
}
```

### API Endpoints
- `POST /api/files/upload`: Upload a new file
- `GET /api/files/{id}/download`: Download file content
- `GET /api/files/{id}/metadata`: Get file metadata
- `GET /api/files/{id}/thumbnail`: Get file thumbnail
- `DELETE /api/files/{id}`: Delete file
- `GET /api/files/entity/{entityType}/{entityId}`: Get files for an entity
- `POST /api/files/{id}/share`: Share file with user
- `GET /api/files/search`: Search files by content/metadata
- `POST /api/files/{id}/versions`: Upload new version of file
- `GET /api/files/{id}/versions`: Get file version history

## 5. Admin & SaaS Management Module

### Overview
The Admin & SaaS Management module provides comprehensive tenant lifecycle management, subscription billing, deployment automation, and support capabilities. This module is covered in detail in the [Admin & Observability Modules Documentation](./04-ADMIN-OBSERVABILITY-MODULES.md).

### Key Features
- **Tenant Provisioning**: Automated tenant setup and configuration
- **Subscription Management**: Module-based billing and subscription control
- **Deployment Automation**: Docker/Kubernetes orchestrated deployments
- **Support System**: Integrated ticket management and customer support
- **Multi-tenant Administration**: Central management of all tenant instances

### API Endpoints
- `POST /api/admin/tenants`: Create new tenant
- `GET /api/admin/tenants`: List all tenants
- `POST /api/admin/tenants/{id}/deploy`: Deploy tenant infrastructure
- `GET /api/admin/billing/invoices`: Get billing information
- `POST /api/admin/support/tickets`: Create support ticket

---

## 6. Observability & Monitoring Module

### Overview
The Observability & Monitoring module provides comprehensive system monitoring, metrics collection, alerting, and logging capabilities across all DriveOps modules. This module is covered in detail in the [Admin & Observability Modules Documentation](./04-ADMIN-OBSERVABILITY-MODULES.md).

### Key Features
- **Metrics Collection**: Prometheus/InfluxDB integration for system and business metrics
- **Centralized Logging**: Elasticsearch/MongoDB for log aggregation and search
- **Alert Management**: Configurable alerts with multi-channel notifications
- **Health Monitoring**: Real-time health checks for all system components
- **Performance Dashboards**: Real-time performance monitoring and analytics

### API Endpoints
- `GET /api/observability/metrics`: Get system metrics
- `GET /api/observability/logs`: Search system logs
- `POST /api/observability/alerts`: Create alert rule
- `GET /api/observability/health`: Get system health status

---

## Module Relationships and Dependencies

### Dependency Graph
```
┌─────────────────────────────────────────────────────────────┐
│                    CORE MODULES                             │
├─────────────────────────────────────────────────────────────┤
│  Users & Permissions (Foundation)                          │
│  ├── Provides: Authentication, Authorization, User Mgmt    │
│  └── Dependencies: Keycloak, PostgreSQL                    │
├─────────────────────────────────────────────────────────────┤
│  Files (Storage Foundation)                                │
│  ├── Provides: File Storage, Metadata, Document Mgmt      │
│  ├── Dependencies: MongoDB GridFS, MinIO, Antivirus       │
│  └── Used by: All other modules for document storage       │
├─────────────────────────────────────────────────────────────┤
│  Vehicles (Business Foundation)                            │
│  ├── Provides: Vehicle Management, Fleet Tracking         │
│  ├── Dependencies: Users, Files, Global Reference Data    │
│  └── Used by: All business modules                         │
├─────────────────────────────────────────────────────────────┤
│  Notifications (Communication Hub)                         │
│  ├── Provides: Multi-channel Messaging, Real-time Alerts  │
│  ├── Dependencies: Users, Email/SMS Services, SignalR     │
│  └── Used by: All modules for user communication           │
├─────────────────────────────────────────────────────────────┤
│  Admin & SaaS Management (Platform Control)               │
│  ├── Provides: Tenant Mgmt, Billing, Deployment          │
│  ├── Dependencies: All core modules, Kubernetes           │
│  └── Manages: Entire platform lifecycle                   │
├─────────────────────────────────────────────────────────────┤
│  Observability & Monitoring (System Intelligence)         │
│  ├── Provides: Metrics, Logging, Alerts, Health Checks    │
│  ├── Dependencies: Prometheus, Elasticsearch, Grafana     │
│  └── Monitors: All modules and infrastructure              │
└─────────────────────────────────────────────────────────────┘
```

### Cross-Module Communication Patterns

#### 1. Users Module Integration
- **Authentication Provider**: All modules validate user identity through Users module
- **Permission Enforcement**: All operations check permissions via Users module
- **Audit Trail**: User actions are logged across all modules for compliance

#### 2. Files Module Integration
- **Document Storage**: Vehicle registration docs, maintenance reports, user avatars
- **Thumbnail Generation**: Automatic thumbnails for vehicle photos and documents
- **Virus Scanning**: All uploaded files are scanned before being made available

#### 3. Vehicles Module Integration
- **Central Entity**: Most business modules reference vehicles as primary entities
- **Global References**: Shared brand/model data prevents duplication
- **Maintenance Integration**: Links to maintenance schedules and service records

#### 4. Notifications Module Integration
- **Event Broadcasting**: All modules can trigger notifications for important events
- **User Preferences**: Respects user notification preferences across all modules
- **Multi-channel Delivery**: Email, SMS, push, and in-app notifications

#### 5. Cross-Module Events
```csharp
// Example: Vehicle registration triggers multiple actions
public class VehicleRegisteredEventHandler : 
    INotificationHandler<VehicleRegisteredEvent>
{
    public async Task Handle(VehicleRegisteredEvent notification, CancellationToken cancellationToken)
    {
        // 1. Send welcome notification via Notifications module
        await _notificationService.SendNotificationAsync(new SendNotificationRequest
        {
            TenantId = notification.TenantId,
            RecipientIds = new[] { notification.OwnerId },
            Type = NotificationType.VehicleRegistered,
            TemplateId = "vehicle_welcome"
        });

        // 2. Create vehicle folder in Files module
        await _fileService.CreateEntityFolderAsync(
            notification.TenantId,
            "Vehicle", 
            notification.VehicleId.ToString()
        );

        // 3. Record metrics in Observability module
        await _metricsService.IncrementCounterAsync(
            "vehicles_registered_total",
            new Dictionary<string, string> { ["tenant_id"] = notification.TenantId }
        );

        // 4. Update tenant statistics in Admin module
        await _tenantStatsService.UpdateVehicleCountAsync(notification.TenantId);
    }
}
```

### Data Flow Examples

#### Vehicle Registration Flow
1. **User uploads vehicle photo** → Files Module stores and processes image
2. **User fills registration form** → Vehicles Module validates and creates entity
3. **System sends confirmation** → Notifications Module delivers welcome message
4. **Admin tracks registration** → Observability Module records metrics
5. **Billing updates usage** → Admin Module updates subscription usage

#### Maintenance Reminder Flow
1. **System checks due dates** → Vehicles Module queries maintenance schedules
2. **Identifies due maintenance** → Business logic determines notification need
3. **Sends reminder notification** → Notifications Module delivers to vehicle owner
4. **User acknowledges** → Updates maintenance record in Vehicles Module
5. **Tracks engagement** → Observability Module records notification metrics

---

## Database Relationships

### Cross-Schema Foreign Keys
```sql
-- Core relationships between modules
ALTER TABLE vehicles.vehicles 
    ADD CONSTRAINT fk_vehicles_owner 
    FOREIGN KEY (owner_id) REFERENCES users.users(id);

ALTER TABLE vehicles.vehicles 
    ADD CONSTRAINT fk_vehicles_registration_doc 
    FOREIGN KEY (registration_document_id) REFERENCES files.file_metadata(id);

ALTER TABLE notifications.notification_recipients 
    ADD CONSTRAINT fk_notification_recipients_user 
    FOREIGN KEY (user_id) REFERENCES users.users(id);

ALTER TABLE files.file_metadata 
    ADD CONSTRAINT fk_files_uploaded_by 
    FOREIGN KEY (uploaded_by) REFERENCES users.users(id);

-- Tenant isolation constraints
ALTER TABLE users.users 
    ADD CONSTRAINT fk_users_tenant 
    FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id);

ALTER TABLE vehicles.vehicles 
    ADD CONSTRAINT fk_vehicles_tenant 
    FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id);

ALTER TABLE notifications.notifications 
    ADD CONSTRAINT fk_notifications_tenant 
    FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id);

ALTER TABLE files.file_metadata 
    ADD CONSTRAINT fk_files_tenant 
    FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id);
```

### Performance Considerations
- **Tenant Isolation**: All tables partitioned by tenant_id for optimal performance
- **Strategic Indexing**: Composite indexes on (tenant_id, entity_id) for cross-module queries
- **Connection Pooling**: Separate connection pools per module for better resource management
- **Caching Strategy**: Redis caching for frequently accessed cross-module data

---

## Conclusion

The DriveOps core modules form a cohesive, well-integrated foundation for automotive industry SaaS solutions. Each module is designed with:

### Design Principles
- **Single Responsibility**: Each module has a clearly defined purpose and scope
- **Loose Coupling**: Modules communicate through well-defined interfaces and events
- **High Cohesion**: Related functionality is grouped within appropriate modules
- **Tenant Isolation**: Complete data separation ensures security and compliance
- **Scalability**: Architecture supports horizontal scaling and performance optimization

### Key Architectural Benefits
1. **Modularity**: Independent development, testing, and deployment of features
2. **Maintainability**: Clear separation of concerns reduces complexity
3. **Extensibility**: New modules can be added without disrupting existing functionality
4. **Security**: Multiple layers of security including authentication, authorization, and data isolation
5. **Performance**: Optimized data access patterns and intelligent caching strategies

### Future Extensibility
The core modules provide a solid foundation for additional business modules:
- **Garage Module**: Leverages Vehicles, Users, Files, and Notifications
- **Fleet Module**: Builds upon Vehicles and Users with additional tracking capabilities
- **CRM Module**: Integrates with Users and Notifications for customer management
- **Accounting Module**: Uses data from all modules for financial reporting

This comprehensive core architecture ensures that DriveOps can scale from small automotive businesses to large enterprise deployments while maintaining security, performance, and operational excellence.

---

*Document created: 2024-09-01*  
*Last updated: 2024-09-01*
