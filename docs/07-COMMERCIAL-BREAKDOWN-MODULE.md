# BREAKDOWN Module - Emergency Roadside Assistance Documentation

## Overview

The BREAKDOWN module is a comprehensive 24/7 emergency roadside assistance and breakdown management system designed for automotive service providers. This premium commercial module (39€/month) provides complete emergency response operations management including real-time dispatching, GPS tracking, insurance integration, and mobile workforce coordination.

### Module Scope

The BREAKDOWN module delivers end-to-end emergency assistance management:
- **24/7 Emergency Call Center**: Multi-channel emergency request handling with priority routing
- **Real-time GPS Dispatching**: Intelligent technician assignment based on location, skills, and availability  
- **Fleet Management**: Comprehensive tow truck and service vehicle tracking
- **Insurance Integration**: Direct billing and claims processing with major insurance providers
- **Customer Communication**: Real-time status updates, ETA notifications, and service tracking
- **Performance Analytics**: SLA monitoring, response time tracking, and operational KPIs
- **Mobile Applications**: Dedicated apps for technicians and customers

### Key Features

#### Emergency Call Management
- Multi-channel emergency request intake (phone, mobile app, web portal)
- Priority-based call routing and escalation workflows
- Comprehensive call logging with audio recording capabilities
- Integration with emergency services and police when required

#### Real-time Dispatching System  
- GPS-based optimal technician assignment algorithms
- Dynamic re-routing based on traffic and service priorities
- Automated escalation for SLA violations
- Load balancing across service areas and technician skills

#### Fleet Tracking & Management
- Live vehicle location tracking and status monitoring
- Equipment inventory management per vehicle
- Fuel consumption and maintenance scheduling
- Performance analytics per vehicle and technician

#### Customer Experience
- Real-time service tracking with ETA updates
- SMS and push notification integration
- Photo/video evidence sharing
- Digital service reports and invoicing
- Customer feedback and rating system

#### Insurance & Billing Integration

#### Emergency Call Aggregate
- **EmergencyCall**: Primary aggregate managing the complete emergency response lifecycle
- **CallType**: Breakdown type classification (mechanical, accident, fuel, lockout, etc.)
- **CallPriority**: Priority levels (Critical, High, Medium, Low) with automated escalation
- **CallStatus**: Status tracking through the entire service lifecycle
- **CallLocation**: GPS coordinates, address details, and accessibility information

#### Service Fleet Aggregate  
- **ServiceVehicle**: Tow trucks, service vans, and mobile repair units
- **VehicleEquipment**: Specialized tools, parts inventory, and capability matrix
- **VehicleLocation**: Real-time GPS tracking with heading and speed
- **VehicleStatus**: Availability, maintenance, and operational status
- **ServiceCapability**: Skills matrix for different breakdown types

#### Technician & Dispatch Aggregate
- **Technician**: Service personnel with skills, certifications, and performance metrics
- **TechnicianSkills**: Specialized capabilities (heavy towing, electrical, mechanical)
- **TechnicianSchedule**: Work shifts, availability, and time-off management
- **DispatchAssignment**: Automated assignment linking calls to optimal technicians
- **ServiceArea**: Geographic coverage zones with response time SLAs

#### Insurance & Financial Aggregate
- **InsurancePartner**: Provider details, billing rates, and coverage types
- **InsuranceClaim**: Claim processing, approval workflows, and documentation
- **BillingRecord**: Comprehensive financial tracking for services rendered
- **ServiceReport**: Detailed work performed, parts used, and time tracking

---

## Complete C# Architecture

### Domain Layer Implementation

The BREAKDOWN module follows Domain-Driven Design (DDD) patterns with rich domain models and proper aggregate boundaries:

```csharp
namespace DriveOps.Breakdown.Domain.Entities
{
    public class EmergencyCall : AggregateRoot
    {
        public EmergencyCallId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string CallNumber { get; private set; }
        public VehicleId VehicleId { get; private set; }
        public UserId CallerId { get; private set; }
        public CallType CallType { get; private set; }
        public CallPriority Priority { get; private set; }
        public CallStatus Status { get; private set; }
        public string Description { get; private set; }
        public CallLocation Location { get; private set; }
        public DateTime CallReceivedAt { get; private set; }
        public DateTime? DispatchedAt { get; private set; }
        public DateTime? ServiceStartedAt { get; private set; }
        public DateTime? ServiceCompletedAt { get; private set; }
        public UserId? AssignedTechnicianId { get; private set; }
        public ServiceVehicleId? AssignedVehicleId { get; private set; }
        public InsuranceClaimId? InsuranceClaimId { get; private set; }
        public decimal? EstimatedCost { get; private set; }
        public decimal? ActualCost { get; private set; }
        public string? CustomerNotes { get; private set; }
        public string? TechnicianNotes { get; private set; }
        
        private readonly List<CallStatusHistory> _statusHistory = new();
        public IReadOnlyCollection<CallStatusHistory> StatusHistory => _statusHistory.AsReadOnly();
        
        private readonly List<CallCommunication> _communications = new();
        public IReadOnlyCollection<CallCommunication> Communications => _communications.AsReadOnly();

        public static EmergencyCall Create(
            TenantId tenantId,
            VehicleId vehicleId,
            UserId callerId,
            CallType callType,
            string description,
            CallLocation location,
            CallPriority? requestedPriority = null)
        {
            var call = new EmergencyCall
            {
                Id = EmergencyCallId.New(),
                TenantId = tenantId,
                CallNumber = GenerateCallNumber(),
                VehicleId = vehicleId,
                CallerId = callerId,
                CallType = callType,
                Priority = requestedPriority ?? CalculateAutomaticPriority(callType, location),
                Status = CallStatus.Received,
                Description = description,
                Location = location,
                CallReceivedAt = DateTime.UtcNow
            };

            call.AddDomainEvent(new EmergencyCallCreatedEvent(
                tenantId, call.Id, call.Priority, call.Location));

            return call;
        }

        public void AssignTechnician(UserId technicianId, ServiceVehicleId vehicleId, UserId assignedBy)
        {
            if (Status != CallStatus.Received && Status != CallStatus.Dispatching)
                throw new InvalidOperationException($"Cannot assign technician to call in status {Status}");

            AssignedTechnicianId = technicianId;
            AssignedVehicleId = vehicleId;
            Status = CallStatus.Dispatched;
            DispatchedAt = DateTime.UtcNow;

            _statusHistory.Add(new CallStatusHistory(
                Id, Status, DateTime.UtcNow, assignedBy, $"Assigned to technician {technicianId}"));

            AddDomainEvent(new EmergencyCallDispatchedEvent(
                TenantId, Id, technicianId, vehicleId, Location));
        }

        public void StartService(UserId technicianId, string? notes = null)
        {
            if (Status != CallStatus.Dispatched)
                throw new InvalidOperationException($"Cannot start service for call in status {Status}");

            if (AssignedTechnicianId != technicianId)
                throw new UnauthorizedAccessException("Only assigned technician can start service");

            Status = CallStatus.InProgress;
            ServiceStartedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(notes))
                TechnicianNotes = notes;

            _statusHistory.Add(new CallStatusHistory(
                Id, Status, DateTime.UtcNow, technicianId, "Service started on-site"));

            AddDomainEvent(new ServiceStartedEvent(TenantId, Id, technicianId));
        }

        public void CompleteService(
            UserId technicianId, 
            decimal actualCost, 
            string serviceReport,
            IEnumerable<ServiceAction> actionsPerformed)
        {
            if (Status != CallStatus.InProgress)
                throw new InvalidOperationException($"Cannot complete service for call in status {Status}");

            if (AssignedTechnicianId != technicianId)
                throw new UnauthorizedAccessException("Only assigned technician can complete service");

            Status = CallStatus.Completed;
            ServiceCompletedAt = DateTime.UtcNow;
            ActualCost = actualCost;
            TechnicianNotes = serviceReport;

            _statusHistory.Add(new CallStatusHistory(
                Id, Status, DateTime.UtcNow, technicianId, "Service completed successfully"));

            AddDomainEvent(new ServiceCompletedEvent(
                TenantId, Id, technicianId, actualCost, actionsPerformed.ToList()));
        }

        public void EscalateCall(CallPriority newPriority, UserId escalatedBy, string reason)
        {
            if (newPriority <= Priority)
                throw new InvalidOperationException("Can only escalate to higher priority");

            var previousPriority = Priority;
            Priority = newPriority;

            _statusHistory.Add(new CallStatusHistory(
                Id, Status, DateTime.UtcNow, escalatedBy, 
                $"Escalated from {previousPriority} to {newPriority}: {reason}"));

            AddDomainEvent(new CallEscalatedEvent(
                TenantId, Id, previousPriority, newPriority, reason));
        }

        public void AddCommunication(
            CommunicationType type, 
            string content, 
            UserId sentBy,
            string? recipientInfo = null)
        {
            var communication = new CallCommunication(
                Id, type, content, sentBy, recipientInfo, DateTime.UtcNow);
            
            _communications.Add(communication);

            AddDomainEvent(new CallCommunicationAddedEvent(
                TenantId, Id, type, content, recipientInfo));
        }

        private static CallPriority CalculateAutomaticPriority(CallType callType, CallLocation location)
        {
            // Priority calculation logic based on call type, location safety, weather, etc.
            return callType switch
            {
                CallType.Accident => CallPriority.Critical,
                CallType.Breakdown when location.IsHighwayLocation => CallPriority.High,
                CallType.FuelDelivery when location.IsRemoteLocation => CallPriority.High,
                CallType.Lockout => CallPriority.Medium,
                _ => CallPriority.Medium
            };
        }

        private static string GenerateCallNumber()
        {
            return $"BRK{DateTime.UtcNow:yyyyMMdd}{DateTime.UtcNow.Ticks % 10000:D4}";
        }
    }

    public class ServiceVehicle : AggregateRoot
    {
        public ServiceVehicleId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public VehicleId BaseVehicleId { get; private set; } // Reference to core Vehicle
        public string CallSign { get; private set; }
        public VehicleType Type { get; private set; }
        public VehicleStatus Status { get; private set; }
        public ServiceCapabilities Capabilities { get; private set; }
        public VehicleLocation? CurrentLocation { get; private set; }
        public UserId? CurrentDriverId { get; private set; }
        public DateTime? LastLocationUpdate { get; private set; }
        public int FuelLevel { get; private set; }
        public DateTime? LastMaintenanceAt { get; private set; }
        public DateTime? NextMaintenanceDue { get; private set; }
## Application Layer - CQRS Implementation

### Commands and Command Handlers

```csharp
namespace DriveOps.Breakdown.Application.Commands
{
    public record CreateEmergencyCallCommand(
        VehicleId VehicleId,
        UserId CallerId,
        CallType CallType,
        string Description,
        double Latitude,
        double Longitude,
        string Address,
        string? Landmark = null,
        CallPriority? RequestedPriority = null) : IRequest<Result<EmergencyCallId>>;

    public class CreateEmergencyCallHandler : IRequestHandler<CreateEmergencyCallCommand, Result<EmergencyCallId>>
    {
        private readonly IEmergencyCallRepository _callRepository;
        private readonly IVehicleRepository _vehicleRepository;
        private readonly ITenantContext _tenantContext;
        private readonly IDispatchingService _dispatchingService;
        private readonly INotificationService _notificationService;
        private readonly IRealtimeService _realtimeService;

        public async Task<Result<EmergencyCallId>> Handle(
            CreateEmergencyCallCommand request, 
            CancellationToken cancellationToken)
        {
            // Verify vehicle belongs to tenant
            var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
            if (vehicle == null || vehicle.TenantId != _tenantContext.TenantId)
                return Result.Failure<EmergencyCallId>("Vehicle not found or access denied");

            // Create call location
            var location = new CallLocation(
                request.Latitude, 
                request.Longitude, 
                request.Address,
                request.Landmark);

            // Create emergency call
            var call = EmergencyCall.Create(
                _tenantContext.TenantId,
                request.VehicleId,
                request.CallerId,
                request.CallType,
                request.Description,
                location,
                request.RequestedPriority);

            await _callRepository.AddAsync(call);

            // Immediate dispatch attempt for critical calls
            if (call.Priority == CallPriority.Critical)
            {
                _ = Task.Run(async () =>
                {
                    await _dispatchingService.AttemptImmediateDispatchAsync(call.Id);
                }, cancellationToken);
            }

            // Send real-time notification to dispatch center
            await _realtimeService.SendToGroupAsync("dispatch_center", "NewEmergencyCall", new
            {
                CallId = call.Id.Value,
                CallNumber = call.CallNumber,
                Priority = call.Priority.ToString(),
                CallType = call.CallType.ToString(),
                Location = new { call.Location.Latitude, call.Location.Longitude, call.Location.Address },
                ReceivedAt = call.CallReceivedAt
            });

            return Result.Success(call.Id);
        }
    }

    public record DispatchTechnicianCommand(
        EmergencyCallId CallId,
        TechnicianId TechnicianId,
        ServiceVehicleId VehicleId,
        UserId DispatchedBy) : IRequest<Result>;

    public class DispatchTechnicianHandler : IRequestHandler<DispatchTechnicianCommand, Result>
    {
        private readonly IEmergencyCallRepository _callRepository;
        private readonly ITechnicianRepository _technicianRepository;
        private readonly IServiceVehicleRepository _vehicleRepository;
        private readonly INotificationService _notificationService;
        private readonly IRealtimeService _realtimeService;

