# DriveOps - GARDIENNAGE/VTC Commercial Module

## Overview

The GARDIENNAGE/VTC commercial module provides comprehensive management for personal transport and security services. This module encompasses VTC/ride services, security/guard services, and courier/delivery services with multiple subscription tiers designed for service providers of all sizes.

### Module Scope

The GARDIENNAGE/VTC module manages:
- **VTC/Transport Services**: Ride booking, driver management, route optimization
- **Security/Gardiennage**: Guard scheduling, patrol routes, incident reporting  
- **Courier/Delivery**: Package tracking, delivery optimization, proof of delivery
- **Dispatch Center**: Multi-service coordination, real-time tracking, emergency response
- **Driver/Guard Management**: Certification tracking, performance monitoring, payroll
- **Customer Portal**: Service booking, real-time tracking, payment processing

### Business Model Innovation

- **Multi-Service Platform**: One unified system handling transport, security, and delivery
- **Scalable Pricing Tiers**: 
  - VTC Management (39€/month)
  - Security/Gardiennage (49€/month) 
  - Dispatch Center (69€/month)
- **B2B2C Model**: Service providers manage their customers through the platform
- **Real-Time Operations**: Live tracking, instant communication, emergency alerts

---

## 1. Business Domain Model

### 1.1 Core Domain Entities

#### Service Provider Aggregate
```csharp
namespace DriveOps.GardiennageVTC.Domain.Entities
{
    public class ServiceProvider : AggregateRoot
    {
        public ServiceProviderId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string CompanyName { get; private set; }
        public string LicenseNumber { get; private set; }
        public ServiceProviderType Type { get; private set; }
        public ContactInfo ContactInfo { get; private set; }
        public Address HeadOfficeAddress { get; private set; }
        public ServiceProviderStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }
        
        private readonly List<ServiceOffering> _serviceOfferings = new();
        public IReadOnlyCollection<ServiceOffering> ServiceOfferings => _serviceOfferings.AsReadOnly();
        
        private readonly List<Personnel> _personnel = new();
        public IReadOnlyCollection<Personnel> Personnel => _personnel.AsReadOnly();
        
        private readonly List<Vehicle> _vehicles = new();
        public IReadOnlyCollection<Vehicle> Vehicles => _vehicles.AsReadOnly();

        public void AddServiceOffering(ServiceType serviceType, ServiceTier tier, decimal basePrice)
        {
            if (_serviceOfferings.Any(s => s.ServiceType == serviceType && s.Tier == tier))
                throw new DomainException($"Service offering {serviceType} with tier {tier} already exists");

            var offering = new ServiceOffering(Id, serviceType, tier, basePrice);
            _serviceOfferings.Add(offering);
            
            RaiseDomainEvent(new ServiceOfferingAddedEvent(TenantId.Value, Id.Value, serviceType, tier));
        }

        public void AssignPersonnel(PersonnelId personnelId, VehicleId? vehicleId = null)
        {
            var personnel = _personnel.FirstOrDefault(p => p.Id == personnelId);
            if (personnel == null)
                throw new DomainException($"Personnel {personnelId} not found");

            if (vehicleId.HasValue)
            {
                var vehicle = _vehicles.FirstOrDefault(v => v.Id == vehicleId);
                if (vehicle == null)
                    throw new DomainException($"Vehicle {vehicleId} not found");
                
                personnel.AssignVehicle(vehicleId.Value);
            }

            RaiseDomainEvent(new PersonnelAssignedEvent(TenantId.Value, Id.Value, personnelId.Value, vehicleId?.Value));
        }
    }

    public enum ServiceProviderType
    {
        VTCCompany = 1,
        SecurityCompany = 2,
        CourierCompany = 3,
        MultiService = 4
    }

    public enum ServiceProviderStatus
    {
        Pending = 0,
        Active = 1,
        Suspended = 2,
        Cancelled = 3
    }
}
```

#### Personnel Management Aggregate
```csharp
namespace DriveOps.GardiennageVTC.Domain.Entities
{
    public class Personnel : AggregateRoot
    {
        public PersonnelId Id { get; private set; }
        public ServiceProviderId ServiceProviderId { get; private set; }
        public TenantId TenantId { get; private set; }
        public PersonnelType Type { get; private set; }
        public PersonalInfo PersonalInfo { get; private set; }
        public EmergencyContact EmergencyContact { get; private set; }
        public PersonnelStatus Status { get; private set; }
        public VehicleId? AssignedVehicleId { get; private set; }
        public DateTime HiredDate { get; private set; }
        public DateTime? TerminatedDate { get; private set; }
        
        private readonly List<Certification> _certifications = new();
        public IReadOnlyCollection<Certification> Certifications => _certifications.AsReadOnly();
        
        private readonly List<PerformanceMetric> _performanceMetrics = new();
        public IReadOnlyCollection<PerformanceMetric> PerformanceMetrics => _performanceMetrics.AsReadOnly();

        private readonly List<WorkShift> _workShifts = new();
        public IReadOnlyCollection<WorkShift> WorkShifts => _workShifts.AsReadOnly();

        public void AddCertification(CertificationType type, string certificationNumber, 
            DateTime issuedDate, DateTime expiryDate, string issuingAuthority)
        {
            var certification = new Certification(Id, type, certificationNumber, issuedDate, expiryDate, issuingAuthority);
            _certifications.Add(certification);
            
            RaiseDomainEvent(new CertificationAddedEvent(TenantId.Value, Id.Value, type, expiryDate));
        }

        public void AssignVehicle(VehicleId vehicleId)
        {
            AssignedVehicleId = vehicleId;
            RaiseDomainEvent(new PersonnelVehicleAssignedEvent(TenantId.Value, Id.Value, vehicleId.Value));
        }

        public void ScheduleShift(DateTime startTime, DateTime endTime, ShiftType shiftType, 
            Location? assignedLocation = null)
        {
            var shift = new WorkShift(Id, startTime, endTime, shiftType, assignedLocation);
            _workShifts.Add(shift);
            
            RaiseDomainEvent(new ShiftScheduledEvent(TenantId.Value, Id.Value, startTime, endTime, shiftType));
        }

        public void RecordPerformanceMetric(PerformanceMetricType metricType, decimal value, 
            DateTime recordedAt, string? notes = null)
        {
            var metric = new PerformanceMetric(Id, metricType, value, recordedAt, notes);
            _performanceMetrics.Add(metric);
        }
    }

    public enum PersonnelType
    {
        VTCDriver = 1,
        SecurityGuard = 2,
        Courier = 3,
        Dispatcher = 4,
        Supervisor = 5
    }

    public enum PersonnelStatus
    {
        Active = 1,
        OnBreak = 2,
        OffDuty = 3,
        Suspended = 4,
        Terminated = 5
    }
}
```

#### Service Booking Aggregate
```csharp
namespace DriveOps.GardiennageVTC.Domain.Entities
{
    public class ServiceBooking : AggregateRoot
    {
        public ServiceBookingId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public ServiceProviderId ServiceProviderId { get; private set; }
        public CustomerId CustomerId { get; private set; }
        public ServiceType ServiceType { get; private set; }
        public BookingStatus Status { get; private set; }
        public DateTime BookingDate { get; private set; }
        public DateTime ServiceDate { get; private set; }
        public Location PickupLocation { get; private set; }
        public Location? DestinationLocation { get; private set; }
        public PersonnelId? AssignedPersonnelId { get; private set; }
        public VehicleId? AssignedVehicleId { get; private set; }
        public PricingInfo PricingInfo { get; private set; }
        public PaymentStatus PaymentStatus { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        
        private readonly List<BookingStatusHistory> _statusHistory = new();
        public IReadOnlyCollection<BookingStatusHistory> StatusHistory => _statusHistory.AsReadOnly();
        
        private readonly List<TrackingUpdate> _trackingUpdates = new();
        public IReadOnlyCollection<TrackingUpdate> TrackingUpdates => _trackingUpdates.AsReadOnly();

        public void AssignPersonnel(PersonnelId personnelId, VehicleId? vehicleId = null)
        {
            if (Status != BookingStatus.Confirmed)
                throw new DomainException("Can only assign personnel to confirmed bookings");

            AssignedPersonnelId = personnelId;
            AssignedVehicleId = vehicleId;
            UpdateStatus(BookingStatus.Assigned);
            
            RaiseDomainEvent(new BookingAssignedEvent(TenantId.Value, Id.Value, personnelId.Value, vehicleId?.Value));
        }

        public void StartService(Location currentLocation)
        {
            if (Status != BookingStatus.Assigned)
                throw new DomainException("Can only start assigned bookings");

            UpdateStatus(BookingStatus.InProgress);
            AddTrackingUpdate(currentLocation, TrackingEventType.ServiceStarted);
            
            RaiseDomainEvent(new ServiceStartedEvent(TenantId.Value, Id.Value, AssignedPersonnelId!.Value, currentLocation));
        }

        public void CompleteService(Location finalLocation, string? notes = null)
        {
            if (Status != BookingStatus.InProgress)
                throw new DomainException("Can only complete in-progress bookings");

            CompletedAt = DateTime.UtcNow;
            UpdateStatus(BookingStatus.Completed);
            AddTrackingUpdate(finalLocation, TrackingEventType.ServiceCompleted, notes);
            
            RaiseDomainEvent(new ServiceCompletedEvent(TenantId.Value, Id.Value, AssignedPersonnelId!.Value, finalLocation));
        }

        public void AddTrackingUpdate(Location location, TrackingEventType eventType, string? notes = null)
        {
            var update = new TrackingUpdate(Id, location, eventType, DateTime.UtcNow, notes);
            _trackingUpdates.Add(update);
            
            RaiseDomainEvent(new TrackingUpdateEvent(TenantId.Value, Id.Value, location, eventType));
        }

        private void UpdateStatus(BookingStatus newStatus)
        {
            var previousStatus = Status;
            Status = newStatus;
            
            var statusHistory = new BookingStatusHistory(Id, previousStatus, newStatus, DateTime.UtcNow);
            _statusHistory.Add(statusHistory);
        }
    }

    public enum ServiceType
    {
        VTCRide = 1,
        SecurityPatrol = 2,
        CourierDelivery = 3,
        EmergencyResponse = 4
    }

    public enum BookingStatus
    {
        Pending = 0,
        Confirmed = 1,
        Assigned = 2,
        InProgress = 3,
        Completed = 4,
        Cancelled = 5
    }

    public enum TrackingEventType
    {
        BookingCreated = 1,
        PersonnelAssigned = 2,
        PersonnelEnRoute = 3,
        ArrivedAtPickup = 4,
        ServiceStarted = 5,
        ServiceInProgress = 6,
        ServiceCompleted = 7,
        EmergencyAlert = 8
    }
}
```

#### Customer Management Aggregate
```csharp
namespace DriveOps.GardiennageVTC.Domain.Entities
{
    public class Customer : AggregateRoot
    {
        public CustomerId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public CustomerType Type { get; private set; }
        public PersonalInfo PersonalInfo { get; private set; }
        public ContactInfo ContactInfo { get; private set; }
        public CustomerStatus Status { get; private set; }
        public DateTime RegisteredAt { get; private set; }
        public CustomerPreferences Preferences { get; private set; }
        
        private readonly List<PaymentMethod> _paymentMethods = new();
        public IReadOnlyCollection<PaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();
        
        private readonly List<CustomerAddress> _addresses = new();
        public IReadOnlyCollection<CustomerAddress> Addresses => _addresses.AsReadOnly();
        
        private readonly List<CustomerRating> _ratings = new();
        public IReadOnlyCollection<CustomerRating> Ratings => _ratings.AsReadOnly();

        public void AddPaymentMethod(PaymentMethodType type, string token, bool isDefault = false)
        {
            if (isDefault)
            {
                foreach (var method in _paymentMethods)
                    method.SetAsNonDefault();
            }

            var paymentMethod = new PaymentMethod(Id, type, token, isDefault);
            _paymentMethods.Add(paymentMethod);
            
            RaiseDomainEvent(new PaymentMethodAddedEvent(TenantId.Value, Id.Value, type, isDefault));
        }

        public void AddAddress(AddressType type, Address address, bool isDefault = false)
        {
            if (isDefault)
            {
                foreach (var addr in _addresses.Where(a => a.Type == type))
                    addr.SetAsNonDefault();
            }

            var customerAddress = new CustomerAddress(Id, type, address, isDefault);
            _addresses.Add(customerAddress);
        }

        public void AddRating(ServiceBookingId bookingId, int rating, string? comment = null)
        {
            if (rating < 1 || rating > 5)
                throw new DomainException("Rating must be between 1 and 5");

            var customerRating = new CustomerRating(Id, bookingId, rating, comment, DateTime.UtcNow);
            _ratings.Add(customerRating);
            
            RaiseDomainEvent(new CustomerRatingAddedEvent(TenantId.Value, Id.Value, bookingId.Value, rating));
        }
    }

    public enum CustomerType
    {
        Individual = 1,
        Corporate = 2,
        Government = 3
    }

    public enum CustomerStatus
    {
        Active = 1,
        Suspended = 2,
        Blocked = 3
    }
}
```

#### Dispatch Center Aggregate
```csharp
namespace DriveOps.GardiennageVTC.Domain.Entities
{
    public class DispatchCenter : AggregateRoot
    {
        public DispatchCenterId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public ServiceProviderId ServiceProviderId { get; private set; }
        public string Name { get; private set; }
        public Location Location { get; private set; }
        public DispatchCenterStatus Status { get; private set; }
        public ServiceArea CoverageArea { get; private set; }
        public EmergencyProtocols EmergencyProtocols { get; private set; }
        
        private readonly List<DispatcherAssignment> _dispatchers = new();
        public IReadOnlyCollection<DispatcherAssignment> Dispatchers => _dispatchers.AsReadOnly();
        
        private readonly List<EmergencyAlert> _emergencyAlerts = new();
        public IReadOnlyCollection<EmergencyAlert> EmergencyAlerts => _emergencyAlerts.AsReadOnly();
        
        private readonly List<ResourceAllocation> _resourceAllocations = new();
        public IReadOnlyCollection<ResourceAllocation> ResourceAllocations => _resourceAllocations.AsReadOnly();

        public void AssignDispatcher(PersonnelId dispatcherId, ShiftSchedule schedule)
        {
            var assignment = new DispatcherAssignment(Id, dispatcherId, schedule);
            _dispatchers.Add(assignment);
            
            RaiseDomainEvent(new DispatcherAssignedEvent(TenantId.Value, Id.Value, dispatcherId.Value, schedule));
        }

        public void HandleEmergencyAlert(EmergencyAlertType alertType, Location location, 
            string description, PersonnelId? reportedBy = null)
        {
            var alert = new EmergencyAlert(Id, alertType, location, description, reportedBy);
            _emergencyAlerts.Add(alert);
            
            // Auto-assign nearest available resources based on alert type
            var nearestResources = FindNearestAvailableResources(location, alertType);
            foreach (var resource in nearestResources)
            {
                AllocateResource(resource, alert.Id, ResourceAllocationPriority.Emergency);
            }
            
            RaiseDomainEvent(new EmergencyAlertRaisedEvent(TenantId.Value, Id.Value, alert.Id.Value, alertType, location));
        }

        public void AllocateResource(ResourceId resourceId, EmergencyAlertId alertId, 
            ResourceAllocationPriority priority)
        {
            var allocation = new ResourceAllocation(Id, resourceId, alertId, priority, DateTime.UtcNow);
            _resourceAllocations.Add(allocation);
            
            RaiseDomainEvent(new ResourceAllocatedEvent(TenantId.Value, Id.Value, resourceId.Value, alertId.Value, priority));
        }

        private List<ResourceId> FindNearestAvailableResources(Location location, EmergencyAlertType alertType)
        {
            // Implementation would query available personnel/vehicles within radius
            // and filter by capability to handle the alert type
            return new List<ResourceId>();
        }
    }

    public enum DispatchCenterStatus
    {
        Active = 1,
        Maintenance = 2,
        Emergency = 3,
        Offline = 4
    }

    public enum EmergencyAlertType
    {
        PanicButton = 1,
        VehicleBreakdown = 2,
        Accident = 3,
        SecurityIncident = 4,
        MedicalEmergency = 5,
        DeliveryIncident = 6
    }

    public enum ResourceAllocationPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Emergency = 4,
        Critical = 5
    }
}
```

### 1.2 Value Objects

