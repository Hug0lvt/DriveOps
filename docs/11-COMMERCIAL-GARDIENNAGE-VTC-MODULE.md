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