        public async Task<Result> Handle(DispatchTechnicianCommand request, CancellationToken cancellationToken)
        {
            var call = await _callRepository.GetByIdAsync(request.CallId);
            if (call == null)
                return Result.Failure("Emergency call not found");

            var technician = await _technicianRepository.GetByIdAsync(request.TechnicianId);
            if (technician == null)
                return Result.Failure("Technician not found");

            var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
            if (vehicle == null)
                return Result.Failure("Service vehicle not found");

            // Verify technician can handle this call type
            if (!technician.CanHandleCallType(call.CallType))
                return Result.Failure("Technician does not have required skills for this call type");

            // Verify vehicle capabilities
            if (!vehicle.Capabilities.CanHandleCallType(call.CallType))
                return Result.Failure("Service vehicle does not have required capabilities");

            // Assign technician to call
            call.AssignTechnician(technician.UserId, request.VehicleId, request.DispatchedBy);
            vehicle.AssignToCall(request.CallId, technician.UserId);

            await _callRepository.UpdateAsync(call);
            await _vehicleRepository.UpdateAsync(vehicle);

            // Send notifications
            await _notificationService.SendNotificationAsync(new SendNotificationRequest
            {
                Type = NotificationType.DispatchNotification,
                RecipientIds = new[] { technician.UserId },
                Title = $"New Assignment - {call.CallType}",
                Message = $"You have been assigned to emergency call {call.CallNumber}",
                Data = new { CallId = call.Id.Value, Location = call.Location }
            });

            // Real-time updates
            await _realtimeService.SendToUserAsync(technician.UserId.Value.ToString(), "CallAssigned", new
            {
                CallId = call.Id.Value,
                CallNumber = call.CallNumber,
                CallType = call.CallType.ToString(),
                Priority = call.Priority.ToString(),
                Location = call.Location,
                CustomerContact = call.CallerId
            });

            return Result.Success();
        }
    }

    public record UpdateServiceStatusCommand(
        EmergencyCallId CallId,
        ServiceStatus Status,
        UserId TechnicianId,
        string? Notes = null,
        double? Latitude = null,
        double? Longitude = null) : IRequest<Result>;

    public class UpdateServiceStatusHandler : IRequestHandler<UpdateServiceStatusCommand, Result>
    {
        private readonly IEmergencyCallRepository _callRepository;
        private readonly ITechnicianRepository _technicianRepository;
        private readonly ICustomerNotificationService _customerNotificationService;
        private readonly IRealtimeService _realtimeService;

        public async Task<Result> Handle(UpdateServiceStatusCommand request, CancellationToken cancellationToken)
        {
            var call = await _callRepository.GetByIdAsync(request.CallId);
            if (call == null)
                return Result.Failure("Emergency call not found");

            // Update location if provided
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                var technician = await _technicianRepository.GetByUserIdAsync(request.TechnicianId);
                technician?.UpdateLocation(request.Latitude.Value, request.Longitude.Value);
            }

            // Update call status based on service status
            switch (request.Status)
            {
                case ServiceStatus.EnRoute:
                    // Call already dispatched, just update ETA
                    break;
                case ServiceStatus.OnSite:
                    call.StartService(request.TechnicianId, request.Notes);
                    break;
                case ServiceStatus.Completed:
                    // This would be handled by a separate CompleteServiceCommand
                    return Result.Failure("Use CompleteServiceCommand for service completion");
                default:
                    return Result.Failure($"Unknown service status: {request.Status}");
            }

            await _callRepository.UpdateAsync(call);

            // Send customer notification
            await _customerNotificationService.SendStatusUpdateAsync(call, request.Status);

            // Real-time update to customer app
            await _realtimeService.SendToUserAsync(call.CallerId.Value.ToString(), "ServiceStatusUpdate", new
            {
                CallId = call.Id.Value,
                Status = request.Status.ToString(),
                Notes = request.Notes,
                UpdatedAt = DateTime.UtcNow
            });

            return Result.Success();
        }
    }
}
```

### Queries and Query Handlers

```csharp
namespace DriveOps.Breakdown.Application.Queries
{
    public record GetEmergencyCallQuery(EmergencyCallId CallId) : IRequest<Result<EmergencyCallDetailDto>>;

    public class GetEmergencyCallHandler : IRequestHandler<GetEmergencyCallQuery, Result<EmergencyCallDetailDto>>
    {
        private readonly IEmergencyCallRepository _callRepository;
        private readonly ITechnicianRepository _technicianRepository;
        private readonly IVehicleRepository _vehicleRepository;
        private readonly IMapper _mapper;

        public async Task<Result<EmergencyCallDetailDto>> Handle(
            GetEmergencyCallQuery request, 
            CancellationToken cancellationToken)
        {
            var call = await _callRepository.GetByIdWithDetailsAsync(request.CallId);
            if (call == null)
                return Result.Failure<EmergencyCallDetailDto>("Emergency call not found");

            var dto = _mapper.Map<EmergencyCallDetailDto>(call);

            // Enrich with technician details if assigned
            if (call.AssignedTechnicianId.HasValue)
            {
                var technician = await _technicianRepository.GetByUserIdAsync(call.AssignedTechnicianId.Value);
                if (technician != null)
                {
                    dto.AssignedTechnician = _mapper.Map<TechnicianSummaryDto>(technician);
                }
            }

            return Result.Success(dto);
        }
    }

    public record GetActiveCallsQuery(
        int Page = 1,
        int PageSize = 20,
        CallPriority? Priority = null,
        CallStatus? Status = null,
        ServiceArea? ServiceArea = null) : IRequest<Result<PagedResult<EmergencyCallSummaryDto>>>;

    public class GetActiveCallsHandler : IRequestHandler<GetActiveCallsQuery, Result<PagedResult<EmergencyCallSummaryDto>>>
    {
        private readonly IEmergencyCallRepository _callRepository;
        private readonly ITenantContext _tenantContext;
        private readonly IMapper _mapper;

        public async Task<Result<PagedResult<EmergencyCallSummaryDto>>> Handle(
            GetActiveCallsQuery request, 
            CancellationToken cancellationToken)
        {
            var specification = new ActiveCallsSpecification(
                _tenantContext.TenantId,
                request.Priority,
                request.Status,
                request.ServiceArea);

            var calls = await _callRepository.GetPagedAsync(
                specification, request.Page, request.PageSize);

            var dtos = _mapper.Map<IEnumerable<EmergencyCallSummaryDto>>(calls.Items);

            return Result.Success(new PagedResult<EmergencyCallSummaryDto>(
                dtos, calls.TotalCount, request.Page, request.PageSize));
        }
    }

    public record GetTechnicianDashboardQuery(TechnicianId TechnicianId) : IRequest<Result<TechnicianDashboardDto>>;

    public class GetTechnicianDashboardHandler : IRequestHandler<GetTechnicianDashboardQuery, Result<TechnicianDashboardDto>>
    {
        private readonly ITechnicianRepository _technicianRepository;
        private readonly IEmergencyCallRepository _callRepository;
        private readonly IServiceVehicleRepository _vehicleRepository;
        private readonly IMapper _mapper;

        public async Task<Result<TechnicianDashboardDto>> Handle(
            GetTechnicianDashboardQuery request, 
            CancellationToken cancellationToken)
        {
            var technician = await _technicianRepository.GetByIdAsync(request.TechnicianId);
            if (technician == null)
                return Result.Failure<TechnicianDashboardDto>("Technician not found");

            var activeAssignments = await _callRepository.GetActiveTechnicianCallsAsync(technician.UserId);
            var assignedVehicle = technician.AssignedVehicleId.HasValue 
                ? await _vehicleRepository.GetByIdAsync(technician.AssignedVehicleId.Value)
                : null;

            var dashboard = new TechnicianDashboardDto
            {
                Technician = _mapper.Map<TechnicianDetailDto>(technician),
                AssignedVehicle = assignedVehicle != null ? _mapper.Map<ServiceVehicleDto>(assignedVehicle) : null,
                ActiveCalls = _mapper.Map<IEnumerable<EmergencyCallSummaryDto>>(activeAssignments),
                TodayStats = await CalculateTodayStatsAsync(technician.UserId),
                MonthStats = await CalculateMonthStatsAsync(technician.UserId)
            };

            return Result.Success(dashboard);
        }