```csharp
namespace DriveOps.GardiennageVTC.Domain.ValueObjects
{
    public record ServiceProviderId(Guid Value)
    {
        public static ServiceProviderId New() => new(Guid.NewGuid());
        public static ServiceProviderId From(Guid value) => new(value);
    }

    public record PersonnelId(Guid Value)
    {
        public static PersonnelId New() => new(Guid.NewGuid());
        public static PersonnelId From(Guid value) => new(value);
    }

    public record ServiceBookingId(Guid Value)
    {
        public static ServiceBookingId New() => new(Guid.NewGuid());
        public static ServiceBookingId From(Guid value) => new(value);
    }

    public record CustomerId(Guid Value)
    {
        public static CustomerId New() => new(Guid.NewGuid());
        public static CustomerId From(Guid value) => new(value);
    }

    public record DispatchCenterId(Guid Value)
    {
        public static DispatchCenterId New() => new(Guid.NewGuid());
        public static DispatchCenterId From(Guid value) => new(value);
    }

    public record Location(
        decimal Latitude,
        decimal Longitude,
        string? Address = null,
        string? City = null,
        string? PostalCode = null
    )
    {
        public double DistanceToKm(Location other)
        {
            // Haversine formula implementation
            var R = 6371; // Earth's radius in kilometers
            var dLat = ToRadians((double)(other.Latitude - Latitude));
            var dLon = ToRadians((double)(other.Longitude - Longitude));
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians((double)Latitude)) * Math.Cos(ToRadians((double)other.Latitude)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    }

    public record PricingInfo(
        decimal BasePrice,
        decimal DistancePrice,
        decimal TimePrice,
        decimal SurchargePrice,
        decimal TotalPrice,
        string Currency = "EUR"
    );

    public record PersonalInfo(
        string FirstName,
        string LastName,
        DateTime DateOfBirth,
        string? NationalId = null,
        string? PassportNumber = null
    );

    public record ContactInfo(
        string Email,
        string Phone,
        string? AlternatePhone = null
    );

    public record EmergencyContact(
        string Name,
        string Phone,
        string Relationship
    );

    public record ServiceArea(
        string Name,
        List<Location> BoundaryPoints,
        decimal RadiusKm
    );

    public record ShiftSchedule(
        DateTime StartTime,
        DateTime EndTime,
        List<DayOfWeek> DaysOfWeek,
        ShiftType Type
    );

    public enum ShiftType
    {
        Regular = 1,
        Night = 2,
        Weekend = 3,
        Holiday = 4,
        Emergency = 5
    }
}
```

---

## 2. Complete C# Architecture

### 2.1 Domain Layer Implementation

#### Domain Events
```csharp
namespace DriveOps.GardiennageVTC.Domain.Events
{
    // Service Provider Events
    public record ServiceProviderCreatedEvent(
        string TenantId,
        Guid ServiceProviderId,
        ServiceProviderType Type,
        string CompanyName
    ) : IntegrationEvent;

    public record ServiceOfferingAddedEvent(
        string TenantId,
        Guid ServiceProviderId,
        ServiceType ServiceType,
        ServiceTier Tier
    ) : IntegrationEvent;

    // Personnel Events
    public record PersonnelHiredEvent(
        string TenantId,
        Guid ServiceProviderId,
        Guid PersonnelId,
        PersonnelType Type,
        DateTime HiredDate
    ) : IntegrationEvent;

    public record CertificationAddedEvent(
        string TenantId,
        Guid PersonnelId,
        CertificationType Type,
        DateTime ExpiryDate
    ) : IntegrationEvent;

    public record CertificationExpiringEvent(
        string TenantId,
        Guid PersonnelId,
        CertificationType Type,
        DateTime ExpiryDate,
        int DaysUntilExpiry
    ) : IntegrationEvent;

    // Booking Events
    public record BookingCreatedEvent(
        string TenantId,
        Guid BookingId,
        Guid CustomerId,
        ServiceType ServiceType,
        DateTime ServiceDate,
        Location PickupLocation
    ) : IntegrationEvent;

    public record BookingAssignedEvent(
        string TenantId,
        Guid BookingId,
        Guid PersonnelId,
        Guid? VehicleId
    ) : IntegrationEvent;

    public record ServiceStartedEvent(
        string TenantId,
        Guid BookingId,
        Guid PersonnelId,
        Location StartLocation
    ) : IntegrationEvent;

    public record ServiceCompletedEvent(
        string TenantId,
        Guid BookingId,
        Guid PersonnelId,
        Location EndLocation
    ) : IntegrationEvent;

    public record TrackingUpdateEvent(
        string TenantId,
        Guid BookingId,
        Location CurrentLocation,
        TrackingEventType EventType
    ) : IntegrationEvent;

    // Emergency Events
    public record EmergencyAlertRaisedEvent(
        string TenantId,
        Guid DispatchCenterId,
        Guid AlertId,
        EmergencyAlertType AlertType,
        Location Location
    ) : IntegrationEvent;

    public record PanicButtonActivatedEvent(
        string TenantId,
        Guid PersonnelId,
        Guid? BookingId,
        Location Location,
        DateTime Timestamp
    ) : IntegrationEvent;

    public record ResourceAllocatedEvent(
        string TenantId,
        Guid DispatchCenterId,
        Guid ResourceId,
        Guid AlertId,
        ResourceAllocationPriority Priority
    ) : IntegrationEvent;
}
```

#### Domain Services
```csharp
namespace DriveOps.GardiennageVTC.Domain.Services
{
    public interface IPricingService
    {
        Task<PricingInfo> CalculatePriceAsync(ServiceType serviceType, Location pickup, 
            Location? destination, DateTime serviceDate, ServiceTier tier);
        Task<decimal> CalculateSurchargeAsync(ServiceType serviceType, DateTime serviceDate, 
            Location location);
    }

    public class PricingService : IPricingService
    {
        private readonly IPricingRuleRepository _pricingRuleRepository;
        private readonly IDistanceCalculator _distanceCalculator;
        private readonly ITimeEstimator _timeEstimator;

        public async Task<PricingInfo> CalculatePriceAsync(ServiceType serviceType, Location pickup, 
            Location? destination, DateTime serviceDate, ServiceTier tier)
        {
            var basePrice = await GetBasePriceAsync(serviceType, tier);
            var distancePrice = 0m;
            var timePrice = 0m;

            if (destination != null)
            {
                var distance = pickup.DistanceToKm(destination);
                var estimatedTime = await _timeEstimator.EstimateTimeAsync(pickup, destination, serviceDate);
                
                distancePrice = (decimal)distance * await GetDistanceRateAsync(serviceType, tier);
                timePrice = estimatedTime * await GetTimeRateAsync(serviceType, tier);
            }

            var surchargePrice = await CalculateSurchargeAsync(serviceType, serviceDate, pickup);
            var totalPrice = basePrice + distancePrice + timePrice + surchargePrice;

            return new PricingInfo(basePrice, distancePrice, timePrice, surchargePrice, totalPrice);
        }

        public async Task<decimal> CalculateSurchargeAsync(ServiceType serviceType, DateTime serviceDate, 
            Location location)
        {
            var surcharges = await _pricingRuleRepository.GetSurchargeRulesAsync(serviceType);
            var totalSurcharge = 0m;

            foreach (var rule in surcharges)
            {
                if (rule.IsApplicable(serviceDate, location))
                {
                    totalSurcharge += rule.Amount;
                }
            }

            return totalSurcharge;
        }

        private async Task<decimal> GetBasePriceAsync(ServiceType serviceType, ServiceTier tier) =>
            await _pricingRuleRepository.GetBasePriceAsync(serviceType, tier);

        private async Task<decimal> GetDistanceRateAsync(ServiceType serviceType, ServiceTier tier) =>
            await _pricingRuleRepository.GetDistanceRateAsync(serviceType, tier);

        private async Task<decimal> GetTimeRateAsync(ServiceType serviceType, ServiceTier tier) =>
            await _pricingRuleRepository.GetTimeRateAsync(serviceType, tier);
    }

    public interface IDispatchService
    {
        Task<PersonnelId?> FindBestAvailablePersonnelAsync(ServiceBooking booking);
        Task<List<PersonnelId>> FindNearestPersonnelAsync(Location location, ServiceType serviceType, 
            int maxResults = 5);
        Task OptimizeRouteAsync(PersonnelId personnelId, List<ServiceBookingId> bookings);
    }

    public class DispatchService : IDispatchService
    {
        private readonly IPersonnelRepository _personnelRepository;
        private readonly IServiceBookingRepository _bookingRepository;
        private readonly IRouteOptimizationService _routeOptimizer;
        private readonly IDistanceCalculator _distanceCalculator;

        public async Task<PersonnelId?> FindBestAvailablePersonnelAsync(ServiceBooking booking)
        {
            var availablePersonnel = await _personnelRepository.GetAvailablePersonnelAsync(
                booking.ServiceType, booking.ServiceDate);

            if (!availablePersonnel.Any())
                return null;

            // Score personnel based on distance, rating, and specialization
            var scoredPersonnel = new List<(PersonnelId Id, double Score)>();

            foreach (var personnel in availablePersonnel)
            {
                var score = await CalculatePersonnelScoreAsync(personnel, booking);
                scoredPersonnel.Add((personnel.Id, score));
            }

            return scoredPersonnel.OrderByDescending(p => p.Score).First().Id;
        }

        public async Task<List<PersonnelId>> FindNearestPersonnelAsync(Location location, 
            ServiceType serviceType, int maxResults = 5)
        {
            var availablePersonnel = await _personnelRepository.GetAvailablePersonnelByTypeAsync(serviceType);
            
            var personnelWithDistance = availablePersonnel
                .Select(p => new
                {
                    PersonnelId = p.Id,
                    Distance = location.DistanceToKm(p.CurrentLocation)
                })
                .OrderBy(p => p.Distance)
                .Take(maxResults)
                .Select(p => p.PersonnelId)
                .ToList();

            return personnelWithDistance;
        }

        public async Task OptimizeRouteAsync(PersonnelId personnelId, List<ServiceBookingId> bookings)
        {
            var bookingEntities = await _bookingRepository.GetByIdsAsync(bookings);
            var waypoints = bookingEntities.Select(b => b.PickupLocation).ToList();

            var optimizedRoute = await _routeOptimizer.OptimizeAsync(waypoints);
            
            // Update booking sequence based on optimized route
            for (int i = 0; i < optimizedRoute.Count; i++)
            {
                var booking = bookingEntities[optimizedRoute[i]];
                booking.SetSequenceOrder(i + 1);
            }
        }

        private async Task<double> CalculatePersonnelScoreAsync(Personnel personnel, ServiceBooking booking)
        {
            var distanceScore = CalculateDistanceScore(personnel.CurrentLocation, booking.PickupLocation);
            var ratingScore = personnel.AverageRating * 20; // Scale to 0-100
            var specializationScore = CalculateSpecializationScore(personnel, booking.ServiceType);
            
            return (distanceScore + ratingScore + specializationScore) / 3;
        }

        private double CalculateDistanceScore(Location personnelLocation, Location bookingLocation)
        {
            var distance = personnelLocation.DistanceToKm(bookingLocation);
            return Math.Max(0, 100 - distance * 2); // Closer is better, max 50km range
        }

        private double CalculateSpecializationScore(Personnel personnel, ServiceType serviceType)
        {
            // Implementation based on personnel certifications and experience
            return personnel.HasSpecialization(serviceType) ? 100 : 50;
        }
    }
}
```

### 2.2 Application Layer Implementation

#### CQRS Commands and Queries
```csharp
namespace DriveOps.GardiennageVTC.Application.Commands
{
    // Service Provider Commands
    public record CreateServiceProviderCommand(
        string TenantId,
        string UserId,
        string CompanyName,
        string LicenseNumber,
        ServiceProviderType Type,
        ContactInfo ContactInfo,
        Address HeadOfficeAddress
    ) : ICommand<ServiceProviderId>;

    public record AddServiceOfferingCommand(
        string TenantId,
        string UserId,
        Guid ServiceProviderId,
        ServiceType ServiceType,
        ServiceTier Tier,
        decimal BasePrice
    ) : ICommand;

    // Personnel Commands
    public record HirePersonnelCommand(
        string TenantId,
        string UserId,
        Guid ServiceProviderId,
        PersonnelType Type,
        PersonalInfo PersonalInfo,
        ContactInfo ContactInfo,
        EmergencyContact EmergencyContact
    ) : ICommand<PersonnelId>;

    public record AssignPersonnelCommand(
        string TenantId,
        string UserId,
        Guid PersonnelId,
        Guid? VehicleId,
        Location? AssignedLocation
    ) : ICommand;

    public record AddCertificationCommand(
        string TenantId,
        string UserId,
        Guid PersonnelId,
        CertificationType Type,
        string CertificationNumber,
        DateTime IssuedDate,
        DateTime ExpiryDate,
        string IssuingAuthority
    ) : ICommand;

    // Booking Commands
    public record CreateBookingCommand(
        string TenantId,
        string UserId,
        Guid CustomerId,
        ServiceType ServiceType,
        DateTime ServiceDate,
        Location PickupLocation,
        Location? DestinationLocation,
        ServiceTier Tier,
        string? SpecialInstructions
    ) : ICommand<ServiceBookingId>;

    public record AssignBookingCommand(
        string TenantId,
        string UserId,
        Guid BookingId,
        Guid PersonnelId,
        Guid? VehicleId
    ) : ICommand;

    public record StartServiceCommand(
        string TenantId,
        string UserId,
        Guid BookingId,
        Location CurrentLocation
    ) : ICommand;

    public record CompleteServiceCommand(
        string TenantId,
        string UserId,
        Guid BookingId,
        Location FinalLocation,
        string? Notes
    ) : ICommand;

    public record UpdateTrackingCommand(
        string TenantId,
        string UserId,
        Guid BookingId,
        Location CurrentLocation,
        TrackingEventType EventType,
        string? Notes
    ) : ICommand;

    // Emergency Commands
    public record ActivatePanicButtonCommand(
        string TenantId,
        string UserId,
        Guid PersonnelId,
        Guid? BookingId,
        Location Location
    ) : ICommand;

    public record RaiseEmergencyAlertCommand(
        string TenantId,
        string UserId,
        Guid DispatchCenterId,
        EmergencyAlertType AlertType,
        Location Location,
        string Description,
        Guid? ReportedBy
    ) : ICommand<EmergencyAlertId>;
}

namespace DriveOps.GardiennageVTC.Application.Queries
{
    // Service Provider Queries
    public record GetServiceProviderQuery(
        string TenantId,
        string UserId,
        Guid ServiceProviderId
    ) : IQuery<ServiceProviderDto>;

    public record GetServiceProvidersQuery(
        string TenantId,
        string UserId,
        ServiceProviderType? Type = null,
        ServiceProviderStatus? Status = null,
        int Page = 1,
        int PageSize = 20
    ) : IQuery<PagedResult<ServiceProviderDto>>;

    // Personnel Queries
    public record GetPersonnelQuery(
        string TenantId,
        string UserId,
        Guid PersonnelId
    ) : IQuery<PersonnelDto>;

    public record GetAvailablePersonnelQuery(
        string TenantId,
        string UserId,
        ServiceType ServiceType,
        DateTime ServiceDate,
        Location? NearLocation = null,
        int? MaxDistance = null
    ) : IQuery<List<PersonnelDto>>;

    public record GetPersonnelPerformanceQuery(
        string TenantId,
        string UserId,
        Guid PersonnelId,
        DateTime FromDate,
        DateTime ToDate
    ) : IQuery<PersonnelPerformanceDto>;

    // Booking Queries
    public record GetBookingQuery(
        string TenantId,
        string UserId,
        Guid BookingId
    ) : IQuery<ServiceBookingDto>;

    public record GetBookingsQuery(
        string TenantId,
        string UserId,
        BookingStatus? Status = null,
        ServiceType? ServiceType = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        Guid? CustomerId = null,
        Guid? PersonnelId = null,
        int Page = 1,
        int PageSize = 20
    ) : IQuery<PagedResult<ServiceBookingDto>>;

    public record GetActiveTrackingQuery(
        string TenantId,
        string UserId,
        Guid BookingId
    ) : IQuery<BookingTrackingDto>;

    public record GetDispatchDashboardQuery(
        string TenantId,
        string UserId,
        Guid DispatchCenterId
    ) : IQuery<DispatchDashboardDto>;

    // Analytics Queries
    public record GetServiceMetricsQuery(
        string TenantId,
        string UserId,
        DateTime FromDate,
        DateTime ToDate,
        ServiceType? ServiceType = null,
        Guid? ServiceProviderId = null
    ) : IQuery<ServiceMetricsDto>;

    public record GetRevenueSummaryQuery(
        string TenantId,
        string UserId,
        DateTime FromDate,
        DateTime ToDate,
        ServiceType? ServiceType = null
    ) : IQuery<RevenueSummaryDto>;
}
```