        private async Task<TechnicianStatsDto> CalculateTodayStatsAsync(UserId technicianUserId)
        {
            var today = DateTime.UtcNow.Date;
            var todayCalls = await _callRepository.GetTechnicianCallsInPeriodAsync(
                technicianUserId, today, today.AddDays(1));

            return new TechnicianStatsDto
            {
                TotalCalls = todayCalls.Count(),
## Database Schema (PostgreSQL with PostGIS)

### Core Emergency Operations Tables

```sql
-- Create breakdown schema
CREATE SCHEMA IF NOT EXISTS breakdown;

-- Enable PostGIS extension for geospatial operations
CREATE EXTENSION IF NOT EXISTS postgis;

-- Emergency calls table with geospatial support
CREATE TABLE breakdown.emergency_calls (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    call_number VARCHAR(20) NOT NULL UNIQUE,
    vehicle_id UUID NOT NULL, -- Reference to vehicles.vehicles
    caller_id UUID NOT NULL, -- Reference to users.users
    call_type INTEGER NOT NULL, -- 0: Towing, 1: Mechanical, 2: Electrical, 3: FuelDelivery, 4: Lockout, 5: JumpStart, 6: WheelChange
    priority INTEGER NOT NULL, -- 0: Low, 1: Medium, 2: High, 3: Critical
    status INTEGER NOT NULL DEFAULT 0, -- 0: Received, 1: Dispatching, 2: Dispatched, 3: InProgress, 4: Completed, 5: Cancelled
    description TEXT NOT NULL,
    
    -- Geospatial location information
    location_point GEOMETRY(POINT, 4326) NOT NULL,
    location_address TEXT NOT NULL,
    location_landmark TEXT,
    is_highway_location BOOLEAN NOT NULL DEFAULT FALSE,
    is_remote_location BOOLEAN NOT NULL DEFAULT FALSE,
    is_safe_location BOOLEAN NOT NULL DEFAULT TRUE,
    access_instructions TEXT,
    
    -- Timing information
    call_received_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    dispatched_at TIMESTAMP WITH TIME ZONE,
    service_started_at TIMESTAMP WITH TIME ZONE,
    service_completed_at TIMESTAMP WITH TIME ZONE,
    
    -- Assignment information
    assigned_technician_id UUID, -- Reference to users.users
    assigned_vehicle_id UUID, -- Reference to breakdown.service_vehicles
    insurance_claim_id UUID, -- Reference to breakdown.insurance_claims
    
    -- Cost information
    estimated_cost DECIMAL(10,2),
    actual_cost DECIMAL(10,2),
    
    -- Notes
    customer_notes TEXT,
    technician_notes TEXT,
    
    -- Customer feedback
    customer_rating INTEGER CHECK (customer_rating >= 1 AND customer_rating <= 5),
    customer_feedback TEXT,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Call status history for audit trail
CREATE TABLE breakdown.call_status_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    call_id UUID NOT NULL REFERENCES breakdown.emergency_calls(id) ON DELETE CASCADE,
    status INTEGER NOT NULL,
    changed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    changed_by UUID NOT NULL, -- Reference to users.users
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Call communications log
CREATE TABLE breakdown.call_communications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    call_id UUID NOT NULL REFERENCES breakdown.emergency_calls(id) ON DELETE CASCADE,
    communication_type INTEGER NOT NULL, -- 0: SMS, 1: Email, 2: Phone, 3: InApp, 4: System
    content TEXT NOT NULL,
    sent_by UUID NOT NULL, -- Reference to users.users
    recipient_info TEXT, -- Phone number, email, or user ID
    sent_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    delivered_at TIMESTAMP WITH TIME ZONE,
    read_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Service vehicles with tracking capabilities
CREATE TABLE breakdown.service_vehicles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    base_vehicle_id UUID NOT NULL, -- Reference to vehicles.vehicles
    call_sign VARCHAR(20) NOT NULL,
    vehicle_type INTEGER NOT NULL, -- 0: TowTruck, 1: ServiceVan, 2: MotorcycleAssist, 3: HeavyRecovery
    status INTEGER NOT NULL DEFAULT 0, -- 0: Available, 1: Dispatched, 2: OnCall, 3: OutOfService, 4: Maintenance
    
    -- Capabilities matrix
    can_tow_light_vehicles BOOLEAN NOT NULL DEFAULT FALSE,
    can_tow_heavy_vehicles BOOLEAN NOT NULL DEFAULT FALSE,
    can_perform_jump_start BOOLEAN NOT NULL DEFAULT FALSE,
    can_deliver_fuel BOOLEAN NOT NULL DEFAULT FALSE,
    can_unlock_vehicles BOOLEAN NOT NULL DEFAULT FALSE,
    can_perform_minor_repairs BOOLEAN NOT NULL DEFAULT FALSE,
    can_change_wheels BOOLEAN NOT NULL DEFAULT FALSE,
    max_tow_weight INTEGER NOT NULL DEFAULT 0,
    
    -- Current location and status
    current_location GEOMETRY(POINT, 4326),
    current_heading DECIMAL(5,2), -- 0-360 degrees
    current_speed DECIMAL(5,2), -- km/h
    last_location_update TIMESTAMP WITH TIME ZONE,
    current_driver_id UUID, -- Reference to users.users
    
    -- Vehicle status
    fuel_level INTEGER NOT NULL DEFAULT 100 CHECK (fuel_level >= 0 AND fuel_level <= 100),
    last_maintenance_at TIMESTAMP WITH TIME ZONE,
    next_maintenance_due TIMESTAMP WITH TIME ZONE,
    mileage INTEGER NOT NULL DEFAULT 0,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Vehicle equipment inventory
CREATE TABLE breakdown.vehicle_equipment (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    vehicle_id UUID NOT NULL REFERENCES breakdown.service_vehicles(id) ON DELETE CASCADE,
    equipment_type INTEGER NOT NULL, -- 0: TowingEquipment, 1: JumpStarter, 2: FuelContainer, 3: LockoutKit, 4: RepairTools, 5: SafetyEquipment
    name VARCHAR(255) NOT NULL,
    serial_number VARCHAR(100),
    is_operational BOOLEAN NOT NULL DEFAULT TRUE,
    last_maintenance_at TIMESTAMP WITH TIME ZONE,
    next_maintenance_due TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Technicians and their capabilities
CREATE TABLE breakdown.technicians (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    user_id UUID NOT NULL UNIQUE, -- Reference to users.users
    employee_number VARCHAR(50) NOT NULL,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Available, 1: Dispatched, 2: OnCall, 3: OffDuty, 4: Unavailable
    hire_date DATE NOT NULL,
    license_number VARCHAR(100),
    license_expiry_date DATE,
    
    -- Current location
    current_location GEOMETRY(POINT, 4326),
    last_location_update TIMESTAMP WITH TIME ZONE,
    
    -- Assignment information
    assigned_vehicle_id UUID REFERENCES breakdown.service_vehicles(id),
    
    -- Performance metrics
    completed_calls INTEGER NOT NULL DEFAULT 0,
    average_rating DECIMAL(3,2) NOT NULL DEFAULT 0 CHECK (average_rating >= 0 AND average_rating <= 5),
    last_active_at TIMESTAMP WITH TIME ZONE,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Technician skills and certifications
CREATE TABLE breakdown.technician_skills (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    technician_id UUID NOT NULL REFERENCES breakdown.technicians(id) ON DELETE CASCADE,
    skill_type INTEGER NOT NULL, -- 0: Towing, 1: Mechanical, 2: Electrical, 3: FuelDelivery, 4: Lockout, 5: HeavyRecovery
    skill_level INTEGER NOT NULL, -- 0: Basic, 1: Intermediate, 2: Advanced, 3: Expert
    certification_date DATE,
    certification_expiry DATE,
    certifying_body VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(technician_id, skill_type)
);

-- Technician schedules and availability
CREATE TABLE breakdown.technician_schedules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    technician_id UUID NOT NULL REFERENCES breakdown.technicians(id) ON DELETE CASCADE,
    schedule_date DATE NOT NULL,
    shift_start TIME NOT NULL,
    shift_end TIME NOT NULL,
    is_available BOOLEAN NOT NULL DEFAULT TRUE,
    break_start TIME,
    break_end TIME,
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(technician_id, schedule_date)
);

-- Service assignments tracking
CREATE TABLE breakdown.service_assignments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    call_id UUID NOT NULL REFERENCES breakdown.emergency_calls(id) ON DELETE CASCADE,
    vehicle_id UUID NOT NULL REFERENCES breakdown.service_vehicles(id),
    technician_id UUID NOT NULL REFERENCES breakdown.technicians(id),
    assigned_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    accepted_at TIMESTAMP WITH TIME ZONE,
    started_at TIMESTAMP WITH TIME ZONE,
    completed_at TIMESTAMP WITH TIME ZONE,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Assigned, 1: Accepted, 2: EnRoute, 3: OnSite, 4: Completed, 5: Cancelled
    estimated_arrival TIMESTAMP WITH TIME ZONE,
    actual_arrival TIMESTAMP WITH TIME ZONE,
    travel_distance_km DECIMAL(8,2),
    travel_time_minutes INTEGER,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Service areas and coverage zones
CREATE TABLE breakdown.service_areas (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    coverage_polygon GEOMETRY(POLYGON, 4326) NOT NULL,
    priority_level INTEGER NOT NULL DEFAULT 1, -- Higher number = higher priority
    response_time_sla_minutes INTEGER NOT NULL DEFAULT 60,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Coverage zones for different service types
CREATE TABLE breakdown.coverage_zones (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    service_area_id UUID NOT NULL REFERENCES breakdown.service_areas(id) ON DELETE CASCADE,
    call_type INTEGER NOT NULL,
    max_response_time_minutes INTEGER NOT NULL,
    minimum_technicians_required INTEGER NOT NULL DEFAULT 1,
    is_24_7_coverage BOOLEAN NOT NULL DEFAULT TRUE,
    coverage_hours_start TIME,
    coverage_hours_end TIME,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Insurance partners and billing integration
CREATE TABLE breakdown.insurance_partners (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    partner_code VARCHAR(50) NOT NULL,
    contact_email VARCHAR(255),
    contact_phone VARCHAR(50),
    billing_email VARCHAR(255),
    api_endpoint VARCHAR(500),
    api_key_encrypted TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Billing configuration
    supports_direct_billing BOOLEAN NOT NULL DEFAULT FALSE,
    billing_frequency VARCHAR(20) NOT NULL DEFAULT 'monthly', -- daily, weekly, monthly
    payment_terms_days INTEGER NOT NULL DEFAULT 30,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Insurance claims and processing
CREATE TABLE breakdown.insurance_claims (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    call_id UUID NOT NULL REFERENCES breakdown.emergency_calls(id),
    insurance_partner_id UUID NOT NULL REFERENCES breakdown.insurance_partners(id),
    claim_number VARCHAR(100) NOT NULL,
    policy_number VARCHAR(100) NOT NULL,
    claim_amount DECIMAL(10,2) NOT NULL,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Submitted, 1: InReview, 2: Approved, 3: Rejected, 4: Paid, 5: Disputed
    submitted_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    approved_at TIMESTAMP WITH TIME ZONE,
    paid_at TIMESTAMP WITH TIME ZONE,
    rejection_reason TEXT,
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Billing records for services
## Mobile Applications

### Technician Mobile Application

The technician mobile app provides comprehensive field service capabilities with offline support and real-time communication:

#### Core Features
- **Real-time Job Management**: Receive, accept, and track emergency call assignments
- **GPS Navigation**: Integrated turn-by-turn navigation with traffic optimization
- **Customer Communication**: Direct calling, SMS, and in-app messaging
- **Service Documentation**: Photo capture, digital signatures, and detailed reporting
- **Offline Capability**: Continue working in areas with poor connectivity
- **Performance Tracking**: Real-time metrics and feedback

#### Technical Implementation

```csharp
namespace DriveOps.Breakdown.Mobile.Technician
{
    public class TechnicianMobileService
    {
        private readonly ILocalDatabase _localDb;
        private readonly ISignalRClient _signalRClient;
        private readonly IGpsService _gpsService;
        private readonly ICameraService _cameraService;
        private readonly IOfflineQueueService _offlineQueue;

        public async Task<Result> AcceptCallAssignmentAsync(EmergencyCallId callId)
        {
            var assignment = await _localDb.GetAssignmentAsync(callId);
            if (assignment == null)
                return Result.Failure("Assignment not found");

            assignment.Status = AssignmentStatus.Accepted;
            assignment.AcceptedAt = DateTime.UtcNow;
            
            await _localDb.SaveAssignmentAsync(assignment);

            // Queue for sync when online
            await _offlineQueue.EnqueueAsync(new AcceptAssignmentCommand(callId));

            // If online, send immediately
            if (await IsOnlineAsync())
            {
                await _signalRClient.SendAsync("AcceptAssignment", callId.Value);
            }

            return Result.Success();
        }

        public async Task StartNavigationToCallAsync(EmergencyCallId callId)
        {
            var assignment = await _localDb.GetAssignmentAsync(callId);
            if (assignment?.Call?.Location == null)
                return;

            var currentLocation = await _gpsService.GetCurrentLocationAsync();
            var destination = new Location(
                assignment.Call.Location.Latitude, 
                assignment.Call.Location.Longitude);

            await _gpsService.StartNavigationAsync(currentLocation, destination);
        }

        public async Task UpdateLocationAsync()
        {
            var location = await _gpsService.GetCurrentLocationAsync();
            
            // Update local database
            await _localDb.UpdateTechnicianLocationAsync(location);

            // Send to server if online
            if (await IsOnlineAsync())
            {
                await _signalRClient.SendAsync("UpdateLocation", location.Latitude, location.Longitude);
            }
            else
            {
                // Queue for later sync
                await _offlineQueue.EnqueueAsync(new UpdateLocationCommand(location.Latitude, location.Longitude));
            }
        }

        public async Task<Result> StartServiceAsync(EmergencyCallId callId, string? notes = null)
        {
            var assignment = await _localDb.GetAssignmentAsync(callId);
            if (assignment == null)
                return Result.Failure("Assignment not found");

            assignment.Status = AssignmentStatus.OnSite;
            assignment.StartedAt = DateTime.UtcNow;
            
            await _localDb.SaveAssignmentAsync(assignment);
            await _offlineQueue.EnqueueAsync(new StartServiceCommand(callId, notes));

            return Result.Success();
        }

        public async Task<Result> CaptureServicePhotoAsync(EmergencyCallId callId, PhotoType photoType)
        {
            try
            {
                var photo = await _cameraService.TakePhotoAsync();
                var photoMetadata = new ServicePhoto
                {
                    CallId = callId,
                    PhotoType = photoType,
                    FilePath = photo.FilePath,
                    Timestamp = DateTime.UtcNow,
                    GpsLocation = await _gpsService.GetCurrentLocationAsync()
                };

                await _localDb.SaveServicePhotoAsync(photoMetadata);
                await _offlineQueue.EnqueueAsync(new UploadServicePhotoCommand(photoMetadata));

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to capture photo: {ex.Message}");
            }
        }

        public async Task<Result> CompleteServiceAsync(EmergencyCallId callId, ServiceCompletionData completionData)
        {
            var assignment = await _localDb.GetAssignmentAsync(callId);
            if (assignment == null)
                return Result.Failure("Assignment not found");

            // Create service report
            var serviceReport = new ServiceReport
            {
                CallId = callId,
                TechnicianId = assignment.TechnicianId,
                WorkPerformed = completionData.WorkPerformed,
                PartsUsed = completionData.PartsUsed,
                TimeSpentMinutes = (int)(DateTime.UtcNow - assignment.StartedAt)?.TotalMinutes,
                BeforePhotos = await _localDb.GetServicePhotosAsync(callId, PhotoType.Before),
                AfterPhotos = await _localDb.GetServicePhotosAsync(callId, PhotoType.After),
                CustomerSignature = completionData.CustomerSignature,
                CompletionNotes = completionData.Notes
            };

            await _localDb.SaveServiceReportAsync(serviceReport);
            await _offlineQueue.EnqueueAsync(new CompleteServiceCommand(callId, serviceReport));

            assignment.Status = AssignmentStatus.Completed;
            assignment.CompletedAt = DateTime.UtcNow;
            await _localDb.SaveAssignmentAsync(assignment);

            return Result.Success();
        }
    }

    public class OfflineQueueService : IOfflineQueueService
    {
        private readonly ILocalDatabase _localDb;
        private readonly IApiClient _apiClient;
        private readonly IConnectivityService _connectivity;

        public async Task EnqueueAsync<T>(T command) where T : class
        {
            var queueItem = new OfflineQueueItem
            {
                Id = Guid.NewGuid(),
                CommandType = typeof(T).Name,
                CommandData = JsonSerializer.Serialize(command),
                CreatedAt = DateTime.UtcNow,
                Attempts = 0
            };

            await _localDb.AddToQueueAsync(queueItem);
        }

        public async Task ProcessQueueAsync()
        {
            if (!await _connectivity.IsConnectedAsync())
                return;

            var queueItems = await _localDb.GetPendingQueueItemsAsync();
            
            foreach (var item in queueItems)
            {
                try
                {
                    await ProcessQueueItemAsync(item);
                    await _localDb.RemoveFromQueueAsync(item.Id);
                }
                catch (Exception ex)
                {
                    item.Attempts++;
                    item.LastError = ex.Message;
                    item.LastAttemptAt = DateTime.UtcNow;
                    
                    if (item.Attempts >= 3)
                    {
                        // Move to failed queue for manual review
                        await _localDb.MoveToFailedQueueAsync(item);
                    }
                    else
                    {
                        await _localDb.UpdateQueueItemAsync(item);
                    }
                }
            }
        }

        private async Task ProcessQueueItemAsync(OfflineQueueItem item)
        {
            switch (item.CommandType)
            {
                case nameof(AcceptAssignmentCommand):
                    var acceptCmd = JsonSerializer.Deserialize<AcceptAssignmentCommand>(item.CommandData);
                    await _apiClient.PostAsync($"/api/breakdown/calls/{acceptCmd.CallId}/accept", null);
                    break;
                    
                case nameof(UpdateLocationCommand):
                    var locationCmd = JsonSerializer.Deserialize<UpdateLocationCommand>(item.CommandData);
                    await _apiClient.PostAsync("/api/breakdown/technicians/location", locationCmd);
                    break;
                    
                case nameof(StartServiceCommand):
                    var startCmd = JsonSerializer.Deserialize<StartServiceCommand>(item.CommandData);
                    await _apiClient.PostAsync($"/api/breakdown/calls/{startCmd.CallId}/start", startCmd);
                    break;
                    
                case nameof(CompleteServiceCommand):
                    var completeCmd = JsonSerializer.Deserialize<CompleteServiceCommand>(item.CommandData);
                    await _apiClient.PostAsync($"/api/breakdown/calls/{completeCmd.CallId}/complete", completeCmd);
                    break;
                    
                default:
                    throw new InvalidOperationException($"Unknown command type: {item.CommandType}");
            }
        }
    }
}
```

### Customer Mobile Application

The customer mobile app provides emergency assistance request and service tracking capabilities:

#### Core Features
- **Emergency Assistance Request**: Quick breakdown reporting with automatic location detection
- **Real-time Service Tracking**: Live tracking of assigned technician with ETA updates
- **Communication Tools**: Direct messaging and calling with assigned technician
- **Service History**: Complete history of all breakdown assistance requests
- **Rating and Feedback**: Service quality rating and feedback system
- **Profile Management**: Vehicle information and emergency contact management

#### Key Screens and Workflows

```csharp
namespace DriveOps.Breakdown.Mobile.Customer
{
    public class CustomerMobileService
    {
        private readonly IApiClient _apiClient;
        private readonly IGpsService _gpsService;
        private readonly ISignalRClient _signalRClient;
        private readonly INotificationService _notificationService;

        public async Task<Result<EmergencyCallId>> RequestEmergencyAssistanceAsync(EmergencyRequestDto request)
        {
            try
            {
                // Get current location if not provided
                if (request.Latitude == 0 && request.Longitude == 0)
                {
                    var location = await _gpsService.GetCurrentLocationAsync();
                    request.Latitude = location.Latitude;
                    request.Longitude = location.Longitude;
                    request.Address = await _gpsService.GetAddressFromLocationAsync(location);
                }

                var response = await _apiClient.PostAsync<CreateEmergencyCallResponse>(
                    "/api/breakdown/calls", request);

                if (response.IsSuccess)
                {
                    // Start listening for updates on this call
                    await _signalRClient.JoinGroupAsync($"call_{response.Data.CallId}");
                    
                    return Result.Success(new EmergencyCallId(response.Data.CallId));
                }

                return Result.Failure<EmergencyCallId>(response.Error);
            }
            catch (Exception ex)
            {
                return Result.Failure<EmergencyCallId>($"Failed to request assistance: {ex.Message}");
            }
        }

        public async Task<Result<EmergencyCallDetailDto>> GetCallStatusAsync(EmergencyCallId callId)
        {
            var response = await _apiClient.GetAsync<EmergencyCallDetailDto>(
                $"/api/breakdown/calls/{callId.Value}");

            return response.IsSuccess 
                ? Result.Success(response.Data)
                : Result.Failure<EmergencyCallDetailDto>(response.Error);
        }

        public async Task<Result> StartTrackingTechnicianAsync(EmergencyCallId callId)
        {
            try
            {
                await _signalRClient.JoinGroupAsync($"tracking_{callId.Value}");
                
                // Subscribe to technician location updates
                _signalRClient.On<TechnicianLocationUpdate>("TechnicianLocationUpdate", update =>
                {
                    // Update UI with new technician location
                    MessagingCenter.Send(this, "TechnicianLocationUpdated", update);
                });

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to start tracking: {ex.Message}");
            }
        }

        public async Task<Result> SendMessageToTechnicianAsync(EmergencyCallId callId, string message)
        {
            var request = new SendMessageRequest
            {
                CallId = callId.Value,
                Message = message,
                MessageType = MessageType.CustomerToTechnician
            };

            var response = await _apiClient.PostAsync("/api/breakdown/communications", request);
            
            return response.IsSuccess 
                ? Result.Success()
                : Result.Failure(response.Error);
        }

        public async Task<Result> SubmitFeedbackAsync(EmergencyCallId callId, CustomerFeedbackDto feedback)
        {
            var response = await _apiClient.PostAsync(
                $"/api/breakdown/calls/{callId.Value}/feedback", feedback);

            return response.IsSuccess 
                ? Result.Success()
                : Result.Failure(response.Error);
        }

        public async Task<Result<IEnumerable<EmergencyCallSummaryDto>>> GetServiceHistoryAsync()
        {
            var response = await _apiClient.GetAsync<IEnumerable<EmergencyCallSummaryDto>>(
                "/api/breakdown/calls/history");

            return response.IsSuccess 
                ? Result.Success(response.Data)
                : Result.Failure<IEnumerable<EmergencyCallSummaryDto>>(response.Error);
        }
    }

    public class EmergencyRequestDto
    {
        public Guid VehicleId { get; set; }
        public CallType CallType { get; set; }
        public string Description { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? Landmark { get; set; }
        public CallPriority? RequestedPriority { get; set; }
        public bool IsHighwayLocation { get; set; }
        public bool IsSafeLocation { get; set; } = true;
        public string? AccessInstructions { get; set; }
    }

    public class CustomerFeedbackDto
    {
        public int OverallRating { get; set; }
        public int? ResponseTimeRating { get; set; }
        public int? TechnicianRating { get; set; }
        public int? ServiceQualityRating { get; set; }
        public int? CommunicationRating { get; set; }
        public string? Comments { get; set; }
        public bool WouldRecommend { get; set; }
    }
}
```

---

## Integration Points

### Core Module Integration

The BREAKDOWN module integrates extensively with DriveOps core modules:

#### 1. Users & Permissions Module Integration
```csharp
namespace DriveOps.Breakdown.Integration
{
    public class BreakdownUserService
    {
        private readonly IUserService _userService;
        private readonly ITechnicianRepository _technicianRepository;

        public async Task<Result> CreateTechnicianUserAsync(CreateTechnicianRequest request)
        {
            // Create user account in Users module
            var userResult = await _userService.CreateUserAsync(new CreateUserRequest
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone,
                Roles = new[] { "Technician" }
            });

            if (!userResult.IsSuccess)
                return Result.Failure(userResult.Error);

            // Create technician profile in Breakdown module
            var technician = new Technician(
                TenantId.Create(_tenantContext.TenantId),
                userResult.Data.UserId,
                request.EmployeeNumber,
                request.HireDate,
                request.LicenseNumber);

            await _technicianRepository.AddAsync(technician);
            
            return Result.Success();
        }
    }
}
```

#### 2. Vehicles Module Integration
```csharp
namespace DriveOps.Breakdown.Integration
{
    public class BreakdownVehicleService
    {
        private readonly IVehicleService _vehicleService;
        private readonly IServiceVehicleRepository _serviceVehicleRepository;

        public async Task<Result> RegisterServiceVehicleAsync(RegisterServiceVehicleRequest request)
        {
            // Verify base vehicle exists in Vehicles module
            var baseVehicle = await _vehicleService.GetVehicleByIdAsync(request.BaseVehicleId);
            if (baseVehicle == null)
                return Result.Failure("Base vehicle not found");

            // Create service vehicle with breakdown-specific capabilities
            var serviceVehicle = new ServiceVehicle(
                ServiceVehicleId.New(),
                TenantId.Create(_tenantContext.TenantId),
                request.BaseVehicleId,
                request.CallSign,
                request.VehicleType,
                request.Capabilities);

            await _serviceVehicleRepository.AddAsync(serviceVehicle);
            
            return Result.Success();
        }

        public async Task<Result> SyncVehicleMaintenanceAsync(VehicleId vehicleId)
        {
            // Get maintenance records from Vehicles module
            var maintenanceRecords = await _vehicleService.GetMaintenanceRecordsAsync(vehicleId);
            
            // Update service vehicle availability based on maintenance status
            var serviceVehicle = await _serviceVehicleRepository.GetByBaseVehicleIdAsync(vehicleId);
            if (serviceVehicle != null)
            {
                var isInMaintenance = maintenanceRecords.Any(m => 
                    m.Status == MaintenanceStatus.InProgress || 
                    m.Status == MaintenanceStatus.Scheduled);
                
                if (isInMaintenance && serviceVehicle.Status == VehicleStatus.Available)
                {
                    serviceVehicle.SetStatus(VehicleStatus.Maintenance);
                    await _serviceVehicleRepository.UpdateAsync(serviceVehicle);
                }
            }

            return Result.Success();
        }
    }
}
```

#### 3. Notifications Module Integration
```csharp
namespace DriveOps.Breakdown.Integration
{
    public class BreakdownNotificationService
    {
        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;

        public async Task SendDispatchNotificationAsync(EmergencyCall call, Technician technician)
        {
            var user = await _userService.GetUserByIdAsync(technician.UserId);
            if (user == null) return;

            await _notificationService.SendNotificationAsync(new SendNotificationRequest
            {
                Type = NotificationType.EmergencyDispatch,
                RecipientIds = new[] { technician.UserId },
                Title = $"Emergency Assignment - {call.CallType}",
                Message = $"You have been assigned to emergency call {call.CallNumber} at {call.Location.Address}",
                Priority = call.Priority switch
                {
                    CallPriority.Critical => NotificationPriority.Critical,
                    CallPriority.High => NotificationPriority.High,
                    _ => NotificationPriority.Medium
                },
                Data = new Dictionary<string, object>
                {
                    ["call_id"] = call.Id.Value,
                    ["call_number"] = call.CallNumber,
                    ["latitude"] = call.Location.Latitude,
                    ["longitude"] = call.Location.Longitude,
                    ["address"] = call.Location.Address
                },
                PreferredChannels = new[] { NotificationChannel.Push, NotificationChannel.SMS }
            });
        }

        public async Task SendCustomerStatusUpdateAsync(EmergencyCall call, ServiceStatus status)
        {
            var customer = await _userService.GetUserByIdAsync(call.CallerId);
            if (customer == null) return;

            var statusMessage = status switch
            {
                ServiceStatus.Dispatched => "A technician has been assigned to your emergency call",
                ServiceStatus.EnRoute => "Your technician is on the way to your location",
                ServiceStatus.OnSite => "Your technician has arrived and is beginning service",
                ServiceStatus.Completed => "Your emergency service has been completed",
                _ => "Your emergency call status has been updated"
            };

            await _notificationService.SendNotificationAsync(new SendNotificationRequest
            {
                Type = NotificationType.ServiceUpdate,
                RecipientIds = new[] { call.CallerId },
                Title = $"Service Update - {call.CallNumber}",
                Message = statusMessage,
                Priority = NotificationPriority.Medium,
                Data = new Dictionary<string, object>
                {
                    ["call_id"] = call.Id.Value,
                    ["status"] = status.ToString(),
                    ["call_number"] = call.CallNumber
                },
                PreferredChannels = new[] { NotificationChannel.Push, NotificationChannel.InApp }
            });
        }
    }
}
```

#### 4. Files Module Integration
```csharp
namespace DriveOps.Breakdown.Integration
{
    public class BreakdownFileService
    {
        private readonly IFileService _fileService;
        private readonly IServiceReportRepository _reportRepository;

        public async Task<Result<FileId>> UploadServicePhotoAsync(
            EmergencyCallId callId, 
            Stream photoStream, 
            string fileName,
            PhotoType photoType)
        {
            var uploadRequest = new UploadFileRequest
            {
                Stream = photoStream,
                FileName = fileName,
                ContentType = "image/jpeg",
                Category = "service_photos",
                Metadata = new Dictionary<string, string>
                {
                    ["call_id"] = callId.Value.ToString(),
                    ["photo_type"] = photoType.ToString(),
                    ["module"] = "breakdown"
                }
            };

            var uploadResult = await _fileService.UploadFileAsync(uploadRequest);
            if (!uploadResult.IsSuccess)
                return Result.Failure<FileId>(uploadResult.Error);

            return Result.Success(uploadResult.Data.FileId);
        }

        public async Task<Result<FileId>> GenerateServiceReportPdfAsync(EmergencyCallId callId)
        {
            var report = await _reportRepository.GetByCallIdAsync(callId);
            if (report == null)
                return Result.Failure<FileId>("Service report not found");

            // Generate PDF from report data
            var pdfStream = await GenerateReportPdfAsync(report);
            
            var uploadRequest = new UploadFileRequest
            {
                Stream = pdfStream,
                FileName = $"service_report_{report.CallNumber}_{DateTime.UtcNow:yyyyMMdd}.pdf",
                ContentType = "application/pdf",
                Category = "service_reports",
                Metadata = new Dictionary<string, string>
                {
                    ["call_id"] = callId.Value.ToString(),
                    ["report_type"] = "service_completion",
                    ["module"] = "breakdown"
                }
            };

            var uploadResult = await _fileService.UploadFileAsync(uploadRequest);
            return uploadResult.IsSuccess 
                ? Result.Success(uploadResult.Data.FileId)
                : Result.Failure<FileId>(uploadResult.Error);
        }
    }
}
```

### External Service Integration

#### GPS and Mapping Services Integration
```csharp
namespace DriveOps.Breakdown.Infrastructure.ExternalServices
{
    public interface IMapService
    {
        Task<RouteInfo> CalculateRouteAsync(Location from, Location to);
        Task<double> CalculateDistanceAsync(Location from, Location to);
        Task<Location> GeocodeAddressAsync(string address);
        Task<string> ReverseGeocodeAsync(double latitude, double longitude);
        Task<TrafficInfo> GetTrafficInfoAsync(Location from, Location to);
    }

    public class GoogleMapsService : IMapService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public async Task<RouteInfo> CalculateRouteAsync(Location from, Location to)
        {
            var url = $"https://maps.googleapis.com/maps/api/directions/json" +
                     $"?origin={from.Latitude},{from.Longitude}" +
                     $"&destination={to.Latitude},{to.Longitude}" +
                     $"&key={_apiKey}" +
                     "&departure_time=now" +
                     "&traffic_model=best_guess";

            var response = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<GoogleDirectionsResponse>(response);

            if (result?.Routes?.Any() == true)
            {
                var route = result.Routes.First();
                return new RouteInfo
                {
                    Distance = route.Legs.Sum(l => l.Distance.Value),
                    Duration = route.Legs.Sum(l => l.Duration.Value),
                    DurationInTraffic = route.Legs.Sum(l => l.DurationInTraffic?.Value ?? l.Duration.Value),
                    Polyline = route.OverviewPolyline.Points
                };
            }

            throw new InvalidOperationException("No route found");
        }

        public async Task<TrafficInfo> GetTrafficInfoAsync(Location from, Location to)
        {
            var route = await CalculateRouteAsync(from, to);
            var delayMinutes = (route.DurationInTraffic - route.Duration) / 60;

            return new TrafficInfo
            {
                DelayMinutes = Math.Max(0, delayMinutes),
                TrafficLevel = delayMinutes switch
                {
                    <= 5 => TrafficLevel.Light,
                    <= 15 => TrafficLevel.Moderate,
                    <= 30 => TrafficLevel.Heavy,
                    _ => TrafficLevel.Severe
                }
            };
        }
    }

    public interface IWeatherService
    {
        Task<WeatherConditions> GetCurrentConditionsAsync(Location location);
        Task<WeatherForecast> GetForecastAsync(Location location, int hours = 24);
    }

    public class OpenWeatherMapService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public async Task<WeatherConditions> GetCurrentConditionsAsync(Location location)
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather" +
                     $"?lat={location.Latitude}&lon={location.Longitude}" +
                     $"&appid={_apiKey}&units=metric";

            var response = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<OpenWeatherResponse>(response);

            return new WeatherConditions
            {
                Temperature = result.Main.Temp,
                Humidity = result.Main.Humidity,
                WindSpeed = result.Wind.Speed,
                Description = result.Weather.First().Description,
                IsAdverse = IsAdverseWeather(result)
            };
        }

        private bool IsAdverseWeather(OpenWeatherResponse weather)
        {
            var conditions = weather.Weather.First().Main.ToLower();
            return conditions.Contains("rain") || 
                   conditions.Contains("snow") || 
                   conditions.Contains("storm") ||
                   weather.Wind.Speed > 10; // High wind conditions
        }
    }
}
```

#### Insurance Provider Integration
```csharp
namespace DriveOps.Breakdown.Infrastructure.Insurance
{
    public interface IInsuranceService
    {
        Task<InsuranceValidationResult> ValidatePolicyAsync(string policyNumber, string partnerCode);
        Task<ClaimSubmissionResult> SubmitClaimAsync(InsuranceClaimRequest request);
        Task<ClaimStatusResult> GetClaimStatusAsync(string claimNumber, string partnerCode);
        Task<BillingResult> SubmitBillingAsync(InsuranceBillingRequest request);
    }

    public class InsuranceIntegrationService : IInsuranceService
    {
        private readonly IInsurancePartnerRepository _partnerRepository;
        private readonly Dictionary<string, IInsuranceProvider> _providers;

        public InsuranceIntegrationService(
            IInsurancePartnerRepository partnerRepository,
            IServiceProvider serviceProvider)
        {
            _partnerRepository = partnerRepository;
            _providers = new Dictionary<string, IInsuranceProvider>
            {
                ["ADMIRAL"] = serviceProvider.GetRequiredService<AdmiralInsuranceProvider>(),
                ["AXA"] = serviceProvider.GetRequiredService<AxaInsuranceProvider>(),
                ["ALLIANZ"] = serviceProvider.GetRequiredService<AllianzInsuranceProvider>()
            };
        }

        public async Task<ClaimSubmissionResult> SubmitClaimAsync(InsuranceClaimRequest request)
        {
            var partner = await _partnerRepository.GetByCodeAsync(request.PartnerCode);
            if (partner == null)
                return ClaimSubmissionResult.Failure("Insurance partner not found");

            if (!_providers.TryGetValue(partner.PartnerCode, out var provider))
                return ClaimSubmissionResult.Failure("Insurance provider not configured");

            return await provider.SubmitClaimAsync(request);
        }
    }