#### Command Handlers
```csharp
namespace DriveOps.GardiennageVTC.Application.Handlers
{
    public class CreateServiceProviderHandler : BaseCommandHandler<CreateServiceProviderCommand, ServiceProviderId>
    {
        private readonly IServiceProviderRepository _serviceProviderRepository;
        private readonly IPricingService _pricingService;

        public CreateServiceProviderHandler(
            IServiceProviderRepository serviceProviderRepository,
            IPricingService pricingService,
            ITenantContext tenantContext,
            ILogger<CreateServiceProviderHandler> logger,
            IUnitOfWork unitOfWork) 
            : base(tenantContext, logger, unitOfWork)
        {
            _serviceProviderRepository = serviceProviderRepository;
            _pricingService = pricingService;
        }

        protected override async Task<Result<ServiceProviderId>> HandleCommandAsync(
            CreateServiceProviderCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Validate license number uniqueness
                var existingProvider = await _serviceProviderRepository
                    .GetByLicenseNumberAsync(request.LicenseNumber);
                
                if (existingProvider != null)
                    return Result<ServiceProviderId>.Failure("License number already exists");

                var serviceProvider = new ServiceProvider(
                    TenantId.From(Guid.Parse(request.TenantId)),
                    request.CompanyName,
                    request.LicenseNumber,
                    request.Type,
                    request.ContactInfo,
                    request.HeadOfficeAddress
                );

                await _serviceProviderRepository.AddAsync(serviceProvider);

                Logger.LogInformation("Service provider created: {ProviderId} for tenant {TenantId}", 
                    serviceProvider.Id, request.TenantId);

                return Result<ServiceProviderId>.Success(serviceProvider.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error creating service provider for tenant {TenantId}", request.TenantId);
                return Result<ServiceProviderId>.Failure($"Failed to create service provider: {ex.Message}");
            }
        }
    }

    public class CreateBookingHandler : BaseCommandHandler<CreateBookingCommand, ServiceBookingId>
    {
        private readonly IServiceBookingRepository _bookingRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IPricingService _pricingService;
        private readonly IDispatchService _dispatchService;
        private readonly INotificationService _notificationService;

        protected override async Task<Result<ServiceBookingId>> HandleCommandAsync(
            CreateBookingCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // Validate customer exists
                var customer = await _customerRepository.GetByIdAsync(CustomerId.From(request.CustomerId));
                if (customer == null)
                    return Result<ServiceBookingId>.Failure("Customer not found");

                // Calculate pricing
                var pricingInfo = await _pricingService.CalculatePriceAsync(
                    request.ServiceType,
                    request.PickupLocation,
                    request.DestinationLocation,
                    request.ServiceDate,
                    request.Tier
                );

                var booking = new ServiceBooking(
                    TenantId.From(Guid.Parse(request.TenantId)),
                    CustomerId.From(request.CustomerId),
                    request.ServiceType,
                    request.ServiceDate,
                    request.PickupLocation,
                    request.DestinationLocation,
                    pricingInfo,
                    request.SpecialInstructions
                );

                await _bookingRepository.AddAsync(booking);

                // Try to auto-assign personnel
                var assignedPersonnel = await _dispatchService.FindBestAvailablePersonnelAsync(booking);
                if (assignedPersonnel != null)
                {
                    booking.AssignPersonnel(assignedPersonnel);
                }

                // Send confirmation notification
                await _notificationService.SendBookingConfirmationAsync(booking, customer);

                Logger.LogInformation("Booking created: {BookingId} for customer {CustomerId}", 
                    booking.Id, request.CustomerId);

                return Result<ServiceBookingId>.Success(booking.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error creating booking for customer {CustomerId}", request.CustomerId);
                return Result<ServiceBookingId>.Failure($"Failed to create booking: {ex.Message}");
            }
        }
    }

    public class ActivatePanicButtonHandler : BaseCommandHandler<ActivatePanicButtonCommand>
    {
        private readonly IPersonnelRepository _personnelRepository;
        private readonly IEmergencyService _emergencyService;
        private readonly INotificationService _notificationService;
        private readonly IDispatchService _dispatchService;

        protected override async Task<Result> HandleCommandAsync(
            ActivatePanicButtonCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var personnel = await _personnelRepository.GetByIdAsync(PersonnelId.From(request.PersonnelId));
                if (personnel == null)
                    return Result.Failure("Personnel not found");

                // Create emergency alert
                var alert = await _emergencyService.CreatePanicAlertAsync(
                    personnel,
                    request.BookingId.HasValue ? ServiceBookingId.From(request.BookingId.Value) : null,
                    request.Location
                );

                // Notify dispatch center immediately
                await _notificationService.SendEmergencyAlertAsync(alert);

                // Allocate emergency resources
                await _dispatchService.AllocateEmergencyResourcesAsync(alert);

                // Notify emergency contacts
                await _notificationService.NotifyEmergencyContactsAsync(personnel, alert);

                Logger.LogCritical("Panic button activated by personnel {PersonnelId} at location {Location}", 
                    request.PersonnelId, request.Location);

                return Result.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling panic button activation for personnel {PersonnelId}", 
                    request.PersonnelId);
                return Result.Failure($"Failed to handle panic button: {ex.Message}");
            }
        }
    }
}
```

#### Query Handlers
```csharp
namespace DriveOps.GardiennageVTC.Application.Handlers
{
    public class GetDispatchDashboardHandler : BaseQueryHandler<GetDispatchDashboardQuery, DispatchDashboardDto>
    {
        private readonly IServiceBookingRepository _bookingRepository;
        private readonly IPersonnelRepository _personnelRepository;
        private readonly IEmergencyAlertRepository _alertRepository;

        protected override async Task<Result<DispatchDashboardDto>> HandleQueryAsync(
            GetDispatchDashboardQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var activeBookings = await _bookingRepository.GetActiveBookingsAsync(request.TenantId);
                var availablePersonnel = await _personnelRepository.GetAvailablePersonnelAsync(request.TenantId);
                var activeAlerts = await _alertRepository.GetActiveAlertsAsync(request.TenantId);

                var dashboard = new DispatchDashboardDto
                {
                    ActiveBookings = activeBookings.Select(MapToBookingDto).ToList(),
                    AvailablePersonnel = availablePersonnel.Select(MapToPersonnelDto).ToList(),
                    ActiveEmergencyAlerts = activeAlerts.Select(MapToAlertDto).ToList(),
                    TotalActiveServices = activeBookings.Count,
                    AvailableResourceCount = availablePersonnel.Count,
                    EmergencyAlertCount = activeAlerts.Count(a => a.Priority >= AlertPriority.High),
                    LastUpdated = DateTime.UtcNow
                };

                return Result<DispatchDashboardDto>.Success(dashboard);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting dispatch dashboard for dispatch center {DispatchCenterId}", 
                    request.DispatchCenterId);
                return Result<DispatchDashboardDto>.Failure($"Failed to get dashboard: {ex.Message}");
            }
        }
    }

    public class GetServiceMetricsHandler : BaseQueryHandler<GetServiceMetricsQuery, ServiceMetricsDto>
    {
        private readonly IAnalyticsRepository _analyticsRepository;

        protected override async Task<Result<ServiceMetricsDto>> HandleQueryAsync(
            GetServiceMetricsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var metrics = await _analyticsRepository.GetServiceMetricsAsync(
                    request.TenantId,
                    request.FromDate,
                    request.ToDate,
                    request.ServiceType,
                    request.ServiceProviderId
                );

                var dto = new ServiceMetricsDto
                {
                    TotalServices = metrics.TotalBookings,
                    CompletedServices = metrics.CompletedBookings,
                    CancelledServices = metrics.CancelledBookings,
                    AverageCompletionTime = metrics.AverageCompletionTimeMinutes,
                    AverageRating = metrics.AverageCustomerRating,
                    TotalRevenue = metrics.TotalRevenue,
                    ServiceTypeBreakdown = metrics.ServiceTypeMetrics.ToDictionary(
                        m => m.ServiceType,
                        m => new ServiceTypeMetric
                        {
                            Count = m.Count,
                            Revenue = m.Revenue,
                            AverageRating = m.AverageRating
                        }
                    ),
                    DailyMetrics = metrics.DailyMetrics.Select(d => new DailyMetric
                    {
                        Date = d.Date,
                        ServiceCount = d.ServiceCount,
                        Revenue = d.Revenue,
                        AverageRating = d.AverageRating
                    }).ToList()
                };

                return Result<ServiceMetricsDto>.Success(dto);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting service metrics for tenant {TenantId}", request.TenantId);
                return Result<ServiceMetricsDto>.Failure($"Failed to get metrics: {ex.Message}");
            }
        }
    }
}
```

### 2.3 Infrastructure Layer Implementation

#### Real-Time Tracking Service
```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.Services
{
    public interface IRealtimeTrackingService
    {
        Task UpdateLocationAsync(Guid personnelId, Location location, DateTime timestamp);
        Task<Location?> GetCurrentLocationAsync(Guid personnelId);
        Task<List<TrackingUpdate>> GetLocationHistoryAsync(Guid personnelId, DateTime from, DateTime to);
        Task StartTrackingAsync(Guid bookingId, Guid personnelId);
        Task StopTrackingAsync(Guid bookingId);
        Task NotifyLocationUpdateAsync(Guid bookingId, Location location);
    }

    public class RealtimeTrackingService : IRealtimeTrackingService
    {
        private readonly IHubContext<TrackingHub> _hubContext;
        private readonly IRedisCache _cache;
        private readonly ILocationRepository _locationRepository;
        private readonly ILogger<RealtimeTrackingService> _logger;

        public async Task UpdateLocationAsync(Guid personnelId, Location location, DateTime timestamp)
        {
            try
            {
                // Store in Redis for real-time access
                var cacheKey = $"location:personnel:{personnelId}";
                var locationData = new
                {
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    Timestamp = timestamp,
                    Address = location.Address
                };

                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(locationData), 
                    TimeSpan.FromMinutes(30));

                // Store in database for history
                await _locationRepository.AddLocationUpdateAsync(personnelId, location, timestamp);

                // Notify connected clients
                await _hubContext.Clients.Group($"personnel_{personnelId}")
                    .SendAsync("LocationUpdated", locationData);

                // Check for active bookings and notify customers
                var activeBookings = await GetActiveBookingsForPersonnelAsync(personnelId);
                foreach (var bookingId in activeBookings)
                {
                    await NotifyLocationUpdateAsync(bookingId, location);
                }

                _logger.LogDebug("Location updated for personnel {PersonnelId}: {Location}", 
                    personnelId, location);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location for personnel {PersonnelId}", personnelId);
                throw;
            }
        }

        public async Task<Location?> GetCurrentLocationAsync(Guid personnelId)
        {
            try
            {
                var cacheKey = $"location:personnel:{personnelId}";
                var locationJson = await _cache.GetStringAsync(cacheKey);

                if (string.IsNullOrEmpty(locationJson))
                    return null;

                var locationData = JsonSerializer.Deserialize<dynamic>(locationJson);
                return new Location(
                    locationData.Latitude,
                    locationData.Longitude,
                    locationData.Address
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current location for personnel {PersonnelId}", personnelId);
                return null;
            }
        }

        public async Task NotifyLocationUpdateAsync(Guid bookingId, Location location)
        {
            await _hubContext.Clients.Group($"booking_{bookingId}")
                .SendAsync("DriverLocationUpdate", new
                {
                    BookingId = bookingId,
                    Location = location,
                    Timestamp = DateTime.UtcNow
                });
        }

        private async Task<List<Guid>> GetActiveBookingsForPersonnelAsync(Guid personnelId)
        {
            // Implementation to get active bookings for personnel
            // This would query the booking repository
            return new List<Guid>();
        }
    }

    [Authorize]
    public class TrackingHub : Hub
    {
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<TrackingHub> _logger;

        public async Task JoinBookingTracking(string bookingId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"booking_{bookingId}");
            _logger.LogInformation("User joined booking tracking: {BookingId}", bookingId);
        }

        public async Task LeaveBookingTracking(string bookingId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"booking_{bookingId}");
            _logger.LogInformation("User left booking tracking: {BookingId}", bookingId);
        }

        public async Task JoinPersonnelTracking(string personnelId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"personnel_{personnelId}");
            _logger.LogInformation("User joined personnel tracking: {PersonnelId}", personnelId);
        }

        public async Task UpdatePersonnelLocation(string personnelId, decimal latitude, decimal longitude)
        {
            var location = new Location(latitude, longitude);
            // Notify all subscribers
            await Clients.Group($"personnel_{personnelId}")
                .SendAsync("LocationUpdated", new { PersonnelId = personnelId, Location = location, Timestamp = DateTime.UtcNow });
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
```

#### Emergency Response Service
```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.Services
{
    public interface IEmergencyService
    {
        Task<EmergencyAlert> CreatePanicAlertAsync(Personnel personnel, ServiceBookingId? bookingId, Location location);
        Task<List<Personnel>> FindNearestEmergencyResponsePersonnelAsync(Location location, int radiusKm = 10);
        Task NotifyEmergencyServicesAsync(EmergencyAlert alert);
        Task EscalateAlertAsync(EmergencyAlertId alertId, EscalationLevel level);
        Task ResolveAlertAsync(EmergencyAlertId alertId, string resolution, Guid resolvedBy);
    }

    public class EmergencyService : IEmergencyService
    {
        private readonly IEmergencyAlertRepository _alertRepository;
        private readonly IPersonnelRepository _personnelRepository;
        private readonly INotificationService _notificationService;
        private readonly IExternalEmergencyApiService _emergencyApiService;
        private readonly ILogger<EmergencyService> _logger;

        public async Task<EmergencyAlert> CreatePanicAlertAsync(Personnel personnel, 
            ServiceBookingId? bookingId, Location location)
        {
            try
            {
                var alert = new EmergencyAlert(
                    DispatchCenterId.From(personnel.AssignedDispatchCenterId),
                    EmergencyAlertType.PanicButton,
                    location,
                    $"Panic button activated by {personnel.PersonalInfo.FirstName} {personnel.PersonalInfo.LastName}",
                    personnel.Id
                );

                alert.SetBookingId(bookingId);
                alert.SetPriority(AlertPriority.Critical);

                await _alertRepository.AddAsync(alert);

                // Start automatic escalation timer
                _ = Task.Delay(TimeSpan.FromMinutes(2))
                    .ContinueWith(async _ => await EscalateAlertAsync(alert.Id, EscalationLevel.Automatic));

                _logger.LogCritical("Panic alert created: {AlertId} for personnel {PersonnelId} at {Location}", 
                    alert.Id, personnel.Id, location);

                return alert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating panic alert for personnel {PersonnelId}", personnel.Id);
                throw;
            }
        }

        public async Task<List<Personnel>> FindNearestEmergencyResponsePersonnelAsync(Location location, int radiusKm = 10)
        {
            var availablePersonnel = await _personnelRepository.GetAvailablePersonnelAsync();
            
            return availablePersonnel
                .Where(p => location.DistanceToKm(p.CurrentLocation) <= radiusKm)
                .Where(p => p.HasEmergencyResponseCertification())
                .OrderBy(p => location.DistanceToKm(p.CurrentLocation))
                .Take(5)
                .ToList();
        }

        public async Task NotifyEmergencyServicesAsync(EmergencyAlert alert)
        {
            try
            {
                // Integrate with local emergency services API
                await _emergencyApiService.ReportEmergencyAsync(new EmergencyReport
                {
                    Type = MapToExternalType(alert.AlertType),
                    Location = alert.Location,
                    Description = alert.Description,
                    ContactInfo = alert.ReportedBy?.ContactInfo,
                    Timestamp = alert.CreatedAt
                });

                _logger.LogInformation("Emergency services notified for alert {AlertId}", alert.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify emergency services for alert {AlertId}", alert.Id);
                // Don't throw - this is a non-critical failure
            }
        }

        public async Task EscalateAlertAsync(EmergencyAlertId alertId, EscalationLevel level)
        {
            var alert = await _alertRepository.GetByIdAsync(alertId);
            if (alert == null || alert.Status == EmergencyAlertStatus.Resolved)
                return;

            alert.Escalate(level);

            switch (level)
            {
                case EscalationLevel.Supervisor:
                    await NotifySupervisorsAsync(alert);
                    break;
                case EscalationLevel.Management:
                    await NotifyManagementAsync(alert);
                    break;
                case EscalationLevel.External:
                    await NotifyEmergencyServicesAsync(alert);
                    break;
            }

            _logger.LogWarning("Alert {AlertId} escalated to level {Level}", alertId, level);
        }

        private async Task NotifySupervisorsAsync(EmergencyAlert alert)
        {
            var supervisors = await _personnelRepository.GetPersonnelByTypeAsync(PersonnelType.Supervisor);
            foreach (var supervisor in supervisors)
            {
                await _notificationService.SendEmergencyNotificationAsync(supervisor, alert);
            }
        }

        private async Task NotifyManagementAsync(EmergencyAlert alert)
        {
            // Implementation for notifying management team
            await _notificationService.SendManagementAlertAsync(alert);
        }
    }

    public enum EscalationLevel
    {
        None = 0,
        Automatic = 1,
        Supervisor = 2,
        Management = 3,
        External = 4
    }

    public enum AlertPriority
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}
```

---

## 3. Database Schema (PostgreSQL)

### 3.1 Core Service Tables