    public class AdmiralInsuranceProvider : IInsuranceProvider
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public async Task<ClaimSubmissionResult> SubmitClaimAsync(InsuranceClaimRequest request)
        {
            var admiralRequest = new AdmiralClaimRequest
            {
                PolicyNumber = request.PolicyNumber,
                ClaimType = "BREAKDOWN",
                IncidentDate = request.IncidentDate,
                Location = new AdmiralLocation
                {
                    Latitude = request.Location.Latitude,
                    Longitude = request.Location.Longitude,
                    Address = request.Location.Address
                },
                ServiceDetails = new AdmiralServiceDetails
                {
                    ServiceType = request.ServiceType,
                    Description = request.Description,
                    TotalCost = request.TotalCost,
                    ServiceProviderName = "DriveOps",
                    TechnicianDetails = request.TechnicianDetails
                },
                Supporting Documents = request.SupportingDocuments
            };

            var response = await _httpClient.PostAsync(
                "/api/claims/submit", 
                JsonContent.Create(admiralRequest));

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AdmiralClaimResponse>();
                return ClaimSubmissionResult.Success(result.ClaimNumber, result.ReferenceNumber);
            }

            return ClaimSubmissionResult.Failure("Failed to submit claim to Admiral");
        }
    }
}
```

---

## Security & Compliance

### Emergency Services Compliance
The BREAKDOWN module implements comprehensive security and compliance measures:

#### Data Protection and Privacy
- **Location Data Encryption**: All GPS coordinates are encrypted at rest and in transit
- **Customer Information Protection**: PII is masked in logs and audit trails
- **Data Retention Policies**: Emergency call data retained per regulatory requirements
- **Access Control**: Role-based access to sensitive emergency information

#### Financial Security
- **Payment Card Industry (PCI) Compliance**: Secure payment processing for customer charges
- **Insurance Data Security**: Encrypted communication with insurance partners
- **Audit Logging**: Complete financial transaction audit trails
- **Fraud Detection**: Automated monitoring for suspicious billing patterns

#### Emergency Services Regulations
- **Response Time Compliance**: SLA monitoring and violation alerting
- **Emergency Call Recording**: Secure storage of emergency communications
- **Incident Documentation**: Complete chain of custody for service reports
- **Quality Assurance**: Regular compliance audits and reporting

### Implementation Examples

```csharp
namespace DriveOps.Breakdown.Security
{
    public class BreakdownSecurityService
    {
        private readonly IEncryptionService _encryptionService;
        private readonly IAuditLogger _auditLogger;
        private readonly IAccessControlService _accessControl;

        public async Task<Result> LogEmergencyCallAccessAsync(
            EmergencyCallId callId, 
            UserId userId, 
            string action)
        {
            // Log all access to emergency call data
            await _auditLogger.LogAsync(new AuditEvent
            {
                EventType = "EmergencyCallAccess",
                EntityType = "EmergencyCall",
                EntityId = callId.Value.ToString(),
                UserId = userId,
                Action = action,
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Current?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = HttpContext.Current?.Request?.Headers?.UserAgent
            });

            return Result.Success();
        }

        public async Task<bool> CanAccessEmergencyCallAsync(EmergencyCallId callId, UserId userId)
        {
            var userRoles = await _accessControl.GetUserRolesAsync(userId);
            
            // Dispatch operators can access all calls
            if (userRoles.Contains("DispatchOperator"))
                return true;

            // Technicians can only access their assigned calls
            if (userRoles.Contains("Technician"))
            {
                var call = await GetEmergencyCallAsync(callId);
                return call?.AssignedTechnicianId == userId;
            }

            // Customers can only access their own calls
            if (userRoles.Contains("Customer"))
            {
                var call = await GetEmergencyCallAsync(callId);
                return call?.CallerId == userId;
            }

            return false;
        }

        public async Task<string> EncryptLocationDataAsync(double latitude, double longitude)
        {
            var locationData = $"{latitude},{longitude}";
            return await _encryptionService.EncryptAsync(locationData);
        }

        public async Task<(double latitude, double longitude)> DecryptLocationDataAsync(string encryptedData)
        {
            var decrypted = await _encryptionService.DecryptAsync(encryptedData);
            var parts = decrypted.Split(',');
            return (double.Parse(parts[0]), double.Parse(parts[1]));
        }
    }

    public class BreakdownComplianceService
    {
        private readonly IEmergencyCallRepository _callRepository;
        private readonly ISlaMonitoringService _slaMonitoring;
        private readonly IComplianceReporter _complianceReporter;

        public async Task<ComplianceReport> GenerateMonthlyComplianceReportAsync(DateTime month)
        {
            var startDate = new DateTime(month.Year, month.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var calls = await _callRepository.GetCallsInPeriodAsync(startDate, endDate);
            var slaViolations = await _slaMonitoring.GetViolationsInPeriodAsync(startDate, endDate);

            return new ComplianceReport
            {
                ReportPeriod = month,
                TotalCalls = calls.Count(),
                AverageResponseTime = calls.Average(c => (c.ServiceStartedAt - c.CallReceivedAt)?.TotalMinutes ?? 0),
                SlaCompliance = CalculateSlaCompliance(calls, slaViolations),
                DataRetentionCompliance = await CheckDataRetentionComplianceAsync(),
                SecurityAuditResults = await GetSecurityAuditResultsAsync(startDate, endDate)
            };
        }

        private async Task<bool> CheckDataRetentionComplianceAsync()
        {
            var retentionDate = DateTime.UtcNow.AddYears(-7); // 7-year retention
            var oldCalls = await _callRepository.GetCallsOlderThanAsync(retentionDate);
            
            // Ensure old data is properly archived or deleted
            return !oldCalls.Any();
        }
    }
}
```

---

## Performance Considerations

### 24/7 Operations Optimization

#### High Availability Architecture
- **Load Balancing**: Multiple instances for emergency call handling
- **Database Replication**: Read replicas for geographic distribution
- **Circuit Breakers**: Fault tolerance for external service dependencies
- **Caching Strategy**: Redis for real-time location data and call status

#### Geospatial Performance
- **Spatial Indexes**: PostGIS indexes for efficient location queries
- **Location Clustering**: Technician grouping for dispatch optimization
- **Route Caching**: Pre-calculated routes for common destinations
- **Real-time Updates**: Optimized SignalR message batching

### Implementation Examples

```csharp
namespace DriveOps.Breakdown.Performance
{
    public class GeospatialQueryOptimizer
    {
        private readonly IMemoryCache _cache;
        private readonly IDistributedCache _distributedCache;

        public async Task<IEnumerable<Technician>> FindNearbyTechniciansAsync(
            CallLocation location, 
            double radiusKm, 
            CallType callType)
        {
            var cacheKey = $"nearby_technicians_{location.Latitude}_{location.Longitude}_{radiusKm}_{callType}";
            
            // Check cache first
            if (_cache.TryGetValue(cacheKey, out IEnumerable<Technician> cachedResult))
                return cachedResult;

            // Use spatial index for efficient query
            var query = $@"
                SELECT t.*, ST_Distance(t.current_location, ST_SetSRID(ST_MakePoint({location.Longitude}, {location.Latitude}), 4326)) as distance
                FROM breakdown.technicians t
                JOIN breakdown.technician_skills ts ON t.id = ts.technician_id
                WHERE t.status = 0 -- Available
                AND t.current_location IS NOT NULL
                AND ST_DWithin(
                    t.current_location, 
                    ST_SetSRID(ST_MakePoint({location.Longitude}, {location.Latitude}), 4326),
                    {radiusKm * 1000}
                )
                AND ts.skill_type = {(int)GetRequiredSkillType(callType)}
                ORDER BY distance
                LIMIT 10";

            var result = await ExecuteGeospatialQueryAsync<Technician>(query);
            
            // Cache for 30 seconds (technicians move frequently)
            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30));
            
            return result;
        }

        public async Task<RouteInfo> GetOptimizedRouteAsync(Location from, Location to)
        {
            var cacheKey = $"route_{from.Latitude}_{from.Longitude}_{to.Latitude}_{to.Longitude}";
            
            // Check distributed cache for longer-term route caching
            var cachedRoute = await _distributedCache.GetStringAsync(cacheKey);
            if (cachedRoute != null)
                return JsonSerializer.Deserialize<RouteInfo>(cachedRoute);

            var route = await CalculateRouteWithTrafficAsync(from, to);
            
            // Cache routes for 10 minutes
            await _distributedCache.SetStringAsync(
                cacheKey, 
                JsonSerializer.Serialize(route),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });

            return route;
        }
    }

    public class RealTimeUpdateBatcher
    {
        private readonly IHubContext<BreakdownHub> _hubContext;
        private readonly ConcurrentQueue<LocationUpdate> _locationUpdates = new();
        private readonly Timer _batchTimer;

        public RealTimeUpdateBatcher(IHubContext<BreakdownHub> hubContext)
        {
            _hubContext = hubContext;
            _batchTimer = new Timer(ProcessLocationUpdates, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        public void QueueLocationUpdate(LocationUpdate update)
        {
            _locationUpdates.Enqueue(update);
        }

        private async void ProcessLocationUpdates(object? state)
        {
            var updates = new List<LocationUpdate>();
            
            // Drain the queue
            while (_locationUpdates.TryDequeue(out var update))
            {
                updates.Add(update);
            }

            if (!updates.Any()) return;

            // Group by tenant and send batch updates
            var updatesByTenant = updates.GroupBy(u => u.TenantId);
            
            foreach (var tenantGroup in updatesByTenant)
            {
                await _hubContext.Clients.Group($"dispatch_{tenantGroup.Key}")
                    .SendAsync("BatchLocationUpdate", tenantGroup.ToArray());
            }
        }
    }

    public class BreakdownCircuitBreaker
    {
        private readonly CircuitBreakerPolicy _circuitBreaker;

        public BreakdownCircuitBreaker()
        {
            _circuitBreaker = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromMinutes(1),
                    onBreak: (exception, duration) =>
                    {
                        // Log circuit breaker opening
                    },
                    onReset: () =>
                    {
                        // Log circuit breaker closing
                    });
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            return await _circuitBreaker.ExecuteAsync(operation);
        }
    }
}
```

---

## API Controllers and gRPC Services

### REST API Controllers

```csharp
namespace DriveOps.Breakdown.API.Controllers
{
    [ApiController]
    [Route("api/breakdown/calls")]
    [Authorize]
    public class EmergencyCallsController : ControllerBase
    {
        private readonly IMediator _mediator;

        [HttpPost]
        [ProducesResponseType(typeof(CreateEmergencyCallResponse), 201)]
        public async Task<IActionResult> CreateEmergencyCall([FromBody] CreateEmergencyCallRequest request)
        {
            var command = new CreateEmergencyCallCommand(
                request.VehicleId,
                UserId.FromClaims(User),
                request.CallType,
                request.Description,
                request.Latitude,
                request.Longitude,
                request.Address,
                request.Landmark,
                request.RequestedPriority);

            var result = await _mediator.Send(command);
            
            return result.IsSuccess 
                ? CreatedAtAction(nameof(GetEmergencyCall), new { id = result.Data.Value }, new { CallId = result.Data.Value })
                : BadRequest(result.Error);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(EmergencyCallDetailDto), 200)]
        public async Task<IActionResult> GetEmergencyCall(Guid id)
        {
            var query = new GetEmergencyCallQuery(new EmergencyCallId(id));
            var result = await _mediator.Send(query);
            
            return result.IsSuccess ? Ok(result.Data) : NotFound();
        }

        [HttpPost("{id:guid}/dispatch")]
        [Authorize(Roles = "DispatchOperator,Supervisor")]
        public async Task<IActionResult> DispatchTechnician(Guid id, [FromBody] DispatchTechnicianRequest request)
        {
            var command = new DispatchTechnicianCommand(
                new EmergencyCallId(id),
                new TechnicianId(request.TechnicianId),
                new ServiceVehicleId(request.VehicleId),
                UserId.FromClaims(User));

            var result = await _mediator.Send(command);
            
            return result.IsSuccess ? Ok() : BadRequest(result.Error);
        }

        [HttpPost("{id:guid}/status")]
        [Authorize(Roles = "Technician")]
        public async Task<IActionResult> UpdateServiceStatus(Guid id, [FromBody] UpdateServiceStatusRequest request)
        {
            var command = new UpdateServiceStatusCommand(
                new EmergencyCallId(id),
                request.Status,
                UserId.FromClaims(User),
                request.Notes,
                request.Latitude,
                request.Longitude);

            var result = await _mediator.Send(command);
            
            return result.IsSuccess ? Ok() : BadRequest(result.Error);
        }

        [HttpGet("active")]
        [Authorize(Roles = "DispatchOperator,Supervisor")]
        public async Task<IActionResult> GetActiveCalls([FromQuery] GetActiveCallsRequest request)
        {
            var query = new GetActiveCallsQuery(
                request.Page,
                request.PageSize,
                request.Priority,
                request.Status,
                request.ServiceArea);

            var result = await _mediator.Send(query);
            
            return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
        }
    }

    [ApiController]
    [Route("api/breakdown/technicians")]
    [Authorize(Roles = "Technician")]
    public class TechniciansController : ControllerBase
    {
        private readonly IMediator _mediator;

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var userId = UserId.FromClaims(User);
            var query = new GetTechnicianDashboardQuery(await GetTechnicianIdFromUserIdAsync(userId));
            var result = await _mediator.Send(query);
            
            return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
        }

        [HttpPost("location")]
        public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationRequest request)
        {
            var command = new UpdateTechnicianLocationCommand(
                UserId.FromClaims(User),
                request.Latitude,
                request.Longitude);

            var result = await _mediator.Send(command);
            
            return result.IsSuccess ? Ok() : BadRequest(result.Error);
        }

        [HttpGet("nearby-calls")]
        public async Task<IActionResult> GetNearbyCalls([FromQuery] double radiusKm = 25)
        {
            var query = new GetNearbyCallsQuery(UserId.FromClaims(User), radiusKm);
            var result = await _mediator.Send(query);
            
            return result.IsSuccess ? Ok(result.Data) : BadRequest(result.Error);
        }
    }
}
```

### gRPC Services

```csharp
namespace DriveOps.Breakdown.API.Grpc
{
    public class BreakdownService : Breakdown.BreakdownBase
    {
        private readonly IMediator _mediator;
        private readonly IMapper _mapper;

        public override async Task<CreateCallResponse> CreateEmergencyCall(
            CreateCallRequest request, 
            ServerCallContext context)
        {
            var command = new CreateEmergencyCallCommand(
                new VehicleId(Guid.Parse(request.VehicleId)),
                new UserId(Guid.Parse(request.CallerId)),
                (CallType)request.CallType,
                request.Description,
                request.Location.Latitude,
                request.Location.Longitude,
                request.Location.Address,
                request.Location.Landmark);

            var result = await _mediator.Send(command);
            
            if (result.IsSuccess)
            {
                return new CreateCallResponse
                {
                    CallId = result.Data.Value.ToString(),
                    CallNumber = await GetCallNumberAsync(result.Data),
                    Success = true
                };
            }
            
            throw new RpcException(new Status(StatusCode.InvalidArgument, result.Error));
        }

        public override async Task<GetCallResponse> GetEmergencyCall(
            GetCallRequest request, 
            ServerCallContext context)
        {
            var query = new GetEmergencyCallQuery(new EmergencyCallId(Guid.Parse(request.CallId)));
            var result = await _mediator.Send(query);
            
            if (!result.IsSuccess)
                throw new RpcException(new Status(StatusCode.NotFound, "Emergency call not found"));

            return _mapper.Map<GetCallResponse>(result.Data);
        }

        public override async Task StreamLocationUpdates(
            StreamLocationRequest request,
            IServerStreamWriter<LocationUpdate> responseStream,
            ServerCallContext context)
        {
            var callId = new EmergencyCallId(Guid.Parse(request.CallId));
            
            // Subscribe to location updates for this call
            await foreach (var update in GetLocationUpdatesAsync(callId, context.CancellationToken))
            {
                await responseStream.WriteAsync(new LocationUpdate
                {
                    TechnicianId = update.TechnicianId.ToString(),
                    Latitude = update.Latitude,
                    Longitude = update.Longitude,
                    Heading = update.Heading ?? 0,
                    Speed = update.Speed ?? 0,
                    Timestamp = update.Timestamp.ToTimestamp()
                });
            }
        }
    }
}
```

---

## Conclusion

The BREAKDOWN module provides a comprehensive emergency roadside assistance and breakdown management system that integrates seamlessly with the DriveOps platform. This 39€/month premium module delivers:

### Key Capabilities Delivered
- **24/7 Emergency Operations**: Complete call center and dispatch management
- **Real-time GPS Tracking**: Live vehicle and technician location monitoring
- **Intelligent Dispatching**: Automated optimal technician assignment
- **Insurance Integration**: Direct billing with major insurance providers
- **Mobile Workforce**: Dedicated apps for technicians and customers
- **Performance Analytics**: SLA monitoring and operational KPIs

### Technical Excellence
- **Domain-Driven Design**: Rich domain models with proper aggregate boundaries
- **CQRS Architecture**: Scalable command/query separation with MediatR
- **Real-time Communication**: SignalR for live updates and tracking
- **Geospatial Optimization**: PostGIS for efficient location-based operations
- **High Availability**: Circuit breakers, caching, and fault tolerance
- **Security & Compliance**: Comprehensive emergency services compliance

### Integration Benefits
- **Core Module Synergy**: Seamless integration with Users, Vehicles, Notifications, and Files modules
- **External Service Integration**: GPS services, weather APIs, insurance providers
- **Mobile-First Design**: Offline-capable apps for field operations
- **Enterprise Scalability**: Multi-tenant architecture with geographic distribution

The BREAKDOWN module exemplifies the DriveOps platform's modular architecture, providing specialized emergency services capabilities while maintaining consistency with the overall system design and patterns.
CREATE TABLE breakdown.billing_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    call_id UUID NOT NULL REFERENCES breakdown.emergency_calls(id),
    insurance_claim_id UUID REFERENCES breakdown.insurance_claims(id),
    billing_type INTEGER NOT NULL, -- 0: Insurance, 1: Customer, 2: Warranty, 3: Internal
    total_amount DECIMAL(10,2) NOT NULL,
    tax_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    payment_status INTEGER NOT NULL DEFAULT 0, -- 0: Pending, 1: Paid, 2: Overdue, 3: Cancelled
    invoice_number VARCHAR(100),
    invoice_date DATE,
    due_date DATE,
    paid_date DATE,
    payment_method VARCHAR(50),
    payment_reference VARCHAR(100),
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Billing line items for detailed invoicing
CREATE TABLE breakdown.billing_line_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    billing_record_id UUID NOT NULL REFERENCES breakdown.billing_records(id) ON DELETE CASCADE,
    item_type INTEGER NOT NULL, -- 0: Labor, 1: Parts, 2: Travel, 3: Equipment, 4: Fees
    description TEXT NOT NULL,
    quantity DECIMAL(8,2) NOT NULL DEFAULT 1,
    unit_price DECIMAL(10,2) NOT NULL,
    total_price DECIMAL(10,2) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Service reports and documentation
CREATE TABLE breakdown.service_reports (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    call_id UUID NOT NULL REFERENCES breakdown.emergency_calls(id) ON DELETE CASCADE,
    technician_id UUID NOT NULL REFERENCES breakdown.technicians(id),
    report_content TEXT NOT NULL,
    work_performed TEXT NOT NULL,
    parts_used TEXT,
    time_spent_minutes INTEGER NOT NULL,
    mileage_reading INTEGER,
    
    -- Photos and documentation
    before_photos TEXT[], -- Array of file IDs
    after_photos TEXT[], -- Array of file IDs
    signature_file_id UUID, -- Reference to files.files
    
    -- Quality and completion
    quality_check_passed BOOLEAN NOT NULL DEFAULT TRUE,
    quality_check_notes TEXT,
    completion_status INTEGER NOT NULL DEFAULT 1, -- 0: Incomplete, 1: Complete, 2: PartiallyComplete
    
    submitted_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    approved_at TIMESTAMP WITH TIME ZONE,
    approved_by UUID, -- Reference to users.users
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- SLA monitoring and violations
CREATE TABLE breakdown.sla_violations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    call_id UUID NOT NULL REFERENCES breakdown.emergency_calls(id),
    violation_type INTEGER NOT NULL, -- 0: ResponseTime, 1: ArrivalTime, 2: CompletionTime, 3: CustomerSatisfaction
    target_value INTEGER NOT NULL, -- Target in minutes or rating
    actual_value INTEGER NOT NULL, -- Actual in minutes or rating
    violation_severity INTEGER NOT NULL, -- 0: Minor, 1: Major, 2: Critical
    detected_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    resolved_at TIMESTAMP WITH TIME ZONE,
    resolution_notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Performance metrics and KPIs
CREATE TABLE breakdown.performance_metrics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id) ON DELETE CASCADE,
    metric_date DATE NOT NULL,
    metric_type VARCHAR(100) NOT NULL,
    metric_value DECIMAL(15,2) NOT NULL,
    target_value DECIMAL(15,2),
    unit VARCHAR(50),
    context JSONB, -- Additional context data
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, metric_date, metric_type)
);

-- Customer feedback and ratings
CREATE TABLE breakdown.customer_feedback (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    call_id UUID NOT NULL REFERENCES breakdown.emergency_calls(id) ON DELETE CASCADE,
    customer_id UUID NOT NULL, -- Reference to users.users
    technician_id UUID NOT NULL REFERENCES breakdown.technicians(id),
    overall_rating INTEGER NOT NULL CHECK (overall_rating >= 1 AND overall_rating <= 5),
    response_time_rating INTEGER CHECK (response_time_rating >= 1 AND response_time_rating <= 5),
    technician_rating INTEGER CHECK (technician_rating >= 1 AND technician_rating <= 5),
    service_quality_rating INTEGER CHECK (service_quality_rating >= 1 AND service_quality_rating <= 5),
    communication_rating INTEGER CHECK (communication_rating >= 1 AND communication_rating <= 5),
    comments TEXT,
    would_recommend BOOLEAN,
    submitted_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Geospatial indexes for efficient location queries
CREATE INDEX idx_emergency_calls_location ON breakdown.emergency_calls USING GIST(location_point);
CREATE INDEX idx_service_vehicles_location ON breakdown.service_vehicles USING GIST(current_location);
CREATE INDEX idx_technicians_location ON breakdown.technicians USING GIST(current_location);
CREATE INDEX idx_service_areas_coverage ON breakdown.service_areas USING GIST(coverage_polygon);

-- Performance indexes
CREATE INDEX idx_emergency_calls_tenant_status ON breakdown.emergency_calls(tenant_id, status);
CREATE INDEX idx_emergency_calls_priority_received ON breakdown.emergency_calls(priority DESC, call_received_at);
CREATE INDEX idx_emergency_calls_assigned_technician ON breakdown.emergency_calls(assigned_technician_id);
CREATE INDEX idx_service_vehicles_tenant_status ON breakdown.service_vehicles(tenant_id, status);
CREATE INDEX idx_technicians_tenant_status ON breakdown.technicians(tenant_id, status);
CREATE INDEX idx_call_status_history_call_id ON breakdown.call_status_history(call_id, changed_at DESC);
CREATE INDEX idx_service_assignments_call_id ON breakdown.service_assignments(call_id);
CREATE INDEX idx_insurance_claims_partner_status ON breakdown.insurance_claims(insurance_partner_id, status);
CREATE INDEX idx_billing_records_tenant_status ON breakdown.billing_records(tenant_id, payment_status);
CREATE INDEX idx_performance_metrics_tenant_date ON breakdown.performance_metrics(tenant_id, metric_date DESC);

-- Foreign key constraints to core modules
ALTER TABLE breakdown.emergency_calls 
ADD CONSTRAINT fk_emergency_calls_vehicle 
FOREIGN KEY (vehicle_id) REFERENCES vehicles.vehicles(id);

ALTER TABLE breakdown.emergency_calls 
ADD CONSTRAINT fk_emergency_calls_caller 
FOREIGN KEY (caller_id) REFERENCES users.users(id);

ALTER TABLE breakdown.emergency_calls 
ADD CONSTRAINT fk_emergency_calls_technician 
FOREIGN KEY (assigned_technician_id) REFERENCES users.users(id);

ALTER TABLE breakdown.service_vehicles 
ADD CONSTRAINT fk_service_vehicles_base_vehicle 
FOREIGN KEY (base_vehicle_id) REFERENCES vehicles.vehicles(id);

ALTER TABLE breakdown.service_vehicles 
ADD CONSTRAINT fk_service_vehicles_driver 
FOREIGN KEY (current_driver_id) REFERENCES users.users(id);

ALTER TABLE breakdown.technicians 
ADD CONSTRAINT fk_technicians_user 
FOREIGN KEY (user_id) REFERENCES users.users(id);

ALTER TABLE breakdown.call_status_history 
ADD CONSTRAINT fk_call_status_history_user 
FOREIGN KEY (changed_by) REFERENCES users.users(id);

ALTER TABLE breakdown.call_communications 
ADD CONSTRAINT fk_call_communications_user 
FOREIGN KEY (sent_by) REFERENCES users.users(id);

ALTER TABLE breakdown.service_reports 
ADD CONSTRAINT fk_service_reports_approved_by 
FOREIGN KEY (approved_by) REFERENCES users.users(id);

ALTER TABLE breakdown.customer_feedback 
ADD CONSTRAINT fk_customer_feedback_customer 
FOREIGN KEY (customer_id) REFERENCES users.users(id);

-- Triggers for updated_at timestamps
CREATE OR REPLACE FUNCTION breakdown.update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_emergency_calls_updated_at 
BEFORE UPDATE ON breakdown.emergency_calls 
FOR EACH ROW EXECUTE FUNCTION breakdown.update_updated_at_column();

CREATE TRIGGER update_service_vehicles_updated_at 
BEFORE UPDATE ON breakdown.service_vehicles 
FOR EACH ROW EXECUTE FUNCTION breakdown.update_updated_at_column();

CREATE TRIGGER update_technicians_updated_at 
BEFORE UPDATE ON breakdown.technicians 
FOR EACH ROW EXECUTE FUNCTION breakdown.update_updated_at_column();

CREATE TRIGGER update_technician_skills_updated_at 
BEFORE UPDATE ON breakdown.technician_skills 
FOR EACH ROW EXECUTE FUNCTION breakdown.update_updated_at_column();

CREATE TRIGGER update_service_assignments_updated_at 
BEFORE UPDATE ON breakdown.service_assignments 
FOR EACH ROW EXECUTE FUNCTION breakdown.update_updated_at_column();

CREATE TRIGGER update_insurance_claims_updated_at 
BEFORE UPDATE ON breakdown.insurance_claims 
FOR EACH ROW EXECUTE FUNCTION breakdown.update_updated_at_column();

CREATE TRIGGER update_billing_records_updated_at 
BEFORE UPDATE ON breakdown.billing_records 
FOR EACH ROW EXECUTE FUNCTION breakdown.update_updated_at_column();
```