```sql
-- GARDIENNAGE/VTC Module Schema
CREATE SCHEMA IF NOT EXISTS gardiennage_vtc;

-- Service Providers
CREATE TABLE gardiennage_vtc.service_providers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    company_name VARCHAR(255) NOT NULL,
    license_number VARCHAR(100) NOT NULL,
    type INTEGER NOT NULL, -- 1: VTC, 2: Security, 3: Courier, 4: MultiService
    status INTEGER NOT NULL DEFAULT 0, -- 0: Pending, 1: Active, 2: Suspended, 3: Cancelled
    contact_email VARCHAR(255) NOT NULL,
    contact_phone VARCHAR(50),
    contact_first_name VARCHAR(100),
    contact_last_name VARCHAR(100),
    head_office_street VARCHAR(255),
    head_office_city VARCHAR(100),
    head_office_postal_code VARCHAR(20),
    head_office_country VARCHAR(100),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    activated_at TIMESTAMP WITH TIME ZONE,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_service_providers_tenant FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id),
    CONSTRAINT uk_service_providers_license_tenant UNIQUE (license_number, tenant_id)
);

-- Service Offerings
CREATE TABLE gardiennage_vtc.service_offerings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    service_provider_id UUID NOT NULL,
    service_type INTEGER NOT NULL, -- 1: VTCRide, 2: SecurityPatrol, 3: CourierDelivery, 4: EmergencyResponse
    tier INTEGER NOT NULL, -- 1: Starter, 2: Professional, 3: Enterprise
    base_price DECIMAL(10,2) NOT NULL,
    distance_rate DECIMAL(10,4), -- per km
    time_rate DECIMAL(10,4), -- per minute
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_service_offerings_provider FOREIGN KEY (service_provider_id) REFERENCES gardiennage_vtc.service_providers(id) ON DELETE CASCADE,
    CONSTRAINT uk_service_offerings_provider_type_tier UNIQUE (service_provider_id, service_type, tier)
);

-- Personnel Management
CREATE TABLE gardiennage_vtc.personnel (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    service_provider_id UUID NOT NULL,
    tenant_id UUID NOT NULL,
    type INTEGER NOT NULL, -- 1: VTCDriver, 2: SecurityGuard, 3: Courier, 4: Dispatcher, 5: Supervisor
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: OnBreak, 3: OffDuty, 4: Suspended, 5: Terminated
    employee_number VARCHAR(50),
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    date_of_birth DATE NOT NULL,
    national_id VARCHAR(50),
    passport_number VARCHAR(50),
    email VARCHAR(255) NOT NULL,
    phone VARCHAR(50) NOT NULL,
    alternate_phone VARCHAR(50),
    emergency_contact_name VARCHAR(200) NOT NULL,
    emergency_contact_phone VARCHAR(50) NOT NULL,
    emergency_contact_relationship VARCHAR(100) NOT NULL,
    assigned_vehicle_id UUID,
    assigned_dispatch_center_id UUID,
    hired_date DATE NOT NULL,
    terminated_date DATE,
    current_latitude DECIMAL(10,8),
    current_longitude DECIMAL(11,8),
    current_location_updated_at TIMESTAMP WITH TIME ZONE,
    average_rating DECIMAL(3,2) DEFAULT 0.00,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_personnel_service_provider FOREIGN KEY (service_provider_id) REFERENCES gardiennage_vtc.service_providers(id) ON DELETE CASCADE,
    CONSTRAINT fk_personnel_tenant FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id),
    CONSTRAINT fk_personnel_vehicle FOREIGN KEY (assigned_vehicle_id) REFERENCES vehicles.vehicles(id)
);

-- Personnel Certifications
CREATE TABLE gardiennage_vtc.personnel_certifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    personnel_id UUID NOT NULL,
    type INTEGER NOT NULL, -- 1: DrivingLicense, 2: VTCLicense, 3: SecurityLicense, 4: FirstAid, 5: EmergencyResponse
    certification_number VARCHAR(100) NOT NULL,
    issued_date DATE NOT NULL,
    expiry_date DATE NOT NULL,
    issuing_authority VARCHAR(255) NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Valid, 2: Expired, 3: Suspended, 4: Revoked
    document_file_id UUID,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_personnel_certifications_personnel FOREIGN KEY (personnel_id) REFERENCES gardiennage_vtc.personnel(id) ON DELETE CASCADE,
    CONSTRAINT fk_personnel_certifications_file FOREIGN KEY (document_file_id) REFERENCES files.files(id)
);

-- Customers
CREATE TABLE gardiennage_vtc.customers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    type INTEGER NOT NULL DEFAULT 1, -- 1: Individual, 2: Corporate, 3: Government
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: Suspended, 3: Blocked
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    company_name VARCHAR(255),
    email VARCHAR(255) NOT NULL,
    phone VARCHAR(50) NOT NULL,
    alternate_phone VARCHAR(50),
    date_of_birth DATE,
    registration_number VARCHAR(100), -- For corporate customers
    preferred_language VARCHAR(10) DEFAULT 'fr',
    communication_preferences JSONB, -- Email, SMS, Push notification preferences
    registered_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    last_active_at TIMESTAMP WITH TIME ZONE,
    average_rating DECIMAL(3,2) DEFAULT 0.00,
    total_bookings INTEGER DEFAULT 0,
    total_spent DECIMAL(12,2) DEFAULT 0.00,
    
    CONSTRAINT fk_customers_tenant FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id),
    CONSTRAINT uk_customers_email_tenant UNIQUE (email, tenant_id)
);

-- Customer Addresses
CREATE TABLE gardiennage_vtc.customer_addresses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL,
    type INTEGER NOT NULL, -- 1: Home, 2: Work, 3: Other
    label VARCHAR(100),
    street VARCHAR(255) NOT NULL,
    city VARCHAR(100) NOT NULL,
    postal_code VARCHAR(20) NOT NULL,
    country VARCHAR(100) NOT NULL,
    latitude DECIMAL(10,8),
    longitude DECIMAL(11,8),
    is_default BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_customer_addresses_customer FOREIGN KEY (customer_id) REFERENCES gardiennage_vtc.customers(id) ON DELETE CASCADE
);

-- Customer Payment Methods
CREATE TABLE gardiennage_vtc.customer_payment_methods (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL,
    type INTEGER NOT NULL, -- 1: CreditCard, 2: DebitCard, 3: PayPal, 4: BankTransfer, 5: Cash
    payment_token VARCHAR(255), -- Encrypted token from payment processor
    last_four_digits VARCHAR(4),
    card_brand VARCHAR(50),
    expiry_month INTEGER,
    expiry_year INTEGER,
    cardholder_name VARCHAR(255),
    is_default BOOLEAN NOT NULL DEFAULT false,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_customer_payment_methods_customer FOREIGN KEY (customer_id) REFERENCES gardiennage_vtc.customers(id) ON DELETE CASCADE
);
```

### 3.2 Service Booking Tables

```sql
-- Service Bookings
CREATE TABLE gardiennage_vtc.service_bookings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    service_provider_id UUID NOT NULL,
    customer_id UUID NOT NULL,
    service_type INTEGER NOT NULL,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Pending, 1: Confirmed, 2: Assigned, 3: InProgress, 4: Completed, 5: Cancelled
    booking_date TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    service_date TIMESTAMP WITH TIME ZONE NOT NULL,
    pickup_latitude DECIMAL(10,8) NOT NULL,
    pickup_longitude DECIMAL(11,8) NOT NULL,
    pickup_address VARCHAR(500),
    destination_latitude DECIMAL(10,8),
    destination_longitude DECIMAL(11,8),
    destination_address VARCHAR(500),
    assigned_personnel_id UUID,
    assigned_vehicle_id UUID,
    base_price DECIMAL(10,2) NOT NULL,
    distance_price DECIMAL(10,2) DEFAULT 0.00,
    time_price DECIMAL(10,2) DEFAULT 0.00,
    surcharge_price DECIMAL(10,2) DEFAULT 0.00,
    total_price DECIMAL(10,2) NOT NULL,
    currency VARCHAR(3) DEFAULT 'EUR',
    payment_status INTEGER NOT NULL DEFAULT 0, -- 0: Pending, 1: Authorized, 2: Captured, 3: Failed, 4: Refunded
    payment_method_id UUID,
    special_instructions TEXT,
    estimated_duration_minutes INTEGER,
    actual_duration_minutes INTEGER,
    estimated_distance_km DECIMAL(8,2),
    actual_distance_km DECIMAL(8,2),
    completed_at TIMESTAMP WITH TIME ZONE,
    cancelled_at TIMESTAMP WITH TIME ZONE,
    cancellation_reason TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_service_bookings_tenant FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id),
    CONSTRAINT fk_service_bookings_provider FOREIGN KEY (service_provider_id) REFERENCES gardiennage_vtc.service_providers(id),
    CONSTRAINT fk_service_bookings_customer FOREIGN KEY (customer_id) REFERENCES gardiennage_vtc.customers(id),
    CONSTRAINT fk_service_bookings_personnel FOREIGN KEY (assigned_personnel_id) REFERENCES gardiennage_vtc.personnel(id),
    CONSTRAINT fk_service_bookings_vehicle FOREIGN KEY (assigned_vehicle_id) REFERENCES vehicles.vehicles(id),
    CONSTRAINT fk_service_bookings_payment_method FOREIGN KEY (payment_method_id) REFERENCES gardiennage_vtc.customer_payment_methods(id)
);

-- Booking Status History
CREATE TABLE gardiennage_vtc.booking_status_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id UUID NOT NULL,
    previous_status INTEGER,
    new_status INTEGER NOT NULL,
    changed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    changed_by UUID,
    notes TEXT,
    
    CONSTRAINT fk_booking_status_history_booking FOREIGN KEY (booking_id) REFERENCES gardiennage_vtc.service_bookings(id) ON DELETE CASCADE
);

-- Real-time Tracking Updates
CREATE TABLE gardiennage_vtc.tracking_updates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id UUID NOT NULL,
    personnel_id UUID,
    latitude DECIMAL(10,8) NOT NULL,
    longitude DECIMAL(11,8) NOT NULL,
    address VARCHAR(500),
    event_type INTEGER NOT NULL, -- 1: Created, 2: Assigned, 3: EnRoute, 4: Arrived, 5: Started, 6: InProgress, 7: Completed, 8: Emergency
    timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    speed_kmh DECIMAL(5,2),
    heading_degrees INTEGER,
    accuracy_meters DECIMAL(6,2),
    notes TEXT,
    metadata JSONB, -- Additional tracking data
    
    CONSTRAINT fk_tracking_updates_booking FOREIGN KEY (booking_id) REFERENCES gardiennage_vtc.service_bookings(id) ON DELETE CASCADE,
    CONSTRAINT fk_tracking_updates_personnel FOREIGN KEY (personnel_id) REFERENCES gardiennage_vtc.personnel(id)
);

-- Service Routes (for multi-stop services)
CREATE TABLE gardiennage_vtc.service_routes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id UUID NOT NULL,
    sequence_order INTEGER NOT NULL,
    location_type INTEGER NOT NULL, -- 1: Pickup, 2: Waypoint, 3: Destination
    latitude DECIMAL(10,8) NOT NULL,
    longitude DECIMAL(11,8) NOT NULL,
    address VARCHAR(500),
    estimated_arrival TIMESTAMP WITH TIME ZONE,
    actual_arrival TIMESTAMP WITH TIME ZONE,
    notes TEXT,
    
    CONSTRAINT fk_service_routes_booking FOREIGN KEY (booking_id) REFERENCES gardiennage_vtc.service_bookings(id) ON DELETE CASCADE,
    CONSTRAINT uk_service_routes_booking_sequence UNIQUE (booking_id, sequence_order)
);
```

### 3.3 Dispatch and Emergency Tables

```sql
-- Dispatch Centers
CREATE TABLE gardiennage_vtc.dispatch_centers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    service_provider_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: Maintenance, 3: Emergency, 4: Offline
    location_latitude DECIMAL(10,8) NOT NULL,
    location_longitude DECIMAL(11,8) NOT NULL,
    location_address VARCHAR(500),
    coverage_radius_km DECIMAL(8,2) NOT NULL DEFAULT 50.0,
    coverage_areas JSONB, -- Polygon definitions for service areas
    emergency_contact_phone VARCHAR(50) NOT NULL,
    emergency_contact_email VARCHAR(255),
    operating_hours JSONB, -- Schedule configuration
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_dispatch_centers_tenant FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id),
    CONSTRAINT fk_dispatch_centers_provider FOREIGN KEY (service_provider_id) REFERENCES gardiennage_vtc.service_providers(id)
);

-- Dispatcher Assignments
CREATE TABLE gardiennage_vtc.dispatcher_assignments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    dispatch_center_id UUID NOT NULL,
    personnel_id UUID NOT NULL,
    shift_start TIMESTAMP WITH TIME ZONE NOT NULL,
    shift_end TIMESTAMP WITH TIME ZONE NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_dispatcher_assignments_center FOREIGN KEY (dispatch_center_id) REFERENCES gardiennage_vtc.dispatch_centers(id) ON DELETE CASCADE,
    CONSTRAINT fk_dispatcher_assignments_personnel FOREIGN KEY (personnel_id) REFERENCES gardiennage_vtc.personnel(id)
);

-- Emergency Alerts
CREATE TABLE gardiennage_vtc.emergency_alerts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dispatch_center_id UUID NOT NULL,
    alert_type INTEGER NOT NULL, -- 1: PanicButton, 2: VehicleBreakdown, 3: Accident, 4: SecurityIncident, 5: MedicalEmergency, 6: DeliveryIncident
    priority INTEGER NOT NULL DEFAULT 2, -- 1: Low, 2: Medium, 3: High, 4: Critical
    status INTEGER NOT NULL DEFAULT 0, -- 0: Open, 1: Acknowledged, 2: InProgress, 3: Resolved, 4: Cancelled
    latitude DECIMAL(10,8) NOT NULL,
    longitude DECIMAL(11,8) NOT NULL,
    address VARCHAR(500),
    description TEXT NOT NULL,
    reported_by UUID, -- Personnel who reported the alert
    booking_id UUID, -- Associated booking if applicable
    assigned_to UUID, -- Personnel assigned to handle the alert
    escalation_level INTEGER DEFAULT 0, -- 0: None, 1: Automatic, 2: Supervisor, 3: Management, 4: External
    external_reference VARCHAR(255), -- Reference number from emergency services
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    acknowledged_at TIMESTAMP WITH TIME ZONE,
    resolved_at TIMESTAMP WITH TIME ZONE,
    resolution_notes TEXT,
    
    CONSTRAINT fk_emergency_alerts_tenant FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id),
    CONSTRAINT fk_emergency_alerts_center FOREIGN KEY (dispatch_center_id) REFERENCES gardiennage_vtc.dispatch_centers(id),
    CONSTRAINT fk_emergency_alerts_reported_by FOREIGN KEY (reported_by) REFERENCES gardiennage_vtc.personnel(id),
    CONSTRAINT fk_emergency_alerts_booking FOREIGN KEY (booking_id) REFERENCES gardiennage_vtc.service_bookings(id),
    CONSTRAINT fk_emergency_alerts_assigned_to FOREIGN KEY (assigned_to) REFERENCES gardiennage_vtc.personnel(id)
);

-- Resource Allocations
CREATE TABLE gardiennage_vtc.resource_allocations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    dispatch_center_id UUID NOT NULL,
    emergency_alert_id UUID,
    booking_id UUID,
    resource_type INTEGER NOT NULL, -- 1: Personnel, 2: Vehicle, 3: Equipment
    resource_id UUID NOT NULL, -- Personnel ID, Vehicle ID, or Equipment ID
    allocation_priority INTEGER NOT NULL, -- 1: Low, 2: Normal, 3: High, 4: Emergency, 5: Critical
    allocated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    released_at TIMESTAMP WITH TIME ZONE,
    allocation_notes TEXT,
    
    CONSTRAINT fk_resource_allocations_center FOREIGN KEY (dispatch_center_id) REFERENCES gardiennage_vtc.dispatch_centers(id) ON DELETE CASCADE,
    CONSTRAINT fk_resource_allocations_alert FOREIGN KEY (emergency_alert_id) REFERENCES gardiennage_vtc.emergency_alerts(id),
    CONSTRAINT fk_resource_allocations_booking FOREIGN KEY (booking_id) REFERENCES gardiennage_vtc.service_bookings(id)
);

-- Incident Reports
CREATE TABLE gardiennage_vtc.incident_reports (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    booking_id UUID,
    emergency_alert_id UUID,
    reported_by UUID NOT NULL,
    incident_type INTEGER NOT NULL, -- 1: Accident, 2: Theft, 3: Damage, 4: CustomerComplaint, 5: SafetyViolation, 6: Other
    severity INTEGER NOT NULL, -- 1: Low, 2: Medium, 3: High, 4: Critical
    incident_date TIMESTAMP WITH TIME ZONE NOT NULL,
    location_latitude DECIMAL(10,8),
    location_longitude DECIMAL(11,8),
    location_address VARCHAR(500),
    title VARCHAR(255) NOT NULL,
    description TEXT NOT NULL,
    immediate_actions_taken TEXT,
    injuries_reported BOOLEAN DEFAULT false,
    property_damage_reported BOOLEAN DEFAULT false,
    police_notified BOOLEAN DEFAULT false,
    insurance_notified BOOLEAN DEFAULT false,
    customer_notified BOOLEAN DEFAULT false,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Open, 1: UnderInvestigation, 2: Resolved, 3: Closed
    investigation_notes TEXT,
    resolution_notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_incident_reports_tenant FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id),
    CONSTRAINT fk_incident_reports_booking FOREIGN KEY (booking_id) REFERENCES gardiennage_vtc.service_bookings(id),
    CONSTRAINT fk_incident_reports_alert FOREIGN KEY (emergency_alert_id) REFERENCES gardiennage_vtc.emergency_alerts(id),
    CONSTRAINT fk_incident_reports_reported_by FOREIGN KEY (reported_by) REFERENCES gardiennage_vtc.personnel(id)
);

-- Incident Report Attachments
CREATE TABLE gardiennage_vtc.incident_report_attachments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    incident_report_id UUID NOT NULL,
    file_id UUID NOT NULL,
    attachment_type INTEGER NOT NULL, -- 1: Photo, 2: Video, 3: Document, 4: Audio
    description VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_incident_attachments_report FOREIGN KEY (incident_report_id) REFERENCES gardiennage_vtc.incident_reports(id) ON DELETE CASCADE,
    CONSTRAINT fk_incident_attachments_file FOREIGN KEY (file_id) REFERENCES files.files(id)
);
```

### 3.4 Performance and Analytics Tables

```sql
-- Performance Metrics
CREATE TABLE gardiennage_vtc.performance_metrics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    personnel_id UUID,
    service_provider_id UUID,
    metric_type INTEGER NOT NULL, -- 1: AverageRating, 2: CompletionRate, 3: ResponseTime, 4: Revenue, 5: SafetyScore
    metric_value DECIMAL(15,4) NOT NULL,
    measurement_period_start DATE NOT NULL,
    measurement_period_end DATE NOT NULL,
    booking_count INTEGER DEFAULT 0,
    metadata JSONB, -- Additional metric details
    recorded_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_performance_metrics_tenant FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id),
    CONSTRAINT fk_performance_metrics_personnel FOREIGN KEY (personnel_id) REFERENCES gardiennage_vtc.personnel(id),
    CONSTRAINT fk_performance_metrics_provider FOREIGN KEY (service_provider_id) REFERENCES gardiennage_vtc.service_providers(id)
);

-- Customer Ratings and Reviews
CREATE TABLE gardiennage_vtc.customer_ratings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    booking_id UUID NOT NULL,
    customer_id UUID NOT NULL,
    personnel_id UUID,
    service_provider_id UUID NOT NULL,
    rating INTEGER NOT NULL CHECK (rating >= 1 AND rating <= 5),
    review_text TEXT,
    service_quality_rating INTEGER CHECK (service_quality_rating >= 1 AND service_quality_rating <= 5),
    punctuality_rating INTEGER CHECK (punctuality_rating >= 1 AND punctuality_rating <= 5),
    communication_rating INTEGER CHECK (communication_rating >= 1 AND communication_rating <= 5),
    vehicle_cleanliness_rating INTEGER CHECK (vehicle_cleanliness_rating >= 1 AND vehicle_cleanliness_rating <= 5),
    would_recommend BOOLEAN,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_customer_ratings_tenant FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id),
    CONSTRAINT fk_customer_ratings_booking FOREIGN KEY (booking_id) REFERENCES gardiennage_vtc.service_bookings(id),
    CONSTRAINT fk_customer_ratings_customer FOREIGN KEY (customer_id) REFERENCES gardiennage_vtc.customers(id),
    CONSTRAINT fk_customer_ratings_personnel FOREIGN KEY (personnel_id) REFERENCES gardiennage_vtc.personnel(id),
    CONSTRAINT fk_customer_ratings_provider FOREIGN KEY (service_provider_id) REFERENCES gardiennage_vtc.service_providers(id),
    CONSTRAINT uk_customer_ratings_booking UNIQUE (booking_id)
);

-- Revenue Analytics
CREATE TABLE gardiennage_vtc.revenue_analytics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    service_provider_id UUID,
    personnel_id UUID,
    analysis_date DATE NOT NULL,
    service_type INTEGER,
    total_bookings INTEGER NOT NULL DEFAULT 0,
    completed_bookings INTEGER NOT NULL DEFAULT 0,
    cancelled_bookings INTEGER NOT NULL DEFAULT 0,
    gross_revenue DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    net_revenue DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    commission_amount DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    refund_amount DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    average_booking_value DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    peak_hour_revenue DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    off_peak_revenue DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CONSTRAINT fk_revenue_analytics_tenant FOREIGN KEY (tenant_id) REFERENCES admin.tenants(id),
    CONSTRAINT fk_revenue_analytics_provider FOREIGN KEY (service_provider_id) REFERENCES gardiennage_vtc.service_providers(id),
    CONSTRAINT fk_revenue_analytics_personnel FOREIGN KEY (personnel_id) REFERENCES gardiennage_vtc.personnel(id),
    CONSTRAINT uk_revenue_analytics_date_provider UNIQUE (analysis_date, service_provider_id, personnel_id, service_type)
);
```

### 3.5 Indexes for Performance Optimization

```sql
-- Primary performance indexes
CREATE INDEX idx_service_bookings_tenant_date ON gardiennage_vtc.service_bookings(tenant_id, service_date DESC);
CREATE INDEX idx_service_bookings_status ON gardiennage_vtc.service_bookings(status, service_date);
CREATE INDEX idx_service_bookings_customer ON gardiennage_vtc.service_bookings(customer_id, service_date DESC);
CREATE INDEX idx_service_bookings_personnel ON gardiennage_vtc.service_bookings(assigned_personnel_id, service_date DESC);
CREATE INDEX idx_service_bookings_provider ON gardiennage_vtc.service_bookings(service_provider_id, service_date DESC);

-- Geospatial indexes for location-based queries
CREATE INDEX idx_service_bookings_pickup_location ON gardiennage_vtc.service_bookings USING GIST(
    point(pickup_longitude, pickup_latitude)
);
CREATE INDEX idx_personnel_current_location ON gardiennage_vtc.personnel USING GIST(
    point(current_longitude, current_latitude)
) WHERE current_latitude IS NOT NULL AND current_longitude IS NOT NULL;
CREATE INDEX idx_tracking_updates_location ON gardiennage_vtc.tracking_updates USING GIST(
    point(longitude, latitude)
);

-- Real-time tracking performance indexes
CREATE INDEX idx_tracking_updates_booking_timestamp ON gardiennage_vtc.tracking_updates(booking_id, timestamp DESC);
CREATE INDEX idx_tracking_updates_personnel_timestamp ON gardiennage_vtc.tracking_updates(personnel_id, timestamp DESC);
CREATE INDEX idx_personnel_status_type ON gardiennage_vtc.personnel(status, type) WHERE status IN (1, 2); -- Active, OnBreak

-- Emergency and safety indexes
CREATE INDEX idx_emergency_alerts_tenant_status ON gardiennage_vtc.emergency_alerts(tenant_id, status, priority DESC);
CREATE INDEX idx_emergency_alerts_location ON gardiennage_vtc.emergency_alerts USING GIST(
    point(longitude, latitude)
) WHERE status IN (0, 1, 2); -- Open, Acknowledged, InProgress
CREATE INDEX idx_emergency_alerts_created_at ON gardiennage_vtc.emergency_alerts(created_at DESC) WHERE status IN (0, 1, 2);

-- Certification tracking indexes
CREATE INDEX idx_personnel_certifications_expiry ON gardiennage_vtc.personnel_certifications(personnel_id, expiry_date) WHERE status = 1;
CREATE INDEX idx_personnel_certifications_type_expiry ON gardiennage_vtc.personnel_certifications(type, expiry_date) WHERE status = 1;

-- Analytics and reporting indexes
CREATE INDEX idx_performance_metrics_tenant_period ON gardiennage_vtc.performance_metrics(tenant_id, measurement_period_end DESC);
CREATE INDEX idx_customer_ratings_provider_date ON gardiennage_vtc.customer_ratings(service_provider_id, created_at DESC);
CREATE INDEX idx_revenue_analytics_tenant_date ON gardiennage_vtc.revenue_analytics(tenant_id, analysis_date DESC);

-- Composite indexes for common query patterns
CREATE INDEX idx_service_bookings_tenant_status_date ON gardiennage_vtc.service_bookings(tenant_id, status, service_date DESC);
CREATE INDEX idx_personnel_provider_type_status ON gardiennage_vtc.personnel(service_provider_id, type, status);
CREATE INDEX idx_booking_status_history_booking_changed ON gardiennage_vtc.booking_status_history(booking_id, changed_at DESC);

-- Partial indexes for frequently filtered data
CREATE INDEX idx_service_bookings_active ON gardiennage_vtc.service_bookings(tenant_id, service_date DESC) 
    WHERE status IN (1, 2, 3); -- Confirmed, Assigned, InProgress
CREATE INDEX idx_personnel_available ON gardiennage_vtc.personnel(service_provider_id, type) 
    WHERE status = 1 AND assigned_vehicle_id IS NOT NULL; -- Active with vehicle
CREATE INDEX idx_customers_active ON gardiennage_vtc.customers(tenant_id, last_active_at DESC) 
    WHERE status = 1; -- Active customers only
```

---

## 4. Mobile Applications Architecture

### 4.1 Personnel Mobile App (React Native)

#### Features and Screens
```typescript
// Personnel App Architecture
interface PersonnelAppStructure {
  authentication: {
    login: LoginScreen;
    biometricAuth: BiometricAuthScreen;
    resetPassword: ResetPasswordScreen;
  };
  
  dashboard: {
    main: DashboardScreen;
    todaySchedule: ScheduleScreen;
    notifications: NotificationsScreen;
    earnings: EarningsScreen;
  };
  
  serviceManagement: {
    availableServices: AvailableServicesScreen;
    activeService: ActiveServiceScreen;
    serviceHistory: ServiceHistoryScreen;
    serviceDetails: ServiceDetailsScreen;
  };
  
  navigation: {
    routePlanning: RoutePlanningScreen;
    turnByTurn: NavigationScreen;
    waypointManagement: WaypointScreen;
  };
  
  emergency: {
    panicButton: PanicButtonScreen;
    emergencyContacts: EmergencyContactsScreen;
    incidentReporting: IncidentReportScreen;
  };
  
  communication: {
    customerChat: CustomerChatScreen;
    dispatchChat: DispatchChatScreen;
    notifications: NotificationCenterScreen;
  };
  
  profile: {
    personalInfo: ProfileScreen;
    certifications: CertificationsScreen;
    documents: DocumentsScreen;
    settings: SettingsScreen;
  };
}

// Real-time Location Service
export class LocationTrackingService {
  private locationSubscription: any;
  private websocketConnection: WebSocketConnection;
  
  async startTracking(personnelId: string, bookingId?: string): Promise<void> {
    // Request location permissions
    const permission = await Location.requestForegroundPermissionsAsync();
    if (permission.status !== 'granted') {
      throw new Error('Location permission denied');
    }
    
    // Start location tracking with high accuracy
    this.locationSubscription = await Location.watchPositionAsync(
      {
        accuracy: Location.Accuracy.BestForNavigation,
        timeInterval: 10000, // 10 seconds
        distanceInterval: 50, // 50 meters
      },
      (location) => this.handleLocationUpdate(location, personnelId, bookingId)
    );
  }
  
  private async handleLocationUpdate(
    location: Location.LocationObject, 
    personnelId: string, 
    bookingId?: string
  ): Promise<void> {
    const trackingData = {
      personnelId,
      bookingId,
      latitude: location.coords.latitude,
      longitude: location.coords.longitude,
      accuracy: location.coords.accuracy,
      speed: location.coords.speed,
      heading: location.coords.heading,
      timestamp: new Date(location.timestamp).toISOString()
    };
    
    // Send via WebSocket for real-time updates
    await this.websocketConnection.send('UpdateLocation', trackingData);
    
    // Also store locally for offline capability
    await AsyncStorage.setItem(
      `lastKnownLocation_${personnelId}`, 
      JSON.stringify(trackingData)
    );
  }
  
  async stopTracking(): Promise<void> {
    if (this.locationSubscription) {
      this.locationSubscription.remove();
      this.locationSubscription = null;
    }
  }
}

// Service Management Component
export const ActiveServiceScreen: React.FC = () => {
  const [currentBooking, setCurrentBooking] = useState<ServiceBooking | null>(null);
  const [customerLocation, setCustomerLocation] = useState<Location | null>(null);
  const [isNavigating, setIsNavigating] = useState(false);
  
  const startService = async () => {
    try {
      const location = await getCurrentLocation();
      await api.startService(currentBooking!.id, location);
      setIsNavigating(true);
      
      // Start real-time tracking
      await locationService.startTracking(
        user.personnelId, 
        currentBooking!.id
      );
      
      showSuccessNotification('Service started successfully');
    } catch (error) {
      showErrorNotification('Failed to start service');
    }
  };
  
  const completeService = async () => {
    try {
      const location = await getCurrentLocation();
      await api.completeService(currentBooking!.id, location);
      
      // Stop tracking
      await locationService.stopTracking();
      
      // Navigate to rating screen
      navigation.navigate('CustomerRating', { bookingId: currentBooking!.id });
    } catch (error) {
      showErrorNotification('Failed to complete service');
    }
  };
  
  return (
    <View style={styles.container}>
      <ServiceHeader booking={currentBooking} />
      <CustomerInfo customer={currentBooking?.customer} />
      <NavigationMap 
        destination={customerLocation}
        showTrafficLayer={true}
        onNavigationStart={() => setIsNavigating(true)}
      />
      <ServiceActions 
        onStart={startService}
        onComplete={completeService}
        onEmergency={activatePanicButton}
        isActive={currentBooking?.status === 'InProgress'}
      />
    </View>
  );
};

// Emergency Panic Button
export const PanicButtonComponent: React.FC = () => {
  const [isEmergency, setIsEmergency] = useState(false);
  const [countdown, setCountdown] = useState(0);
  
  const activatePanicButton = async () => {
    try {
      // Get current precise location
      const location = await getCurrentLocation(true);
      
      // Send emergency alert
      await api.activatePanicButton({
        personnelId: user.personnelId,
        bookingId: currentBooking?.id,
        location,
        timestamp: new Date().toISOString()
      });
      
      setIsEmergency(true);
      
      // Start emergency countdown
      startEmergencyCountdown();
      
      // Vibrate device
      Vibration.vibrate([0, 500, 200, 500]);
      
      // Send local notification
      await Notifications.scheduleNotificationAsync({
        content: {
          title: 'Emergency Alert Sent',
          body: 'Dispatch and emergency services have been notified',
          sound: 'emergency.wav',
        },
        trigger: null,
      });
      
    } catch (error) {
      Alert.alert('Error', 'Failed to send emergency alert. Please call emergency services directly.');
    }
  };
  
  return (
    <View style={styles.emergencyContainer}>
      <TouchableOpacity
        style={[styles.panicButton, isEmergency && styles.panicButtonActive]}
        onPress={activatePanicButton}
        disabled={isEmergency}
      >
        <Text style={styles.panicButtonText}>
          {isEmergency ? `Emergency Active (${countdown}s)` : 'Emergency'}
        </Text>
      </TouchableOpacity>
    </View>
  );
};
```

### 4.2 Customer Mobile App (React Native)

#### Customer App Features
```typescript
// Customer App Architecture
interface CustomerAppStructure {
  authentication: {
    registration: RegistrationScreen;
    login: LoginScreen;
    socialAuth: SocialAuthScreen;
  };
  
  booking: {
    serviceSelection: ServiceSelectionScreen;
    locationPicker: LocationPickerScreen;
    bookingDetails: BookingDetailsScreen;
    paymentMethod: PaymentMethodScreen;
    bookingConfirmation: ConfirmationScreen;
  };
  
  tracking: {
    liveTracking: LiveTrackingScreen;
    etaDisplay: ETADisplayScreen;
    personnelInfo: PersonnelInfoScreen;
  };
  
  history: {
    bookingHistory: BookingHistoryScreen;
    receipts: ReceiptsScreen;
    ratings: RatingsScreen;
  };
  
  profile: {
    personalInfo: ProfileScreen;
    addresses: AddressesScreen;
    paymentMethods: PaymentMethodsScreen;
    preferences: PreferencesScreen;
  };
}

// Service Booking Flow
export const ServiceBookingScreen: React.FC = () => {
  const [serviceType, setServiceType] = useState<ServiceType>('VTCRide');
  const [pickupLocation, setPickupLocation] = useState<Location | null>(null);
  const [destinationLocation, setDestinationLocation] = useState<Location | null>(null);
  const [serviceDate, setServiceDate] = useState(new Date());
  const [pricing, setPricing] = useState<PricingInfo | null>(null);
  
  const calculatePricing = async () => {
    if (!pickupLocation) return;
    
    try {
      const pricingInfo = await api.calculatePricing({
        serviceType,
        pickupLocation,
        destinationLocation,
        serviceDate,
        tier: 'Professional'
      });
      
      setPricing(pricingInfo);
    } catch (error) {
      showErrorNotification('Failed to calculate pricing');
    }
  };
  
  const createBooking = async () => {
    try {
      const booking = await api.createBooking({
        serviceType,
        pickupLocation: pickupLocation!,
        destinationLocation,
        serviceDate,
        paymentMethodId: selectedPaymentMethod.id,
        specialInstructions
      });
      
      // Navigate to tracking screen
      navigation.navigate('LiveTracking', { bookingId: booking.id });
      
      showSuccessNotification('Booking created successfully');
    } catch (error) {
      showErrorNotification('Failed to create booking');
    }
  };
  
  return (
    <ScrollView style={styles.container}>
      <ServiceTypeSelector 
        value={serviceType} 
        onChange={setServiceType}
      />
      
      <LocationPicker
        label="Pickup Location"
        value={pickupLocation}
        onChange={setPickupLocation}
        showCurrentLocation={true}
      />
      
      {serviceType === 'VTCRide' && (
        <LocationPicker
          label="Destination"
          value={destinationLocation}
          onChange={setDestinationLocation}
        />
      )}
      
      <DateTimePicker
        value={serviceDate}
        onChange={setServiceDate}
        minimumDate={new Date()}
      />
      
      {pricing && (
        <PricingDisplay pricing={pricing} />
      )}
      
      <Button
        title="Book Service"
        onPress={createBooking}
        disabled={!pickupLocation || !pricing}
      />
    </ScrollView>
  );
};

// Live Tracking Component
export const LiveTrackingScreen: React.FC<{ bookingId: string }> = ({ bookingId }) => {
  const [booking, setBooking] = useState<ServiceBooking | null>(null);
  const [personnelLocation, setPersonnelLocation] = useState<Location | null>(null);
  const [eta, setEta] = useState<number | null>(null);
  
  useEffect(() => {
    // Subscribe to real-time updates
    const subscription = websocketConnection.subscribe(
      `booking_${bookingId}`,
      (data) => {
        switch (data.eventType) {
          case 'LocationUpdate':
            setPersonnelLocation(data.location);
            break;
          case 'StatusUpdate':
            setBooking(prev => ({ ...prev!, status: data.status }));
            break;
          case 'ETAUpdate':
            setEta(data.eta);
            break;
        }
      }
    );
    
    return () => subscription.unsubscribe();
  }, [bookingId]);
  
  const sendMessage = async (message: string) => {
    await api.sendMessage(bookingId, message);
  };
  
  const cancelBooking = async () => {
    try {
      await api.cancelBooking(bookingId, 'Customer cancellation');
      navigation.goBack();
    } catch (error) {
      showErrorNotification('Failed to cancel booking');
    }
  };
  
  return (
    <View style={styles.container}>
      <TrackingMap
        booking={booking}
        personnelLocation={personnelLocation}
        customerLocation={booking?.pickupLocation}
        destination={booking?.destinationLocation}
      />
      
      <BookingStatusCard
        status={booking?.status}
        eta={eta}
        personnel={booking?.assignedPersonnel}
      />
      
      <ActionButtons
        onMessage={() => navigation.navigate('Chat', { bookingId })}
        onCall={() => callPersonnel(booking?.assignedPersonnel?.phone)}
        onCancel={cancelBooking}
        canCancel={booking?.status === 'Confirmed' || booking?.status === 'Assigned'}
      />
    </View>
  );
};
```

### 4.3 Dispatcher Web Dashboard (React)

#### Real-time Operations Dashboard
```typescript
// Dispatcher Dashboard Architecture
export const DispatchDashboard: React.FC = () => {
  const [activeBookings, setActiveBookings] = useState<ServiceBooking[]>([]);
  const [availablePersonnel, setAvailablePersonnel] = useState<Personnel[]>([]);
  const [emergencyAlerts, setEmergencyAlerts] = useState<EmergencyAlert[]>([]);
  const [mapCenter, setMapCenter] = useState<Location>(defaultLocation);
  
  useEffect(() => {
    // Subscribe to real-time updates
    const hubConnection = new HubConnectionBuilder()
      .withUrl('/hubs/dispatch')
      .build();
    
    hubConnection.start().then(() => {
      hubConnection.invoke('JoinDispatchGroup', dispatchCenterId);
      
      // Listen for updates
      hubConnection.on('BookingUpdate', handleBookingUpdate);
      hubConnection.on('PersonnelLocationUpdate', handlePersonnelLocationUpdate);
      hubConnection.on('EmergencyAlert', handleEmergencyAlert);
    });
    
    return () => hubConnection.stop();
  }, []);
  
  const handleEmergencyAlert = (alert: EmergencyAlert) => {
    setEmergencyAlerts(prev => [alert, ...prev]);
    
    // Show emergency notification
    toast.error(`Emergency Alert: ${alert.alertType} at ${alert.location.address}`, {
      duration: 0, // Don't auto-dismiss
      action: {
        label: 'Respond',
        onClick: () => respondToEmergency(alert.id)
      }
    });
    
    // Play emergency sound
    playEmergencySound();
  };
  
  const assignBooking = async (bookingId: string, personnelId: string) => {
    try {
      await api.assignBooking(bookingId, personnelId);
      toast.success('Booking assigned successfully');
    } catch (error) {
      toast.error('Failed to assign booking');
    }
  };
  
  const optimizeRoutes = async () => {
    try {
      const optimization = await api.optimizeRoutes(dispatchCenterId);
      toast.success(`Routes optimized. Estimated savings: ${optimization.timeSavedMinutes} minutes`);
    } catch (error) {
      toast.error('Route optimization failed');
    }
  };
  
  return (
    <div className="dispatch-dashboard">
      <DashboardHeader 
        emergencyCount={emergencyAlerts.length}
        activeBookingCount={activeBookings.length}
        availablePersonnelCount={availablePersonnel.length}
      />
      
      <div className="dashboard-grid">
        <div className="map-section">
          <OperationsMap
            center={mapCenter}
            activeBookings={activeBookings}
            availablePersonnel={availablePersonnel}
            emergencyAlerts={emergencyAlerts}
            onBookingClick={selectBooking}
            onPersonnelClick={selectPersonnel}
          />
        </div>
        
        <div className="sidebar">
          <EmergencyAlerts 
            alerts={emergencyAlerts}
            onRespond={respondToEmergency}
            onEscalate={escalateEmergency}
          />
          
          <ActiveBookings
            bookings={activeBookings}
            onAssign={assignBooking}
            onTrack={trackBooking}
          />
          
          <AvailablePersonnel
            personnel={availablePersonnel}
            onAssign={assignPersonnel}
            onContact={contactPersonnel}
          />
        </div>
      </div>
      
      <ActionBar>
        <Button onClick={optimizeRoutes}>Optimize Routes</Button>
        <Button onClick={generateReport}>Generate Report</Button>
        <Button onClick={emergencyProtocol}>Emergency Protocol</Button>
      </ActionBar>
    </div>
  );
};

// Emergency Response Component
export const EmergencyResponsePanel: React.FC<{ alert: EmergencyAlert }> = ({ alert }) => {
  const [responseTeam, setResponseTeam] = useState<Personnel[]>([]);
  const [estimatedArrival, setEstimatedArrival] = useState<Date | null>(null);
  
  const allocateResources = async () => {
    try {
      const nearestPersonnel = await api.findNearestEmergencyPersonnel(
        alert.location,
        alert.alertType
      );
      
      setResponseTeam(nearestPersonnel);
      
      // Auto-assign closest available personnel
      if (nearestPersonnel.length > 0) {
        await api.allocateEmergencyResource(alert.id, nearestPersonnel[0].id);
      }
      
    } catch (error) {
      toast.error('Failed to allocate emergency resources');
    }
  };
  
  const escalateToExternal = async () => {
    try {
      await api.escalateEmergency(alert.id, 'External');
      toast.success('Emergency escalated to external services');
    } catch (error) {
      toast.error('Failed to escalate emergency');
    }
  };
  
  return (
    <div className="emergency-response-panel">
      <div className="alert-header">
        <h3>{alert.alertType}</h3>
        <span className={`priority-badge priority-${alert.priority}`}>
          {alert.priority}
        </span>
      </div>
      
      <div className="alert-details">
        <p><strong>Location:</strong> {alert.location.address}</p>
        <p><strong>Time:</strong> {formatDateTime(alert.createdAt)}</p>
        <p><strong>Description:</strong> {alert.description}</p>
      </div>
      
      <div className="response-team">
        <h4>Response Team</h4>
        {responseTeam.map(personnel => (
          <PersonnelCard key={personnel.id} personnel={personnel} />
        ))}
      </div>
      
      <div className="emergency-actions">
        <Button onClick={allocateResources} variant="primary">
          Allocate Resources
        </Button>
        <Button onClick={escalateToExternal} variant="danger">
          Escalate to 911
        </Button>
        <Button onClick={() => contactPersonnel(alert.reportedBy)}>
          Contact Reporter
        </Button>
      </div>
    </div>
  );
};
```

---

## 5. Analytics and Reporting System

### 5.1 Real-time Analytics Service

```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.Analytics
{
    public interface IAnalyticsService
    {
        Task<ServiceMetrics> GetServiceMetricsAsync(string tenantId, DateTime fromDate, DateTime toDate, 
            ServiceType? serviceType = null, Guid? serviceProviderId = null);
        Task<RevenueSummary> GetRevenueSummaryAsync(string tenantId, DateTime fromDate, DateTime toDate);
        Task<List<PersonnelPerformance>> GetPersonnelPerformanceAsync(string tenantId, DateTime fromDate, DateTime toDate);
        Task<OperationalInsights> GetOperationalInsightsAsync(string tenantId, DateTime fromDate, DateTime toDate);
        Task<PredictiveAnalytics> GetPredictiveAnalyticsAsync(string tenantId, string region);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly IAnalyticsRepository _analyticsRepository;
        private readonly IServiceBookingRepository _bookingRepository;
        private readonly IPersonnelRepository _personnelRepository;
        private readonly IMLPredictionService _mlService;
        private readonly ILogger<AnalyticsService> _logger;

        public async Task<ServiceMetrics> GetServiceMetricsAsync(string tenantId, DateTime fromDate, 
            DateTime toDate, ServiceType? serviceType = null, Guid? serviceProviderId = null)
        {
            try
            {
                var bookings = await _bookingRepository.GetBookingsForAnalyticsAsync(
                    tenantId, fromDate, toDate, serviceType, serviceProviderId);

                var metrics = new ServiceMetrics
                {
                    TotalBookings = bookings.Count,
                    CompletedBookings = bookings.Count(b => b.Status == BookingStatus.Completed),
                    CancelledBookings = bookings.Count(b => b.Status == BookingStatus.Cancelled),
                    TotalRevenue = bookings.Where(b => b.Status == BookingStatus.Completed)
                        .Sum(b => b.TotalPrice),
                    AverageCompletionTimeMinutes = bookings
                        .Where(b => b.Status == BookingStatus.Completed && b.ActualDurationMinutes.HasValue)
                        .Average(b => b.ActualDurationMinutes.Value),
                    AverageCustomerRating = await GetAverageRatingAsync(bookings.Select(b => b.Id).ToList()),
                    ServiceTypeBreakdown = bookings
                        .GroupBy(b => b.ServiceType)
                        .ToDictionary(
                            g => g.Key,
                            g => new ServiceTypeMetrics
                            {
                                Count = g.Count(),
                                Revenue = g.Where(b => b.Status == BookingStatus.Completed).Sum(b => b.TotalPrice),
                                AverageRating = GetServiceTypeAverageRating(g.ToList())
                            }
                        ),
                    HourlyDistribution = CalculateHourlyDistribution(bookings),
                    GeographicDistribution = CalculateGeographicDistribution(bookings),
                    CustomerRetentionRate = await CalculateCustomerRetentionRate(tenantId, fromDate, toDate)
                };

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating service metrics for tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<PredictiveAnalytics> GetPredictiveAnalyticsAsync(string tenantId, string region)
        {
            try
            {
                var historicalData = await _analyticsRepository.GetHistoricalDataAsync(tenantId, region, 
                    DateTime.UtcNow.AddDays(-90), DateTime.UtcNow);

                var predictions = await _mlService.PredictDemandAsync(historicalData);
                
                return new PredictiveAnalytics
                {
                    DemandForecast = predictions.DemandByHour,
                    OptimalPricing = predictions.SuggestedPricing,
                    StaffingRecommendations = predictions.StaffingNeeds,
                    RevenueProjection = predictions.ProjectedRevenue,
                    RiskFactors = predictions.IdentifiedRisks,
                    RecommendedActions = predictions.ActionItems
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating predictive analytics for tenant {TenantId}", tenantId);
                throw;
            }
        }

        private async Task<decimal> CalculateCustomerRetentionRate(string tenantId, DateTime fromDate, DateTime toDate)
        {
            var previousPeriod = fromDate.AddDays(-(toDate - fromDate).Days);
            
            var currentCustomers = await _bookingRepository.GetUniqueCustomersAsync(tenantId, fromDate, toDate);
            var previousCustomers = await _bookingRepository.GetUniqueCustomersAsync(tenantId, previousPeriod, fromDate);
            
            if (previousCustomers.Count == 0) return 0;
            
            var retainedCustomers = currentCustomers.Intersect(previousCustomers).Count();
            return (decimal)retainedCustomers / previousCustomers.Count * 100;
        }
    }

    // Machine Learning Service for Predictions
    public interface IMLPredictionService
    {
        Task<DemandPrediction> PredictDemandAsync(HistoricalData data);
        Task<PricingOptimization> OptimizePricingAsync(ServiceType serviceType, Location location, DateTime timeframe);
        Task<RouteOptimization> OptimizeRoutesAsync(List<ServiceBooking> bookings, List<Personnel> availablePersonnel);
        Task<RiskAssessment> AssessRiskFactorsAsync(string tenantId, Guid personnelId);
    }

    public class MLPredictionService : IMLPredictionService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MLPredictionService> _logger;

        public async Task<DemandPrediction> PredictDemandAsync(HistoricalData data)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("MLService");
                var request = new DemandPredictionRequest
                {
                    HistoricalBookings = data.Bookings,
                    WeatherData = data.WeatherData,
                    EventData = data.LocalEvents,
                    TimeRange = TimeSpan.FromDays(7) // Predict next 7 days
                };

                var response = await client.PostAsJsonAsync("/api/predict/demand", request);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<DemandPrediction>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting demand");
                
                // Fallback to rule-based prediction
                return CreateFallbackDemandPrediction(data);
            }
        }

        public async Task<RouteOptimization> OptimizeRoutesAsync(List<ServiceBooking> bookings, 
            List<Personnel> availablePersonnel)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("MLService");
                var request = new RouteOptimizationRequest
                {
                    Bookings = bookings.Select(b => new BookingLocation
                    {
                        Id = b.Id.Value,
                        PickupLocation = b.PickupLocation,
                        DestinationLocation = b.DestinationLocation,
                        ServiceDate = b.ServiceDate,
                        Priority = b.Priority
                    }).ToList(),
                    AvailablePersonnel = availablePersonnel.Select(p => new PersonnelLocation
                    {
                        Id = p.Id.Value,
                        CurrentLocation = p.CurrentLocation,
                        Capacity = p.MaxConcurrentBookings,
                        Skills = p.Skills
                    }).ToList()
                };

                var response = await client.PostAsJsonAsync("/api/optimize/routes", request);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<RouteOptimization>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing routes");
                
                // Fallback to simple nearest-neighbor assignment
                return CreateFallbackRouteOptimization(bookings, availablePersonnel);
            }
        }
    }
}
```

### 5.2 Business Intelligence Dashboard

```typescript
// BI Dashboard Components
export const BusinessIntelligenceDashboard: React.FC = () => {
  const [timeRange, setTimeRange] = useState<TimeRange>('last30days');
  const [serviceMetrics, setServiceMetrics] = useState<ServiceMetrics | null>(null);
  const [revenueData, setRevenueData] = useState<RevenueData | null>(null);
  const [kpis, setKpis] = useState<KPIData | null>(null);

  useEffect(() => {
    loadDashboardData();
  }, [timeRange]);

  const loadDashboardData = async () => {
    try {
      const [metrics, revenue, kpiData] = await Promise.all([
        api.getServiceMetrics(timeRange),
        api.getRevenueAnalytics(timeRange),
        api.getKPIs(timeRange)
      ]);

      setServiceMetrics(metrics);
      setRevenueData(revenue);
      setKpis(kpiData);
    } catch (error) {
      toast.error('Failed to load dashboard data');
    }
  };

  return (
    <div className="bi-dashboard">
      <DashboardHeader>
        <h1>Business Intelligence Dashboard</h1>
        <TimeRangeSelector value={timeRange} onChange={setTimeRange} />
      </DashboardHeader>

      <div className="kpi-grid">
        <KPICard
          title="Total Revenue"
          value={kpis?.totalRevenue}
          format="currency"
          trend={kpis?.revenueTrend}
        />
        <KPICard
          title="Active Services"
          value={kpis?.activeServices}
          format="number"
          trend={kpis?.servicesTrend}
        />
        <KPICard
          title="Customer Satisfaction"
          value={kpis?.averageRating}
          format="rating"
          trend={kpis?.ratingTrend}
        />
        <KPICard
          title="Fleet Utilization"
          value={kpis?.fleetUtilization}
          format="percentage"
          trend={kpis?.utilizationTrend}
        />
      </div>

      <div className="charts-grid">
        <RevenueChart data={revenueData?.dailyRevenue} />
        <ServiceVolumeChart data={serviceMetrics?.dailyVolume} />
        <CustomerSegmentationChart data={serviceMetrics?.customerSegments} />
        <GeographicHeatmap data={serviceMetrics?.geographicData} />
      </div>

      <div className="insights-section">
        <PredictiveInsights data={serviceMetrics?.predictions} />
        <RecommendationEngine recommendations={kpis?.recommendations} />
      </div>
    </div>
  );
};

// Revenue Analytics Component
export const RevenueAnalytics: React.FC = () => {
  const [revenueBreakdown, setRevenueBreakdown] = useState<RevenueBreakdown | null>(null);
  const [profitAnalysis, setProfitAnalysis] = useState<ProfitAnalysis | null>(null);

  const exportReport = async (format: 'pdf' | 'excel') => {
    try {
      const reportData = await api.generateRevenueReport(format);
      downloadFile(reportData, `revenue-report.${format}`);
    } catch (error) {
      toast.error('Failed to generate report');
    }
  };

  return (
    <div className="revenue-analytics">
      <div className="revenue-overview">
        <TotalRevenueCard revenue={revenueBreakdown?.total} />
        <ServiceTypeRevenue breakdown={revenueBreakdown?.byServiceType} />
        <MonthlyTrend data={revenueBreakdown?.monthlyTrend} />
      </div>

      <div className="profit-analysis">
        <GrossMarginChart data={profitAnalysis?.margins} />
        <CostBreakdown costs={profitAnalysis?.costs} />
        <ProfitabilityTrends trends={profitAnalysis?.trends} />
      </div>

      <div className="actions">
        <Button onClick={() => exportReport('pdf')}>Export PDF Report</Button>
        <Button onClick={() => exportReport('excel')}>Export Excel Report</Button>
      </div>
    </div>
  );
};

// Performance Analytics
export const PerformanceAnalytics: React.FC = () => {
  const [personnelMetrics, setPersonnelMetrics] = useState<PersonnelMetrics[]>([]);
  const [vehicleUtilization, setVehicleUtilization] = useState<VehicleUtilization[]>([]);
  const [serviceQuality, setServiceQuality] = useState<ServiceQualityMetrics | null>(null);

  return (
    <div className="performance-analytics">
      <PersonnelPerformanceTable data={personnelMetrics} />
      <VehicleUtilizationChart data={vehicleUtilization} />
      <ServiceQualityDashboard metrics={serviceQuality} />
      
      <div className="improvement-recommendations">
        <h3>Improvement Recommendations</h3>
        <RecommendationsList 
          recommendations={serviceQuality?.recommendations}
          onImplement={implementRecommendation}
        />
      </div>
    </div>
  );
};
```

---

## 6. Integration Points

### 6.1 Core Module Integrations

#### Users & Permissions Integration
```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.Integrations
{
    public interface IUserManagementIntegration
    {
        Task<UserProfile> GetUserProfileAsync(string userId);
        Task<List<Permission>> GetUserPermissionsAsync(string userId);
        Task CreatePersonnelUserAsync(Personnel personnel, string defaultPassword);
        Task UpdateUserRolesAsync(string userId, List<string> roles);
        Task DeactivateUserAsync(string userId);
    }

    public class UserManagementIntegration : IUserManagementIntegration
    {
        private readonly IUserService _userService;
        private readonly IPermissionService _permissionService;

        public async Task CreatePersonnelUserAsync(Personnel personnel, string defaultPassword)
        {
            var userRequest = new CreateUserRequest
            {
                Email = personnel.ContactInfo.Email,
                FirstName = personnel.PersonalInfo.FirstName,
                LastName = personnel.PersonalInfo.LastName,
                Phone = personnel.ContactInfo.Phone,
                TenantId = personnel.TenantId.Value,
                Roles = GetDefaultRoles(personnel.Type),
                Password = defaultPassword,
                RequirePasswordChange = true
            };

            await _userService.CreateUserAsync(userRequest);
        }

        private List<string> GetDefaultRoles(PersonnelType type)
        {
            return type switch
            {
                PersonnelType.VTCDriver => new[] { "Driver", "Personnel" }.ToList(),
                PersonnelType.SecurityGuard => new[] { "SecurityGuard", "Personnel" }.ToList(),
                PersonnelType.Courier => new[] { "Courier", "Personnel" }.ToList(),
                PersonnelType.Dispatcher => new[] { "Dispatcher", "Personnel", "OperationsViewer" }.ToList(),
                PersonnelType.Supervisor => new[] { "Supervisor", "Personnel", "OperationsManager" }.ToList(),
                _ => new[] { "Personnel" }.ToList()
            };
        }
    }
}
```

#### Vehicles Integration
```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.Integrations
{
    public interface IVehicleManagementIntegration
    {
        Task<Vehicle> GetVehicleDetailsAsync(Guid vehicleId);
        Task<List<Vehicle>> GetAvailableVehiclesAsync(ServiceType serviceType);
        Task AssignVehicleToPersonnelAsync(Guid vehicleId, Guid personnelId);
        Task UpdateVehicleLocationAsync(Guid vehicleId, Location location);
        Task<VehicleMaintenanceStatus> GetMaintenanceStatusAsync(Guid vehicleId);
    }

    public class VehicleManagementIntegration : IVehicleManagementIntegration
    {
        private readonly IVehicleService _vehicleService;
        private readonly IMaintenanceService _maintenanceService;

        public async Task<List<Vehicle>> GetAvailableVehiclesAsync(ServiceType serviceType)
        {
            var vehicleTypes = GetRequiredVehicleTypes(serviceType);
            return await _vehicleService.GetAvailableVehiclesByTypesAsync(vehicleTypes);
        }

        public async Task AssignVehicleToPersonnelAsync(Guid vehicleId, Guid personnelId)
        {
            await _vehicleService.AssignVehicleAsync(vehicleId, personnelId);
            
            // Update vehicle status to "In Service"
            await _vehicleService.UpdateVehicleStatusAsync(vehicleId, VehicleStatus.InService);
        }

        private List<VehicleType> GetRequiredVehicleTypes(ServiceType serviceType)
        {
            return serviceType switch
            {
                ServiceType.VTCRide => new[] { VehicleType.Car, VehicleType.Luxury, VehicleType.Van }.ToList(),
                ServiceType.SecurityPatrol => new[] { VehicleType.Car, VehicleType.SecurityVehicle }.ToList(),
                ServiceType.CourierDelivery => new[] { VehicleType.Car, VehicleType.Van, VehicleType.Motorcycle }.ToList(),
                ServiceType.EmergencyResponse => new[] { VehicleType.Car, VehicleType.EmergencyVehicle }.ToList(),
                _ => new[] { VehicleType.Car }.ToList()
            };
        }
    }
}
```

#### Files Integration
```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.Integrations
{
    public interface IFileManagementIntegration
    {
        Task<FileUploadResult> UploadCertificationDocumentAsync(Guid personnelId, 
            CertificationType type, IFormFile file);
        Task<FileUploadResult> UploadIncidentPhotoAsync(Guid incidentId, IFormFile photo);
        Task<Stream> DownloadDocumentAsync(Guid fileId);
        Task<List<FileMetadata>> GetPersonnelDocumentsAsync(Guid personnelId);
        Task<List<FileMetadata>> GetIncidentAttachmentsAsync(Guid incidentId);
    }

    public class FileManagementIntegration : IFileManagementIntegration
    {
        private readonly IFileService _fileService;

        public async Task<FileUploadResult> UploadCertificationDocumentAsync(Guid personnelId, 
            CertificationType type, IFormFile file)
        {
            var uploadRequest = new UploadFileRequest
            {
                File = file,
                EntityType = "Personnel",
                EntityId = personnelId.ToString(),
                Category = "Certification",
                Subcategory = type.ToString(),
                Tags = new[] { "certification", type.ToString().ToLower() },
                IsPublic = false,
                RequiresApproval = true
            };

            return await _fileService.UploadFileAsync(uploadRequest);
        }

        public async Task<FileUploadResult> UploadIncidentPhotoAsync(Guid incidentId, IFormFile photo)
        {
            var uploadRequest = new UploadFileRequest
            {
                File = photo,
                EntityType = "IncidentReport",
                EntityId = incidentId.ToString(),
                Category = "Evidence",
                Subcategory = "Photo",
                Tags = new[] { "incident", "evidence", "photo" },
                IsPublic = false,
                RequiresApproval = false
            };

            return await _fileService.UploadFileAsync(uploadRequest);
        }
    }
}
```

#### Notifications Integration
```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.Integrations
{
    public interface INotificationIntegration
    {
        Task SendBookingConfirmationAsync(ServiceBooking booking, Customer customer);
        Task SendServiceStartedNotificationAsync(ServiceBooking booking);
        Task SendEmergencyAlertAsync(EmergencyAlert alert);
        Task SendCertificationExpiryWarningAsync(Personnel personnel, Certification certification);
        Task SendPerformanceReportAsync(Personnel personnel, PerformanceReport report);
    }

    public class NotificationIntegration : INotificationIntegration
    {
        private readonly INotificationService _notificationService;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly ISMSService _smsService;

        public async Task SendBookingConfirmationAsync(ServiceBooking booking, Customer customer)
        {
            var emailNotification = new EmailNotification
            {
                To = customer.ContactInfo.Email,
                Subject = $"Booking Confirmation - {booking.ServiceType}",
                TemplateId = "booking-confirmation",
                TemplateData = new
                {
                    CustomerName = $"{customer.PersonalInfo.FirstName} {customer.PersonalInfo.LastName}",
                    ServiceType = booking.ServiceType.ToString(),
                    ServiceDate = booking.ServiceDate.ToString("yyyy-MM-dd HH:mm"),
                    PickupLocation = booking.PickupLocation.Address,
                    BookingId = booking.Id.Value,
                    TotalPrice = booking.TotalPrice
                }
            };

            await _notificationService.SendEmailAsync(emailNotification);

            // Send SMS if customer has opted in
            if (customer.Preferences.ReceiveSMSNotifications)
            {
                var smsMessage = $"Booking confirmed for {booking.ServiceDate:MMM dd, HH:mm}. " +
                                $"Pickup: {booking.PickupLocation.Address}. " +
                                $"Booking ID: {booking.Id.Value}";

                await _smsService.SendSMSAsync(customer.ContactInfo.Phone, smsMessage);
            }
        }

        public async Task SendEmergencyAlertAsync(EmergencyAlert alert)
        {
            // Send to dispatch center
            var dispatchNotification = new PushNotification
            {
                Title = "🚨 EMERGENCY ALERT",
                Body = $"{alert.AlertType}: {alert.Description}",
                Data = new { AlertId = alert.Id.Value, Location = alert.Location },
                Recipients = await GetDispatchCenterRecipientsAsync(alert.DispatchCenterId)
            };

            await _notificationService.SendPushNotificationAsync(dispatchNotification);

            // Send SMS to emergency contacts
            if (alert.ReportedBy != null)
            {
                var personnel = await GetPersonnelAsync(alert.ReportedBy.Value);
                var emergencyMessage = $"Emergency alert for {personnel.PersonalInfo.FirstName} " +
                                     $"{personnel.PersonalInfo.LastName} at {alert.Location.Address}. " +
                                     $"Time: {alert.CreatedAt:HH:mm}";

                await _smsService.SendSMSAsync(personnel.EmergencyContact.Phone, emergencyMessage);
            }
        }
    }
}
```

### 6.2 External Service Integrations

#### GPS and Mapping Services
```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.ExternalServices
{
    public interface IGPSTrackingService
    {
        Task<RouteInfo> CalculateRouteAsync(Location origin, Location destination);
        Task<TimeSpan> EstimateArrivalTimeAsync(Location origin, Location destination, DateTime departureTime);
        Task<List<Location>> GetOptimizedWaypointsAsync(List<Location> waypoints);
        Task<TrafficInfo> GetTrafficInfoAsync(Location location, int radiusKm);
    }

    public class GPSTrackingService : IGPSTrackingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public async Task<RouteInfo> CalculateRouteAsync(Location origin, Location destination)
        {
            var client = _httpClientFactory.CreateClient("MapsAPI");
            var apiKey = _configuration["ExternalServices:GoogleMaps:ApiKey"];

            var request = $"directions/json?" +
                         $"origin={origin.Latitude},{origin.Longitude}&" +
                         $"destination={destination.Latitude},{destination.Longitude}&" +
                         $"key={apiKey}&" +
                         $"traffic_model=best_guess&" +
                         $"departure_time=now";

            var response = await client.GetAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            var directionsResponse = JsonSerializer.Deserialize<GoogleDirectionsResponse>(content);

            return new RouteInfo
            {
                Distance = directionsResponse.Routes.First().Legs.Sum(l => l.Distance.Value),
                Duration = TimeSpan.FromSeconds(directionsResponse.Routes.First().Legs.Sum(l => l.Duration.Value)),
                Polyline = directionsResponse.Routes.First().OverviewPolyline.Points,
                Steps = directionsResponse.Routes.First().Legs.SelectMany(l => l.Steps)
                    .Select(s => new RouteStep
                    {
                        Instruction = s.HtmlInstructions,
                        Distance = s.Distance.Value,
                        Duration = TimeSpan.FromSeconds(s.Duration.Value),
                        StartLocation = new Location(s.StartLocation.Lat, s.StartLocation.Lng),
                        EndLocation = new Location(s.EndLocation.Lat, s.EndLocation.Lng)
                    }).ToList()
            };
        }
    }

    public interface IGeofencingService
    {
        Task<bool> IsLocationWithinGeofenceAsync(Location location, Geofence geofence);
        Task<List<GeofenceAlert>> CheckGeofenceViolationsAsync(Guid personnelId, Location location);
        Task CreateServiceAreaGeofenceAsync(Guid serviceProviderId, ServiceArea serviceArea);
    }

    public class GeofencingService : IGeofencingService
    {
        public async Task<bool> IsLocationWithinGeofenceAsync(Location location, Geofence geofence)
        {
            // Point-in-polygon algorithm for complex geofences
            if (geofence.Type == GeofenceType.Polygon)
            {
                return IsPointInPolygon(location, geofence.BoundaryPoints);
            }
            
            // Simple radius check for circular geofences
            if (geofence.Type == GeofenceType.Circle)
            {
                var distance = location.DistanceToKm(geofence.Center);
                return distance <= geofence.RadiusKm;
            }

            return false;
        }

        private bool IsPointInPolygon(Location point, List<Location> polygon)
        {
            bool inside = false;
            int j = polygon.Count - 1;

            for (int i = 0; i < polygon.Count; i++)
            {
                if (((polygon[i].Latitude > point.Latitude) != (polygon[j].Latitude > point.Latitude)) &&
                    (point.Longitude < (polygon[j].Longitude - polygon[i].Longitude) * 
                     (point.Latitude - polygon[i].Latitude) / (polygon[j].Latitude - polygon[i].Latitude) + 
                     polygon[i].Longitude))
                {
                    inside = !inside;
                }
                j = i;
            }

            return inside;
        }
    }
}
```

#### Payment Processing Integration
```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.ExternalServices
{
    public interface IPaymentProcessingService
    {
        Task<PaymentAuthorizationResult> AuthorizePaymentAsync(PaymentRequest request);
        Task<PaymentCaptureResult> CapturePaymentAsync(string authorizationId, decimal amount);
        Task<RefundResult> RefundPaymentAsync(string paymentId, decimal amount, string reason);
        Task<PaymentMethod> SavePaymentMethodAsync(SavePaymentMethodRequest request);
        Task<List<PaymentMethod>> GetCustomerPaymentMethodsAsync(Guid customerId);
    }

    public class StripePaymentService : IPaymentProcessingService
    {
        private readonly StripeClient _stripeClient;
        private readonly IConfiguration _configuration;

        public async Task<PaymentAuthorizationResult> AuthorizePaymentAsync(PaymentRequest request)
        {
            try
            {
                var paymentIntentOptions = new PaymentIntentCreateOptions
                {
                    Amount = (long)(request.Amount * 100), // Convert to cents
                    Currency = request.Currency.ToLower(),
                    PaymentMethod = request.PaymentMethodId,
                    CaptureMethod = "manual", // Authorize only, capture later
                    Description = $"Service booking {request.BookingId}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["booking_id"] = request.BookingId.ToString(),
                        ["customer_id"] = request.CustomerId.ToString(),
                        ["service_type"] = request.ServiceType.ToString()
                    }
                };

                var service = new PaymentIntentService(_stripeClient);
                var paymentIntent = await service.CreateAsync(paymentIntentOptions);

                return new PaymentAuthorizationResult
                {
                    IsSuccess = paymentIntent.Status == "requires_capture",
                    AuthorizationId = paymentIntent.Id,
                    Status = paymentIntent.Status,
                    Amount = request.Amount,
                    TransactionId = paymentIntent.Id
                };
            }
            catch (StripeException ex)
            {
                return new PaymentAuthorizationResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ErrorCode = ex.StripeError?.Code
                };
            }
        }

        public async Task<PaymentCaptureResult> CapturePaymentAsync(string authorizationId, decimal amount)
        {
            try
            {
                var options = new PaymentIntentCaptureOptions
                {
                    AmountToCapture = (long)(amount * 100)
                };

                var service = new PaymentIntentService(_stripeClient);
                var paymentIntent = await service.CaptureAsync(authorizationId, options);

                return new PaymentCaptureResult
                {
                    IsSuccess = paymentIntent.Status == "succeeded",
                    TransactionId = paymentIntent.Id,
                    Amount = amount,
                    ProcessingFee = CalculateProcessingFee(amount)
                };
            }
            catch (StripeException ex)
            {
                return new PaymentCaptureResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
```

#### Background Check and Verification Services
```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.ExternalServices
{
    public interface IBackgroundCheckService
    {
        Task<BackgroundCheckResult> InitiateBackgroundCheckAsync(Personnel personnel);
        Task<BackgroundCheckResult> GetBackgroundCheckResultAsync(string checkId);
        Task<LicenseVerificationResult> VerifyLicenseAsync(string licenseNumber, LicenseType type, string jurisdiction);
        Task<List<BackgroundCheckResult>> GetPendingChecksAsync();
    }

    public class BackgroundCheckService : IBackgroundCheckService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public async Task<BackgroundCheckResult> InitiateBackgroundCheckAsync(Personnel personnel)
        {
            var client = _httpClientFactory.CreateClient("BackgroundCheckAPI");
            var apiKey = _configuration["ExternalServices:BackgroundCheck:ApiKey"];

            var request = new BackgroundCheckRequest
            {
                FirstName = personnel.PersonalInfo.FirstName,
                LastName = personnel.PersonalInfo.LastName,
                DateOfBirth = personnel.PersonalInfo.DateOfBirth,
                NationalId = personnel.PersonalInfo.NationalId,
                Address = personnel.Address,
                CheckTypes = GetRequiredCheckTypes(personnel.Type)
            };

            var response = await client.PostAsJsonAsync("/api/background-checks", request);
            var result = await response.Content.ReadFromJsonAsync<BackgroundCheckApiResponse>();

            return new BackgroundCheckResult
            {
                CheckId = result.CheckId,
                Status = BackgroundCheckStatus.Pending,
                InitiatedAt = DateTime.UtcNow,
                PersonnelId = personnel.Id.Value,
                EstimatedCompletionDate = DateTime.UtcNow.AddBusinessDays(5)
            };
        }

        private List<BackgroundCheckType> GetRequiredCheckTypes(PersonnelType personnelType)
        {
            var baseChecks = new List<BackgroundCheckType>
            {
                BackgroundCheckType.CriminalHistory,
                BackgroundCheckType.IdentityVerification
            };

            return personnelType switch
            {
                PersonnelType.VTCDriver => baseChecks.Concat(new[]
                {
                    BackgroundCheckType.DrivingRecord,
                    BackgroundCheckType.VehicleLicenseVerification
                }).ToList(),
                
                PersonnelType.SecurityGuard => baseChecks.Concat(new[]
                {
                    BackgroundCheckType.SecurityLicenseVerification,
                    BackgroundCheckType.PsychologicalEvaluation
                }).ToList(),
                
                PersonnelType.Courier => baseChecks.Concat(new[]
                {
                    BackgroundCheckType.DrivingRecord
                }).ToList(),
                
                _ => baseChecks
            };
        }
    }
}
```

---

## 7. Performance Considerations and Optimizations

### 7.1 Scalability Architecture

```csharp
namespace DriveOps.GardiennageVTC.Infrastructure.Performance
{
    // Caching Strategy
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        Task RemovePatternAsync(string pattern);
    }

    public class RedisCacheService : ICacheService
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisCacheService> _logger;

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var serialized = JsonSerializer.Serialize(value);
                await _database.StringSetAsync(key, serialized, expiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache key {Key}", key);
            }
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var value = await _database.StringGetAsync(key);
                return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache key {Key}", key);
                return null;
            }
        }
    }

    // Database Performance Optimizations
    public class OptimizedServiceBookingRepository : IServiceBookingRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ICacheService _cache;

        public async Task<PagedResult<ServiceBooking>> GetActiveBookingsAsync(
            string tenantId, int page, int pageSize)
        {
            var cacheKey = $"active_bookings:{tenantId}:{page}:{pageSize}";
            var cached = await _cache.GetAsync<PagedResult<ServiceBooking>>(cacheKey);
            
            if (cached != null)
                return cached;

            var query = _context.ServiceBookings
                .Where(b => b.TenantId == Guid.Parse(tenantId))
                .Where(b => b.Status == BookingStatus.InProgress || b.Status == BookingStatus.Assigned)
                .Include(b => b.Customer)
                .Include(b => b.AssignedPersonnel)
                .Include(b => b.AssignedVehicle)
                .OrderBy(b => b.ServiceDate);

            var totalCount = await query.CountAsync();
            var bookings = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new PagedResult<ServiceBooking>
            {
                Items = bookings,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            // Cache for 30 seconds due to real-time nature
            await _cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(30));
            
            return result;
        }

        public async Task<List<ServiceBooking>> GetNearbyBookingsAsync(Location location, int radiusKm)
        {
            // Use spatial query for efficient location-based searches
            var result = await _context.ServiceBookings
                .Where(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                .Where(b => 
                    (6371 * Math.Acos(
                        Math.Cos(Math.PI * location.Latitude / 180) *
                        Math.Cos(Math.PI * b.PickupLatitude / 180) *
                        Math.Cos(Math.PI * b.PickupLongitude / 180 - Math.PI * location.Longitude / 180) +
                        Math.Sin(Math.PI * location.Latitude / 180) *
                        Math.Sin(Math.PI * b.PickupLatitude / 180)
                    )) <= radiusKm)
                .OrderBy(b => b.ServiceDate)
                .ToListAsync();

            return result;
        }
    }

    // Real-time Performance Optimization
    public class OptimizedTrackingService : IRealtimeTrackingService
    {
        private readonly IHubContext<TrackingHub> _hubContext;
        private readonly IMemoryCache _memoryCache;
        private readonly IDatabase _redisDatabase;

        public async Task UpdateLocationAsync(Guid personnelId, Location location, DateTime timestamp)
        {
            // Use memory cache for immediate access
            var cacheKey = $"location:{personnelId}";
            _memoryCache.Set(cacheKey, location, TimeSpan.FromMinutes(5));

            // Batch location updates to reduce database load
            var batchKey = $"location_batch:{personnelId}";
            var existingBatch = await _redisDatabase.ListRangeAsync(batchKey);
            
            var locationUpdate = JsonSerializer.Serialize(new { location, timestamp });
            await _redisDatabase.ListRightPushAsync(batchKey, locationUpdate);
            await _redisDatabase.ExpireAsync(batchKey, TimeSpan.FromMinutes(1));

            // Process batch when it reaches threshold or timeout
            if (existingBatch.Length >= 10) // Batch size threshold
            {
                await ProcessLocationBatch(personnelId);
            }

            // Send real-time updates (throttled)
            var lastUpdateKey = $"last_update:{personnelId}";
            var lastUpdate = _memoryCache.Get<DateTime?>(lastUpdateKey);
            
            if (!lastUpdate.HasValue || DateTime.UtcNow - lastUpdate.Value > TimeSpan.FromSeconds(10))
            {
                await SendLocationUpdate(personnelId, location, timestamp);
                _memoryCache.Set(lastUpdateKey, DateTime.UtcNow, TimeSpan.FromSeconds(10));
            }
        }

        private async Task ProcessLocationBatch(Guid personnelId)
        {
            var batchKey = $"location_batch:{personnelId}";
            var batch = await _redisDatabase.ListRangeAsync(batchKey);
            
            if (batch.Length > 0)
            {
                var locations = batch.Select(b => JsonSerializer.Deserialize<dynamic>(b!)).ToList();
                
                // Bulk insert to database
                await BulkInsertLocationUpdates(personnelId, locations);
                
                // Clear batch
                await _redisDatabase.DeleteAsync(batchKey);
            }
        }
    }

    // Load Balancing and Circuit Breaker
    public class CircuitBreakerService
    {
        private readonly Dictionary<string, CircuitBreakerState> _circuitBreakers = new();
        private readonly ILogger<CircuitBreakerService> _logger;

        public async Task<T> ExecuteAsync<T>(string operationName, Func<Task<T>> operation, 
            int failureThreshold = 5, TimeSpan resetTimeout = default)
        {
            if (resetTimeout == default)
                resetTimeout = TimeSpan.FromMinutes(1);

            var state = GetOrCreateCircuitBreakerState(operationName);

            if (state.State == CircuitState.Open)
            {
                if (DateTime.UtcNow - state.LastFailureTime > resetTimeout)
                {
                    state.State = CircuitState.HalfOpen;
                    _logger.LogInformation("Circuit breaker {Operation} moved to Half-Open state", operationName);
                }
                else
                {
                    throw new CircuitBreakerOpenException($"Circuit breaker {operationName} is open");
                }
            }

            try
            {
                var result = await operation();
                
                if (state.State == CircuitState.HalfOpen)
                {
                    state.State = CircuitState.Closed;
                    state.FailureCount = 0;
                    _logger.LogInformation("Circuit breaker {Operation} moved to Closed state", operationName);
                }

                return result;
            }
            catch (Exception ex)
            {
                state.FailureCount++;
                state.LastFailureTime = DateTime.UtcNow;

                if (state.FailureCount >= failureThreshold)
                {
                    state.State = CircuitState.Open;
                    _logger.LogWarning("Circuit breaker {Operation} opened due to {FailureCount} failures", 
                        operationName, state.FailureCount);
                }

                throw;
            }
        }

        private CircuitBreakerState GetOrCreateCircuitBreakerState(string operationName)
        {
            if (!_circuitBreakers.ContainsKey(operationName))
            {
                _circuitBreakers[operationName] = new CircuitBreakerState
                {
                    State = CircuitState.Closed,
                    FailureCount = 0
                };
            }

            return _circuitBreakers[operationName];
        }
    }
}
```

### 7.2 Mobile App Performance Optimizations

```typescript
// Battery Optimization for Location Tracking
export class BatteryOptimizedLocationService {
  private locationUpdateInterval: number = 30000; // 30 seconds default
  private isMoving: boolean = false;
  private lastKnownLocation: Location | null = null;
  private movementThreshold: number = 50; // meters

  async startOptimizedTracking(personnelId: string): Promise<void> {
    // Dynamic interval based on movement
    const subscription = await Location.watchPositionAsync(
      {
        accuracy: Location.Accuracy.Balanced,
        timeInterval: this.locationUpdateInterval,
        distanceInterval: this.movementThreshold,
      },
      (location) => this.handleOptimizedLocationUpdate(location, personnelId)
    );

    // Monitor device motion to adjust tracking frequency
    this.startMotionDetection();
  }

  private async handleOptimizedLocationUpdate(location: Location.LocationObject, personnelId: string) {
    const currentLocation = {
      latitude: location.coords.latitude,
      longitude: location.coords.longitude,
      timestamp: location.timestamp
    };

    // Check if device is moving
    if (this.lastKnownLocation) {
      const distance = this.calculateDistance(this.lastKnownLocation, currentLocation);
      this.isMoving = distance > this.movementThreshold;
    }

    // Adjust tracking frequency based on movement
    this.adjustTrackingFrequency();

    // Batch updates when not moving
    if (this.isMoving) {
      await this.sendLocationUpdate(currentLocation, personnelId);
    } else {
      await this.batchLocationUpdate(currentLocation, personnelId);
    }

    this.lastKnownLocation = currentLocation;
  }

  private adjustTrackingFrequency(): void {
    if (this.isMoving) {
      this.locationUpdateInterval = 10000; // 10 seconds when moving
    } else {
      this.locationUpdateInterval = 60000; // 1 minute when stationary
    }
  }

  private async batchLocationUpdate(location: any, personnelId: string): Promise<void> {
    // Store in local storage and send in batches
    const batchKey = `location_batch_${personnelId}`;
    let batch = await AsyncStorage.getItem(batchKey);
    let locationBatch = batch ? JSON.parse(batch) : [];

    locationBatch.push(location);

    // Send batch when it reaches threshold or timeout
    if (locationBatch.length >= 5 || this.shouldSendBatch(locationBatch)) {
      await this.sendLocationBatch(locationBatch, personnelId);
      await AsyncStorage.removeItem(batchKey);
    } else {
      await AsyncStorage.setItem(batchKey, JSON.stringify(locationBatch));
    }
  }
}

// Offline Capability
export class OfflineDataManager {
  private readonly OFFLINE_QUEUE_KEY = 'offline_queue';
  private readonly MAX_QUEUE_SIZE = 1000;

  async queueOfflineAction(action: OfflineAction): Promise<void> {
    try {
      const queue = await this.getOfflineQueue();
      
      if (queue.length >= this.MAX_QUEUE_SIZE) {
        // Remove oldest actions to prevent memory issues
        queue.splice(0, 100);
      }

      queue.push({
        ...action,
        timestamp: Date.now(),
        id: generateUniqueId()
      });

      await AsyncStorage.setItem(this.OFFLINE_QUEUE_KEY, JSON.stringify(queue));
    } catch (error) {
      console.error('Error queuing offline action:', error);
    }
  }

  async processOfflineQueue(): Promise<void> {
    const queue = await this.getOfflineQueue();
    
    if (queue.length === 0) return;

    const processedActions: string[] = [];

    for (const action of queue) {
      try {
        await this.executeOfflineAction(action);
        processedActions.push(action.id);
      } catch (error) {
        console.error('Error processing offline action:', error);
        
        // Remove actions older than 24 hours to prevent infinite retries
        if (Date.now() - action.timestamp > 24 * 60 * 60 * 1000) {
          processedActions.push(action.id);
        }
      }
    }

    // Remove processed actions from queue
    const remainingQueue = queue.filter(action => !processedActions.includes(action.id));
    await AsyncStorage.setItem(this.OFFLINE_QUEUE_KEY, JSON.stringify(remainingQueue));
  }

  private async executeOfflineAction(action: OfflineAction): Promise<void> {
    switch (action.type) {
      case 'LOCATION_UPDATE':
        await api.updateLocation(action.data);
        break;
      case 'STATUS_UPDATE':
        await api.updateStatus(action.data);
        break;
      case 'COMPLETE_SERVICE':
        await api.completeService(action.data);
        break;
      // Add more action types as needed
    }
  }
}

// Data Compression for API Calls
export class DataCompressionService {
  async compressLocationData(locations: LocationUpdate[]): Promise<string> {
    // Use differential compression for location data
    if (locations.length === 0) return '';

    const baseLocation = locations[0];
    const compressed = {
      base: {
        lat: baseLocation.latitude,
        lng: baseLocation.longitude,
        time: baseLocation.timestamp
      },
      deltas: locations.slice(1).map(loc => ({
        dlat: (loc.latitude - baseLocation.latitude) * 1000000, // Micro-degrees
        dlng: (loc.longitude - baseLocation.longitude) * 1000000,
        dt: loc.timestamp - baseLocation.timestamp
      }))
    };

    return JSON.stringify(compressed);
  }

  async decompressLocationData(compressedData: string): Promise<LocationUpdate[]> {
    const data = JSON.parse(compressedData);
    const locations: LocationUpdate[] = [{
      latitude: data.base.lat,
      longitude: data.base.lng,
      timestamp: data.base.time
    }];

    for (const delta of data.deltas) {
      const prevLocation = locations[locations.length - 1];
      locations.push({
        latitude: prevLocation.latitude + (delta.dlat / 1000000),
        longitude: prevLocation.longitude + (delta.dlng / 1000000),
        timestamp: prevLocation.timestamp + delta.dt
      });
    }

    return locations;
  }
}
```

---

## 8. Conclusion

The GARDIENNAGE/VTC commercial module provides a comprehensive solution for managing multi-service operations in the personal transport and security industry. This module successfully integrates:

### Key Achievements

1. **Unified Multi-Service Platform**: A single system managing VTC transport, security/guard services, and courier/delivery operations
2. **Real-Time Operations**: Advanced GPS tracking, live dispatch coordination, and instant emergency response capabilities
3. **Scalable Architecture**: Domain-driven design with CQRS patterns supporting growth from individual operators to large dispatch centers
4. **Safety-First Design**: Comprehensive emergency response system with panic buttons, automatic escalation, and external service integration
5. **Performance Optimization**: Battery-efficient mobile apps, intelligent caching, and circuit breaker patterns for reliability
6. **Compliance Ready**: Built-in certification tracking, background check integration, and regulatory reporting capabilities

### Business Impact

- **Revenue Optimization**: AI-powered pricing algorithms and demand forecasting
- **Operational Efficiency**: Smart dispatch algorithms reducing response times by up to 30%
- **Safety Enhancement**: Emergency response system with sub-minute notification times
- **Customer Satisfaction**: Real-time tracking and communication improving service transparency
- **Regulatory Compliance**: Automated certification monitoring and audit trail capabilities

### Technical Excellence

- **Microservices Architecture**: Loosely coupled services enabling independent scaling and deployment
- **Event-Driven Design**: Asynchronous processing ensuring system responsiveness
- **Multi-Tenant Isolation**: Secure tenant separation with dedicated resources per customer
- **Mobile-First Approach**: Native mobile applications optimized for real-world field usage
- **Integration-Ready**: Seamless integration with core DriveOps modules and external services

### Future Extensibility

The module architecture supports future enhancements including:
- IoT integration for vehicle telematics
- AI-powered route optimization
- Blockchain-based verification systems
- Advanced analytics and machine learning capabilities
- International expansion with localization support

This comprehensive documentation serves as the technical foundation for implementing a world-class GARDIENNAGE/VTC commercial module that meets the demanding requirements of modern service providers while maintaining the security, performance, and scalability standards expected in enterprise SaaS platforms.

---

*Document created: 2024-12-19*  
*Last updated: 2024-12-19*  
*Module version: 1.0.0*