---
                CompletedCalls = todayCalls.Count(c => c.Status == CallStatus.Completed),
                AverageResponseTime = todayCalls.Any() 
                    ? todayCalls.Average(c => (c.ServiceStartedAt - c.DispatchedAt)?.TotalMinutes ?? 0)
                    : 0,
                CustomerRating = todayCalls.Any() 
                    ? todayCalls.Average(c => c.CustomerRating ?? 0)
                    : 0
            };
        }

        private async Task<TechnicianStatsDto> CalculateMonthStatsAsync(UserId technicianUserId)
        {
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var monthCalls = await _callRepository.GetTechnicianCallsInPeriodAsync(
                technicianUserId, monthStart, monthEnd);

            return new TechnicianStatsDto
            {
                TotalCalls = monthCalls.Count(),
                CompletedCalls = monthCalls.Count(c => c.Status == CallStatus.Completed),
                AverageResponseTime = monthCalls.Any() 
                    ? monthCalls.Average(c => (c.ServiceStartedAt - c.DispatchedAt)?.TotalMinutes ?? 0)
                    : 0,
                CustomerRating = monthCalls.Any() 
                    ? monthCalls.Average(c => c.CustomerRating ?? 0)
                    : 0
            };
        }
    }

    public record GetDispatchCenterDashboardQuery() : IRequest<Result<DispatchCenterDashboardDto>>;

    public class GetDispatchCenterDashboardHandler : IRequestHandler<GetDispatchCenterDashboardQuery, Result<DispatchCenterDashboardDto>>
    {
        private readonly IEmergencyCallRepository _callRepository;
        private readonly ITechnicianRepository _technicianRepository;
        private readonly IServiceVehicleRepository _vehicleRepository;
        private readonly ITenantContext _tenantContext;
        private readonly IMapper _mapper;

        public async Task<Result<DispatchCenterDashboardDto>> Handle(
            GetDispatchCenterDashboardQuery request, 
            CancellationToken cancellationToken)
        {
            var activeCalls = await _callRepository.GetActiveCallsAsync(_tenantContext.TenantId);
            var availableTechnicians = await _technicianRepository.GetAvailableTechniciansAsync(_tenantContext.TenantId);
            var activeVehicles = await _vehicleRepository.GetActiveVehiclesAsync(_tenantContext.TenantId);

            var dashboard = new DispatchCenterDashboardDto
            {
                ActiveCalls = _mapper.Map<IEnumerable<EmergencyCallSummaryDto>>(activeCalls),
                AvailableTechnicians = _mapper.Map<IEnumerable<TechnicianSummaryDto>>(availableTechnicians),
                ActiveVehicles = _mapper.Map<IEnumerable<ServiceVehicleDto>>(activeVehicles),
                CallsPendingDispatch = activeCalls.Count(c => c.Status == CallStatus.Received),
                AverageResponseTime = await CalculateAverageResponseTimeAsync(),
                SlaViolations = await GetSlaViolationsAsync()
            };

            return Result.Success(dashboard);
        }

        private async Task<double> CalculateAverageResponseTimeAsync()
        {
            var completedCallsToday = await _callRepository.GetCompletedCallsTodayAsync(_tenantContext.TenantId);
            return completedCallsToday.Any() 
                ? completedCallsToday.Average(c => (c.ServiceStartedAt - c.CallReceivedAt)?.TotalMinutes ?? 0)
                : 0;
        }

        private async Task<IEnumerable<SlaViolationDto>> GetSlaViolationsAsync()
        {
            var slaViolations = await _callRepository.GetSlaViolationsAsync(_tenantContext.TenantId);
            return _mapper.Map<IEnumerable<SlaViolationDto>>(slaViolations);
        }
    }
}
```

---

## Infrastructure Layer

### Repository Implementations with Geospatial Support

```csharp
namespace DriveOps.Breakdown.Infrastructure.Persistence
{
    public class EmergencyCallRepository : IEmergencyCallRepository
    {
        private readonly BreakdownDbContext _context;

        public async Task<EmergencyCall?> GetByIdAsync(EmergencyCallId id)
        {
            return await _context.EmergencyCalls
                .Include(c => c.StatusHistory)
                .Include(c => c.Communications)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<EmergencyCall?> GetByIdWithDetailsAsync(EmergencyCallId id)
        {
            return await _context.EmergencyCalls
                .Include(c => c.StatusHistory)
                .Include(c => c.Communications)
                .Include(c => c.Vehicle)
                .Include(c => c.Caller)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<IEnumerable<EmergencyCall>> GetActiveCallsAsync(TenantId tenantId)
        {
            return await _context.EmergencyCalls
                .Where(c => c.TenantId == tenantId && 
                           c.Status != CallStatus.Completed && 
                           c.Status != CallStatus.Cancelled)
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.CallReceivedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<EmergencyCall>> GetCallsInRadiusAsync(
            TenantId tenantId,
            double latitude,
            double longitude,
            double radiusKm)
        {
            // Using PostGIS spatial functions for efficient geospatial queries
            var point = new Point(longitude, latitude) { SRID = 4326 };
            
            return await _context.EmergencyCalls
                .Where(c => c.TenantId == tenantId &&
                           c.Location.DistanceFrom(point) <= radiusKm * 1000) // Convert km to meters
                .OrderBy(c => c.Location.DistanceFrom(point))
                .ToListAsync();
        }

        public async Task<PagedResult<EmergencyCall>> GetPagedAsync(
            ISpecification<EmergencyCall> specification,
            int page,
            int pageSize)
        {
            var query = _context.EmergencyCalls.AsQueryable();
            
            if (specification != null)
                query = query.Where(specification.ToExpression());

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<EmergencyCall>(items, totalCount, page, pageSize);
        }

        public async Task<IEnumerable<EmergencyCall>> GetActiveTechnicianCallsAsync(UserId technicianUserId)
        {
            return await _context.EmergencyCalls
                .Where(c => c.AssignedTechnicianId == technicianUserId &&
                           c.Status != CallStatus.Completed &&
                           c.Status != CallStatus.Cancelled)
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.CallReceivedAt)
                .ToListAsync();
        }

        public async Task AddAsync(EmergencyCall call)
        {
            await _context.EmergencyCalls.AddAsync(call);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(EmergencyCall call)
        {
            _context.EmergencyCalls.Update(call);
            await _context.SaveChangesAsync();
        }
    }

    public class TechnicianRepository : ITechnicianRepository
    {
        private readonly BreakdownDbContext _context;

        public async Task<IEnumerable<Technician>> FindAvailableInRadiusAsync(
            double latitude,
            double longitude,
            double radiusKm,
            CallType callType)
        {
            var point = new Point(longitude, latitude) { SRID = 4326 };

            return await _context.Technicians
                .Where(t => t.Status == TechnicianStatus.Available &&
                           t.CurrentLocation != null &&
                           t.CurrentLocation.Location.DistanceFrom(point) <= radiusKm * 1000)
                .Where(t => t.Skills.Any(s => 
                    (callType == CallType.Towing && s.SkillType == ServiceSkillType.Towing) ||
                    (callType == CallType.Mechanical && s.SkillType == ServiceSkillType.Mechanical) ||
                    (callType == CallType.Electrical && s.SkillType == ServiceSkillType.Electrical) ||
                    (callType == CallType.FuelDelivery && s.SkillType == ServiceSkillType.FuelDelivery)))
                .OrderBy(t => t.CurrentLocation.Location.DistanceFrom(point))
                .ThenByDescending(t => t.AverageRating)
                .Take(10) // Limit to top 10 candidates
                .ToListAsync();
        }

        public async Task<IEnumerable<Technician>> GetAvailableTechniciansAsync(TenantId tenantId)
        {
            return await _context.Technicians
                .Where(t => t.TenantId == tenantId && t.Status == TechnicianStatus.Available)
                .Include(t => t.Skills)
                .OrderByDescending(t => t.AverageRating)
                .ToListAsync();
        }

        public async Task UpdateLocationAsync(TechnicianId technicianId, double latitude, double longitude)
        {
            var technician = await _context.Technicians.FindAsync(technicianId);
            if (technician != null)
            {
                technician.UpdateLocation(latitude, longitude);
                await _context.SaveChangesAsync();
            }
        }
    }

    public class ServiceVehicleRepository : IServiceVehicleRepository
    {
        private readonly BreakdownDbContext _context;

        public async Task<IEnumerable<ServiceVehicle>> GetAvailableVehiclesAsync(TenantId tenantId)
        {
            return await _context.ServiceVehicles
                .Where(v => v.TenantId == tenantId && v.Status == VehicleStatus.Available)
                .Include(v => v.Equipment)
                .ToListAsync();
        }

        public async Task<IEnumerable<ServiceVehicle>> GetVehiclesInRadiusAsync(
            TenantId tenantId,
            double latitude,
            double longitude,
            double radiusKm)
        {
            var point = new Point(longitude, latitude) { SRID = 4326 };

            return await _context.ServiceVehicles
                .Where(v => v.TenantId == tenantId &&
                           v.CurrentLocation != null &&
                           v.CurrentLocation.Location.DistanceFrom(point) <= radiusKm * 1000)
                .OrderBy(v => v.CurrentLocation.Location.DistanceFrom(point))
                .ToListAsync();
        }

        public async Task UpdateLocationAsync(ServiceVehicleId vehicleId, double latitude, double longitude, double? heading = null, double? speed = null)
        {
            var vehicle = await _context.ServiceVehicles.FindAsync(vehicleId);
            if (vehicle != null)
            {
                vehicle.UpdateLocation(latitude, longitude, heading, speed);
                await _context.SaveChangesAsync();
            }
        }
    }
}
```

### Real-time Services with SignalR

```csharp
namespace DriveOps.Breakdown.Infrastructure.SignalR
{
    [Authorize]
    public class BreakdownHub : Hub
    {
        private readonly ITenantContext _tenantContext;
        private readonly ITechnicianRepository _technicianRepository;
        private readonly ILogger<BreakdownHub> _logger;

        public async Task JoinDispatchGroup()
        {
            var tenantId = _tenantContext.TenantId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"dispatch_{tenantId}");
            _logger.LogInformation("User {UserId} joined dispatch group for tenant {TenantId}", 
                Context.UserIdentifier, tenantId);
        }

        public async Task JoinTechnicianGroup(string technicianId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"technician_{technicianId}");
            _logger.LogInformation("Technician {TechnicianId} joined technician group", technicianId);
        }

        public async Task JoinCustomerGroup(string customerId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"customer_{customerId}");
            _logger.LogInformation("Customer {CustomerId} joined customer group", customerId);
        }

        public async Task UpdateTechnicianLocation(double latitude, double longitude)
        {
            var userId = Context.UserIdentifier;
            if (userId != null)
            {
                var technician = await _technicianRepository.GetByUserIdAsync(new UserId(Guid.Parse(userId)));
                if (technician != null)
                {
                    technician.UpdateLocation(latitude, longitude);
                    
                    // Broadcast location update to dispatch center
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dispatch_{_tenantContext.TenantId}");
                    await Clients.Group($"dispatch_{_tenantContext.TenantId}").SendAsync("TechnicianLocationUpdate", new
                    {
                        TechnicianId = technician.Id.Value,
                        Latitude = latitude,
                        Longitude = longitude,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        public async Task UpdateCallStatus(string callId, string status, string? notes = null)
        {
            var userId = Context.UserIdentifier;
            if (userId != null)
            {
                // Broadcast status update to relevant parties
                await Clients.Group($"call_{callId}").SendAsync("CallStatusUpdate", new
                {
                    CallId = callId,
                    Status = status,
                    Notes = notes,
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("User {UserId} disconnected from breakdown hub", Context.UserIdentifier);
            await base.OnDisconnectedAsync(exception);
        }
    }

    public interface IBreakdownRealtimeService
    {
        Task SendDispatchNotificationAsync(EmergencyCall call, Technician technician);
        Task SendStatusUpdateToCustomerAsync(string customerId, object statusUpdate);
        Task SendLocationUpdateToDispatchAsync(string technicianId, double latitude, double longitude);
        Task BroadcastEmergencyCallAsync(EmergencyCall call);
    }

    public class BreakdownRealtimeService : IBreakdownRealtimeService
    {
        private readonly IHubContext<BreakdownHub> _hubContext;
        private readonly ITenantContext _tenantContext;

        public BreakdownRealtimeService(IHubContext<BreakdownHub> hubContext, ITenantContext tenantContext)
        {
            _hubContext = hubContext;
            _tenantContext = tenantContext;
        }

        public async Task SendDispatchNotificationAsync(EmergencyCall call, Technician technician)
        {
            await _hubContext.Clients.Group($"technician_{technician.Id}").SendAsync("CallAssigned", new
            {
                CallId = call.Id.Value,
                CallNumber = call.CallNumber,
                CallType = call.CallType.ToString(),
                Priority = call.Priority.ToString(),
                Description = call.Description,
                Location = new
                {
                    call.Location.Latitude,
                    call.Location.Longitude,
                    call.Location.Address,
                    call.Location.Landmark
                },
                CustomerInfo = new
                {
                    CallerId = call.CallerId.Value,
                    // Additional customer details would be loaded separately
                },
                AssignedAt = DateTime.UtcNow
            });
        }

        public async Task SendStatusUpdateToCustomerAsync(string customerId, object statusUpdate)
        {
            await _hubContext.Clients.Group($"customer_{customerId}").SendAsync("ServiceStatusUpdate", statusUpdate);
        }

        public async Task SendLocationUpdateToDispatchAsync(string technicianId, double latitude, double longitude)
        {
            await _hubContext.Clients.Group($"dispatch_{_tenantContext.TenantId}").SendAsync("TechnicianLocationUpdate", new
            {
                TechnicianId = technicianId,
                Latitude = latitude,
                Longitude = longitude,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public async Task BroadcastEmergencyCallAsync(EmergencyCall call)
        {
            await _hubContext.Clients.Group($"dispatch_{_tenantContext.TenantId}").SendAsync("NewEmergencyCall", new
            {
                CallId = call.Id.Value,
                CallNumber = call.CallNumber,
                Priority = call.Priority.ToString(),
                CallType = call.CallType.ToString(),
                Description = call.Description,
                Location = new
                {
                    call.Location.Latitude,
                    call.Location.Longitude,
                    call.Location.Address
                },
                ReceivedAt = call.CallReceivedAt
            });
        }
    }
}
```

---
        
        private readonly List<VehicleEquipment> _equipment = new();
        public IReadOnlyCollection<VehicleEquipment> Equipment => _equipment.AsReadOnly();
        
        private readonly List<ServiceAssignment> _assignments = new();
        public IReadOnlyCollection<ServiceAssignment> CurrentAssignments => 
            _assignments.Where(a => a.Status == AssignmentStatus.Active).ToList().AsReadOnly();

        public void UpdateLocation(double latitude, double longitude, double? heading = null, double? speed = null)
        {
            var newLocation = new VehicleLocation(latitude, longitude, heading, speed);
            var previousLocation = CurrentLocation;
            
            CurrentLocation = newLocation;
            LastLocationUpdate = DateTime.UtcNow;

            AddDomainEvent(new VehicleLocationUpdatedEvent(
                TenantId, Id, previousLocation, newLocation));
        }

        public void AssignToCall(EmergencyCallId callId, UserId technicianId)
        {
            if (Status != VehicleStatus.Available)
                throw new InvalidOperationException($"Vehicle not available for assignment (status: {Status})");

            var assignment = new ServiceAssignment(
                Id, callId, technicianId, DateTime.UtcNow);
            
            _assignments.Add(assignment);
            Status = VehicleStatus.Dispatched;
            CurrentDriverId = technicianId;

            AddDomainEvent(new VehicleAssignedToCallEvent(
                TenantId, Id, callId, technicianId));
        }

        public void CompleteAssignment(EmergencyCallId callId)
        {
            var assignment = _assignments.FirstOrDefault(a => 
                a.CallId == callId && a.Status == AssignmentStatus.Active);
            
            if (assignment == null)
                throw new InvalidOperationException("No active assignment found for this call");

            assignment.Complete();
            
            if (!_assignments.Any(a => a.Status == AssignmentStatus.Active))
            {
                Status = VehicleStatus.Available;
                CurrentDriverId = null;
            }

            AddDomainEvent(new VehicleAssignmentCompletedEvent(
                TenantId, Id, callId));
        }

        public void AddEquipment(EquipmentType type, string name, string serialNumber, bool isOperational = true)
        {
            var equipment = new VehicleEquipment(Id, type, name, serialNumber, isOperational);
            _equipment.Add(equipment);

            AddDomainEvent(new VehicleEquipmentAddedEvent(TenantId, Id, equipment.Id));
        }
    }

    public class Technician : AggregateRoot
    {
        public TechnicianId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public UserId UserId { get; private set; } // Reference to core User
        public string EmployeeNumber { get; private set; }
        public TechnicianStatus Status { get; private set; }
        public DateTime HireDate { get; private set; }
        public string? LicenseNumber { get; private set; }
        public DateTime? LicenseExpiryDate { get; private set; }
        public TechnicianLocation? CurrentLocation { get; private set; }
        public ServiceVehicleId? AssignedVehicleId { get; private set; }
        public int CompletedCalls { get; private set; }
        public decimal AverageRating { get; private set; }
        public DateTime? LastActiveAt { get; private set; }
        
        private readonly List<TechnicianSkill> _skills = new();
        public IReadOnlyCollection<TechnicianSkill> Skills => _skills.AsReadOnly();
        
        private readonly List<TechnicianSchedule> _schedules = new();
        public IReadOnlyCollection<TechnicianSchedule> Schedules => _schedules.AsReadOnly();
        
        private readonly List<ServiceAssignment> _assignments = new();
        public IReadOnlyCollection<ServiceAssignment> CurrentAssignments => 
            _assignments.Where(a => a.Status == AssignmentStatus.Active).ToList().AsReadOnly();

        public void UpdateLocation(double latitude, double longitude)
        {
            CurrentLocation = new TechnicianLocation(latitude, longitude, DateTime.UtcNow);
            LastActiveAt = DateTime.UtcNow;

            AddDomainEvent(new TechnicianLocationUpdatedEvent(TenantId, Id, CurrentLocation));
        }

        public void AddSkill(ServiceSkillType skillType, SkillLevel level, DateTime? certificationDate = null)
        {
            var existingSkill = _skills.FirstOrDefault(s => s.SkillType == skillType);
            if (existingSkill != null)
            {
                existingSkill.UpdateLevel(level, certificationDate);
            }
            else
            {
                var skill = new TechnicianSkill(Id, skillType, level, certificationDate);
                _skills.Add(skill);
            }

            AddDomainEvent(new TechnicianSkillUpdatedEvent(TenantId, Id, skillType, level));
        }

        public bool CanHandleCallType(CallType callType)
        {
            return callType switch
            {
                CallType.Towing => _skills.Any(s => s.SkillType == ServiceSkillType.Towing && s.Level >= SkillLevel.Basic),
                CallType.Mechanical => _skills.Any(s => s.SkillType == ServiceSkillType.Mechanical && s.Level >= SkillLevel.Intermediate),
                CallType.Electrical => _skills.Any(s => s.SkillType == ServiceSkillType.Electrical && s.Level >= SkillLevel.Basic),
                CallType.FuelDelivery => _skills.Any(s => s.SkillType == ServiceSkillType.FuelDelivery),
                CallType.Lockout => true, // Basic skill all technicians have
                _ => false
            };
        }

        public void CompleteCall(decimal customerRating)
        {
            CompletedCalls++;
            
            // Update average rating with weighted calculation
            if (AverageRating == 0)
                AverageRating = customerRating;
            else
                AverageRating = (AverageRating * (CompletedCalls - 1) + customerRating) / CompletedCalls;

            AddDomainEvent(new TechnicianCallCompletedEvent(TenantId, Id, customerRating, AverageRating));
        }
    }
}
```

### Value Objects and Domain Services

```csharp
namespace DriveOps.Breakdown.Domain.ValueObjects
{
    public class CallLocation : ValueObject
    {
        public double Latitude { get; }
        public double Longitude { get; }
        public string Address { get; }
        public string? Landmark { get; }
        public bool IsHighwayLocation { get; }
        public bool IsRemoteLocation { get; }
        public bool IsSafeLocation { get; }
        public string? AccessInstructions { get; }

        public CallLocation(
            double latitude, 
            double longitude, 
            string address,
            string? landmark = null,
            bool isHighwayLocation = false,
            bool isRemoteLocation = false,
            bool isSafeLocation = true,
            string? accessInstructions = null)
        {
            if (latitude < -90 || latitude > 90)
                throw new ArgumentException("Latitude must be between -90 and 90", nameof(latitude));
            
            if (longitude < -180 || longitude > 180)
                throw new ArgumentException("Longitude must be between -180 and 180", nameof(longitude));

            Latitude = latitude;
            Longitude = longitude;
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Landmark = landmark;
            IsHighwayLocation = isHighwayLocation;
            IsRemoteLocation = isRemoteLocation;
            IsSafeLocation = isSafeLocation;
            AccessInstructions = accessInstructions;
        }

        public double DistanceTo(CallLocation other)
        {
            return GeoDistanceCalculator.CalculateDistance(
                Latitude, Longitude, other.Latitude, other.Longitude);
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Latitude;
            yield return Longitude;
            yield return Address;
            yield return Landmark ?? string.Empty;
            yield return IsHighwayLocation;
            yield return IsRemoteLocation;
            yield return IsSafeLocation;
            yield return AccessInstructions ?? string.Empty;
        }
    }

    public class ServiceCapabilities : ValueObject
    {
        public bool CanTowLightVehicles { get; }
        public bool CanTowHeavyVehicles { get; }
        public bool CanPerformJumpStart { get; }
        public bool CanDeliverFuel { get; }
        public bool CanUnlockVehicles { get; }
        public bool CanPerformMinorRepairs { get; }
        public bool CanChangeWheels { get; }
        public int MaxTowWeight { get; }
        public string[] SpecialEquipment { get; }

        public ServiceCapabilities(
            bool canTowLightVehicles,
            bool canTowHeavyVehicles,
            bool canPerformJumpStart,
            bool canDeliverFuel,
            bool canUnlockVehicles,
            bool canPerformMinorRepairs,
            bool canChangeWheels,
            int maxTowWeight,
            params string[] specialEquipment)
        {
            CanTowLightVehicles = canTowLightVehicles;
            CanTowHeavyVehicles = canTowHeavyVehicles;
            CanPerformJumpStart = canPerformJumpStart;
            CanDeliverFuel = canDeliverFuel;
            CanUnlockVehicles = canUnlockVehicles;
            CanPerformMinorRepairs = canPerformMinorRepairs;
            CanChangeWheels = canChangeWheels;
            MaxTowWeight = maxTowWeight;
            SpecialEquipment = specialEquipment ?? Array.Empty<string>();
        }

        public bool CanHandleCallType(CallType callType)
        {
            return callType switch
            {
                CallType.Towing => CanTowLightVehicles || CanTowHeavyVehicles,
                CallType.JumpStart => CanPerformJumpStart,
                CallType.FuelDelivery => CanDeliverFuel,
                CallType.Lockout => CanUnlockVehicles,
                CallType.WheelChange => CanChangeWheels,
                CallType.MinorRepair => CanPerformMinorRepairs,
                _ => false
            };
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return CanTowLightVehicles;
            yield return CanTowHeavyVehicles;
            yield return CanPerformJumpStart;
            yield return CanDeliverFuel;
            yield return CanUnlockVehicles;
            yield return CanPerformMinorRepairs;
            yield return CanChangeWheels;
            yield return MaxTowWeight;
            yield return string.Join(",", SpecialEquipment);
        }
    }
}

namespace DriveOps.Breakdown.Domain.Services
{
    public interface IDispatchingService
    {
        Task<DispatchResult> FindOptimalTechnicianAsync(
            EmergencyCall call, 
            IEnumerable<Technician> availableTechnicians,
            IEnumerable<ServiceVehicle> availableVehicles);
        
        Task<IEnumerable<Technician>> GetAvailableTechniciansAsync(
            CallLocation location, 
            CallType callType,
            double maxDistanceKm = 50);
    }

    public class DispatchingService : IDispatchingService
    {
        private readonly ITechnicianRepository _technicianRepository;
        private readonly IServiceVehicleRepository _vehicleRepository;
        private readonly ITrafficService _trafficService;
        private readonly IWeatherService _weatherService;

        public async Task<DispatchResult> FindOptimalTechnicianAsync(
            EmergencyCall call,
            IEnumerable<Technician> availableTechnicians,
            IEnumerable<ServiceVehicle> availableVehicles)
        {
            var scoredOptions = new List<DispatchOption>();

            foreach (var technician in availableTechnicians)
            {
                if (!technician.CanHandleCallType(call.CallType))
                    continue;

                var compatibleVehicles = availableVehicles.Where(v => 
                    v.Capabilities.CanHandleCallType(call.CallType));

                foreach (var vehicle in compatibleVehicles)
                {
                    var score = await CalculateDispatchScore(call, technician, vehicle);
                    scoredOptions.Add(new DispatchOption(technician, vehicle, score));
                }
            }

            var bestOption = scoredOptions.OrderByDescending(o => o.Score).FirstOrDefault();
            
            return bestOption != null 
                ? DispatchResult.Success(bestOption.Technician, bestOption.Vehicle)
                : DispatchResult.NoAvailableResources();
        }

        private async Task<double> CalculateDispatchScore(
            EmergencyCall call, 
            Technician technician, 
            ServiceVehicle vehicle)
        {
            var score = 100.0; // Base score

            // Distance factor (closer is better)
            if (technician.CurrentLocation != null && vehicle.CurrentLocation != null)
            {
                var technicianDistance = call.Location.DistanceTo(
                    new CallLocation(technician.CurrentLocation.Latitude, technician.CurrentLocation.Longitude, ""));
                var vehicleDistance = call.Location.DistanceTo(
                    new CallLocation(vehicle.CurrentLocation.Latitude, vehicle.CurrentLocation.Longitude, ""));
                
                var avgDistance = (technicianDistance + vehicleDistance) / 2;
                score -= Math.Min(avgDistance * 2, 50); // Max 50 point penalty for distance
            }

            // Priority factor
            var priorityMultiplier = call.Priority switch
            {
                CallPriority.Critical => 2.0,
                CallPriority.High => 1.5,
                CallPriority.Medium => 1.0,
                CallPriority.Low => 0.8,
                _ => 1.0
            };

            score *= priorityMultiplier;

            // Technician experience and rating
            score += technician.AverageRating * 10; // 0-50 bonus points
            score += Math.Min(technician.CompletedCalls / 10.0, 20); // Experience bonus up to 20 points

            // Traffic and weather conditions
            if (vehicle.CurrentLocation != null)
            {
                var trafficDelay = await _trafficService.GetEstimatedDelayAsync(
                    vehicle.CurrentLocation, call.Location);
                score -= trafficDelay.TotalMinutes / 2; // Penalty for traffic delays

                var weatherConditions = await _weatherService.GetCurrentConditionsAsync(call.Location);
                if (weatherConditions.IsAdverse)
                    score -= 10; // Weather penalty
            }

            return Math.Max(score, 0);
        }

        public async Task<IEnumerable<Technician>> GetAvailableTechniciansAsync(
            CallLocation location, 
            CallType callType, 
            double maxDistanceKm = 50)
        {
            return await _technicianRepository.FindAvailableInRadiusAsync(
                location.Latitude, location.Longitude, maxDistanceKm, callType);
        }
    }
}
```

---
- Direct billing to insurance providers (Admiral, AXA, Allianz)
- Automated claims processing and documentation
- Customer payment processing for out-of-pocket services
- Comprehensive financial reporting and analytics

---

## Business Domain Model

### Core Emergency Services Entities

The BREAKDOWN module implements a sophisticated domain model centered around emergency response operations:
