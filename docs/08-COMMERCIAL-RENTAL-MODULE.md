# DriveOps - Commercial RENTAL Module Documentation

## Overview

The RENTAL module is a comprehensive premium business module (€59/month) that provides complete vehicle rental and sharing management capabilities for car rental companies and peer-to-peer vehicle sharing platforms. This module transforms DriveOps instances into full-featured rental management systems with real-time booking, dynamic pricing, fleet optimization, and digital handover processes.

### Module Scope

The RENTAL module delivers enterprise-grade rental operations including:

- **Fleet Management**: Real-time vehicle availability tracking and optimization
- **Booking Engine**: Online reservations with calendar integration and instant confirmations
- **Dynamic Pricing**: Demand-based pricing, seasonal rates, and promotional campaigns
- **Digital Handover**: Mobile-first vehicle inspection and key exchange processes
- **Damage Management**: Photo-based assessment and automated insurance claims
- **Customer Portal**: Self-service booking, loyalty programs, and account management
- **Revenue Analytics**: Utilization optimization and profit margin analysis

---

## 1. Business Domain Model

### 1.1 Core Rental Entities

#### Reservation Aggregate
```csharp
namespace DriveOps.Rental.Domain.Aggregates
{
    public class Reservation : AggregateRoot
    {
        public ReservationId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public RentalCustomerId CustomerId { get; private set; }
        public RentalVehicleId VehicleId { get; private set; }
        public VehicleCategoryId CategoryId { get; private set; }
        public ReservationNumber Number { get; private set; }
        
        // Booking details
        public DateTime PickupDateTime { get; private set; }
        public DateTime ReturnDateTime { get; private set; }
        public RentalLocationId PickupLocationId { get; private set; }
        public RentalLocationId ReturnLocationId { get; private set; }
        
        // Pricing
        public PricingCalculation Pricing { get; private set; }
        public TariffPlanId TariffPlanId { get; private set; }
        public List<PromoCodeId> AppliedPromoCodes { get; private set; }
        
        // Status management
        public ReservationStatus Status { get; private set; }
        public DateTime? ConfirmedAt { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public string? CancellationReason { get; private set; }
        
        // Payment
        public PaymentStatus PaymentStatus { get; private set; }
        public decimal SecurityDeposit { get; private set; }
        public PaymentMethodId PaymentMethodId { get; private set; }
        
        // Audit
        public UserId CreatedBy { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private readonly List<ReservationEvent> _events = new();
        public IReadOnlyCollection<ReservationEvent> Events => _events.AsReadOnly();

        public static Reservation Create(
            TenantId tenantId,
            RentalCustomerId customerId,
            VehicleCategoryId categoryId,
            DateTime pickupDateTime,
            DateTime returnDateTime,
            RentalLocationId pickupLocationId,
            RentalLocationId returnLocationId,
            TariffPlanId tariffPlanId,
            UserId createdBy)
        {
            var reservation = new Reservation
            {
                Id = ReservationId.New(),
                TenantId = tenantId,
                CustomerId = customerId,
                CategoryId = categoryId,
                Number = ReservationNumber.Generate(),
                PickupDateTime = pickupDateTime,
                ReturnDateTime = returnDateTime,
                PickupLocationId = pickupLocationId,
                ReturnLocationId = returnLocationId,
                TariffPlanId = tariffPlanId,
                Status = ReservationStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            reservation.AddDomainEvent(new ReservationCreatedEvent(reservation.Id, tenantId, customerId));
            return reservation;
        }

        public Result AssignVehicle(RentalVehicleId vehicleId, UserId assignedBy)
        {
            if (Status != ReservationStatus.Pending)
                return Result.Failure("Can only assign vehicle to pending reservations");

            VehicleId = vehicleId;
            UpdatedAt = DateTime.UtcNow;
            
            AddDomainEvent(new VehicleAssignedToReservationEvent(Id, TenantId, vehicleId));
            return Result.Success();
        }

        public Result Confirm(PricingCalculation finalPricing, UserId confirmedBy)
        {
            if (Status != ReservationStatus.Pending)
                return Result.Failure("Can only confirm pending reservations");

            Status = ReservationStatus.Confirmed;
            Pricing = finalPricing;
            ConfirmedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new ReservationConfirmedEvent(Id, TenantId, CustomerId, PickupDateTime));
            return Result.Success();
        }

        public Result Cancel(string reason, UserId cancelledBy)
        {
            if (Status is ReservationStatus.Completed or ReservationStatus.InProgress)
                return Result.Failure("Cannot cancel completed or in-progress reservations");

            Status = ReservationStatus.Cancelled;
            CancellationReason = reason;
            CancelledAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new ReservationCancelledEvent(Id, TenantId, CustomerId, reason));
            return Result.Success();
        }
    }

    public enum ReservationStatus
#### RentalContract Aggregate
```csharp
namespace DriveOps.Rental.Domain.Aggregates
{
    public class RentalContract : AggregateRoot
    {
        public RentalContractId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public ReservationId ReservationId { get; private set; }
        public RentalCustomerId CustomerId { get; private set; }
        public RentalVehicleId VehicleId { get; private set; }
        public ContractNumber Number { get; private set; }
        
        // Contract terms
        public DateTime EffectiveStartDate { get; private set; }
        public DateTime EffectiveEndDate { get; private set; }
        public DateTime? ActualStartDate { get; private set; }
        public DateTime? ActualEndDate { get; private set; }
        
        // Handover process
        public VehicleHandoverId? PickupHandoverId { get; private set; }
        public VehicleHandoverId? ReturnHandoverId { get; private set; }
        
        // Financial details
        public ContractPricing Pricing { get; private set; }
        public decimal SecurityDepositAmount { get; private set; }
        public SecurityDepositStatus DepositStatus { get; private set; }
        
        // Mileage tracking
        public int StartMileage { get; private set; }
        public int? EndMileage { get; private set; }
        public int IncludedMileageLimit { get; private set; }
        public decimal ExcessMileageRate { get; private set; }
        
        // Contract status
        public ContractStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private readonly List<ContractExtension> _extensions = new();
        public IReadOnlyCollection<ContractExtension> Extensions => _extensions.AsReadOnly();

        private readonly List<AdditionalCharge> _additionalCharges = new();
        public IReadOnlyCollection<AdditionalCharge> AdditionalCharges => _additionalCharges.AsReadOnly();

        public Result StartContract(
            VehicleHandoverId handoverId,
            int currentMileage,
            UserId startedBy)
        {
            if (Status != ContractStatus.Ready)
                return Result.Failure("Contract must be in Ready status to start");

            PickupHandoverId = handoverId;
            ActualStartDate = DateTime.UtcNow;
            StartMileage = currentMileage;
            Status = ContractStatus.Active;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new ContractStartedEvent(Id, TenantId, VehicleId, startedBy));
            return Result.Success();
        }

        public Result CompleteContract(
            VehicleHandoverId returnHandoverId,
            int finalMileage,
            List<AdditionalCharge> additionalCharges,
            UserId completedBy)
        {
            if (Status != ContractStatus.Active)
                return Result.Failure("Only active contracts can be completed");

            ReturnHandoverId = returnHandoverId;
            ActualEndDate = DateTime.UtcNow;
            EndMileage = finalMileage;
            Status = ContractStatus.Completed;
            UpdatedAt = DateTime.UtcNow;

            // Add any additional charges
            foreach (var charge in additionalCharges)
            {
                _additionalCharges.Add(charge);
            }

            // Calculate excess mileage charges
            var totalMileage = finalMileage - StartMileage;
            if (totalMileage > IncludedMileageLimit)
            {
                var excessMileage = totalMileage - IncludedMileageLimit;
                var excessCharge = new AdditionalCharge(
                    AdditionalChargeType.ExcessMileage,
                    excessMileage * ExcessMileageRate,
                    $"Excess mileage: {excessMileage} km @ {ExcessMileageRate:C}/km"
                );
                _additionalCharges.Add(excessCharge);
            }

            AddDomainEvent(new ContractCompletedEvent(Id, TenantId, VehicleId, finalMileage));
            return Result.Success();
        }

        public Result ExtendContract(
            DateTime newEndDate,
            decimal extensionRate,
            UserId extendedBy)
        {
            if (Status != ContractStatus.Active)
                return Result.Failure("Only active contracts can be extended");

            if (newEndDate <= EffectiveEndDate)
                return Result.Failure("Extension end date must be after current end date");

            var extension = new ContractExtension(
                EffectiveEndDate,
                newEndDate,
                extensionRate,
                extendedBy,
                DateTime.UtcNow
            );

            _extensions.Add(extension);
            EffectiveEndDate = newEndDate;
            UpdatedAt = DateTime.UtcNow;

            AddDomainEvent(new ContractExtendedEvent(Id, TenantId, newEndDate, extensionRate));
            return Result.Success();
        }
    }

    public enum ContractStatus
    {
        Draft = 1,
        Ready = 2,
        Active = 3,
        Completed = 4,
        Terminated = 5
    }

    public enum SecurityDepositStatus
    {
        Pending = 1,
        Authorized = 2,
        Captured = 3,
        Released = 4,
        PartiallyReleased = 5
    }
}
```

#### Fleet Management Entities

```csharp
namespace DriveOps.Rental.Domain.Entities
{
    public class RentalVehicle : Entity
    {
        public RentalVehicleId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public VehicleId BaseVehicleId { get; private set; } // Reference to core vehicle
        public VehicleCategoryId CategoryId { get; private set; }
        public RentalLocationId HomeLocationId { get; private set; }
        public RentalLocationId? CurrentLocationId { get; private set; }
        
        // Rental-specific properties
        public RentalVehicleStatus Status { get; private set; }
        public decimal DailyRate { get; private set; }
        public decimal WeeklyRate { get; private set; }
        public decimal MonthlyRate { get; private set; }
        public decimal SecurityDepositAmount { get; private set; }
        
        // Availability management
        public bool IsAvailableForRental { get; private set; }
        public DateTime? AvailableFrom { get; private set; }
        public DateTime? AvailableUntil { get; private set; }
        public string? UnavailabilityReason { get; private set; }
        
        // Utilization tracking
        public int TotalRentalDays { get; private set; }
        public decimal TotalRevenue { get; private set; }
        public DateTime LastRentalEndDate { get; private set; }
        
        // Maintenance scheduling
        public int CurrentMileage { get; private set; }
        public DateTime? NextMaintenanceDate { get; private set; }
        public int? NextMaintenanceMileage { get; private set; }
        
        private readonly List<VehicleAvailabilityPeriod> _availabilityPeriods = new();
        public IReadOnlyCollection<VehicleAvailabilityPeriod> AvailabilityPeriods => _availabilityPeriods.AsReadOnly();

        public Result SetAvailability(DateTime from, DateTime until)
        {
            if (from >= until)
                return Result.Failure("Availability from date must be before until date");

            IsAvailableForRental = true;
            AvailableFrom = from;
            AvailableUntil = until;
            UnavailabilityReason = null;

            AddDomainEvent(new VehicleAvailabilityChangedEvent(Id, TenantId, true, from, until));
            return Result.Success();
        }

        public Result SetUnavailable(string reason, DateTime? unavailableUntil = null)
        {
            IsAvailableForRental = false;
            UnavailabilityReason = reason;
            AvailableUntil = unavailableUntil;

            AddDomainEvent(new VehicleAvailabilityChangedEvent(Id, TenantId, false, null, unavailableUntil));
            return Result.Success();
        }

        public Result UpdateLocation(RentalLocationId newLocationId, UserId updatedBy)
        {
            var previousLocationId = CurrentLocationId;
            CurrentLocationId = newLocationId;

#### Pricing Engine Entities

```csharp
namespace DriveOps.Rental.Domain.Entities
{
    public class TariffPlan : Entity
    {
        public TariffPlanId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public TariffType Type { get; private set; }
        
        // Validity period
        public DateTime ValidFrom { get; private set; }
        public DateTime ValidUntil { get; private set; }
        public bool IsActive { get; private set; }
        
        // Base rates
        public decimal HourlyRate { get; private set; }
        public decimal DailyRate { get; private set; }
        public decimal WeeklyRate { get; private set; }
        public decimal MonthlyRate { get; private set; }
        
        // Mileage
        public decimal IncludedMileagePerDay { get; private set; }
        public decimal ExcessMileageRate { get; private set; }
        
        // Discounts and multipliers
        public decimal LongTermDiscountPercentage { get; private set; }
        public int LongTermDiscountThresholdDays { get; private set; }
        public decimal WeekendMultiplier { get; private set; }
        public decimal HolidayMultiplier { get; private set; }
        
        private readonly List<VehicleCategoryId> _applicableCategories = new();
        public IReadOnlyCollection<VehicleCategoryId> ApplicableCategories => _applicableCategories.AsReadOnly();

        private readonly List<SeasonalPricing> _seasonalPricing = new();
        public IReadOnlyCollection<SeasonalPricing> SeasonalPricing => _seasonalPricing.AsReadOnly();

        public decimal CalculatePrice(
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            int estimatedMileage)
        {
            if (!_applicableCategories.Contains(categoryId))
                throw new InvalidOperationException("Tariff plan not applicable to this vehicle category");

            var duration = endDate - startDate;
            var days = (int)Math.Ceiling(duration.TotalDays);
            
            // Base price calculation
            decimal basePrice = days switch
            {
                >= 30 => days * (MonthlyRate / 30),
                >= 7 => Math.Min(days * DailyRate, (days / 7) * WeeklyRate + (days % 7) * DailyRate),
                _ => days * DailyRate
            };
            
            // Apply seasonal multipliers
            var seasonalMultiplier = GetSeasonalMultiplier(startDate, endDate);
            basePrice *= seasonalMultiplier;
            
            // Apply weekend/holiday multipliers
            var weekendHolidayMultiplier = GetWeekendHolidayMultiplier(startDate, endDate);
            basePrice *= weekendHolidayMultiplier;
            
            // Apply long-term discount
            if (days >= LongTermDiscountThresholdDays)
            {
                basePrice *= (1 - LongTermDiscountPercentage / 100);
            }
            
            // Add mileage costs
            var includedMileage = days * IncludedMileagePerDay;
            if (estimatedMileage > includedMileage)
            {
                var excessMileage = estimatedMileage - includedMileage;
                basePrice += excessMileage * ExcessMileageRate;
            }
            
            return Math.Round(basePrice, 2);
        }

        private decimal GetSeasonalMultiplier(DateTime startDate, DateTime endDate)
        {
            // Implementation for seasonal pricing logic
            var applicableSeasons = _seasonalPricing
                .Where(s => s.StartDate <= endDate && s.EndDate >= startDate)
                .ToList();
                
            return applicableSeasons.Any() ? applicableSeasons.Max(s => s.Multiplier) : 1.0m;
        }

        private decimal GetWeekendHolidayMultiplier(DateTime startDate, DateTime endDate)
        {
            // Simplified implementation - would need more sophisticated logic for actual use
            var isWeekend = startDate.DayOfWeek == DayOfWeek.Saturday || startDate.DayOfWeek == DayOfWeek.Sunday;
            return isWeekend ? WeekendMultiplier : 1.0m;
        }
    }

    public class SeasonalPricing : Entity
    {
        public SeasonalPricingId Id { get; private set; }
        public TariffPlanId TariffPlanId { get; private set; }
        public string SeasonName { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }
        public decimal Multiplier { get; private set; }
        public int Priority { get; private set; } // Higher priority overrides lower
        
        public bool IsApplicableForPeriod(DateTime startDate, DateTime endDate)
        {
            return StartDate <= endDate && EndDate >= startDate;
        }
    }

    public class PromoCode : Entity
    {
        public PromoCodeId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string Code { get; private set; }
        public string Description { get; private set; }
        public PromoCodeType Type { get; private set; }
        
        // Discount configuration
        public decimal DiscountValue { get; private set; }
        public DiscountType DiscountType { get; private set; }
        public decimal? MaxDiscountAmount { get; private set; }
        public decimal? MinOrderAmount { get; private set; }
        
        // Validity and usage
        public DateTime ValidFrom { get; private set; }
        public DateTime ValidUntil { get; private set; }
        public int? MaxUsages { get; private set; }
        public int? MaxUsagesPerCustomer { get; private set; }
        public int CurrentUsageCount { get; private set; }
        
        // Applicability
        public bool IsActive { get; private set; }
        public bool IsStackable { get; private set; }
        
        private readonly List<VehicleCategoryId> _applicableCategories = new();
        public IReadOnlyCollection<VehicleCategoryId> ApplicableCategories => _applicableCategories.AsReadOnly();

        private readonly List<RentalCustomerId> _eligibleCustomers = new();
        public IReadOnlyCollection<RentalCustomerId> EligibleCustomers => _eligibleCustomers.AsReadOnly();

        public Result<decimal> CalculateDiscount(decimal baseAmount, RentalCustomerId customerId)
        {
            // Validate promo code applicability
            var validationResult = ValidateUsage(customerId);
            if (!validationResult.IsSuccess)
                return Result<decimal>.Failure(validationResult.Error);

            if (MinOrderAmount.HasValue && baseAmount < MinOrderAmount.Value)
                return Result<decimal>.Failure("Minimum order amount not met");

            decimal discount = DiscountType switch
            {
                DiscountType.Percentage => baseAmount * (DiscountValue / 100),
                DiscountType.FixedAmount => DiscountValue,
                _ => 0
            };

            if (MaxDiscountAmount.HasValue)
                discount = Math.Min(discount, MaxDiscountAmount.Value);

            return Result<decimal>.Success(Math.Round(discount, 2));
        }

        public Result ValidateUsage(RentalCustomerId customerId)
        {
            if (!IsActive)
                return Result.Failure("Promo code is not active");

            if (DateTime.UtcNow < ValidFrom || DateTime.UtcNow > ValidUntil)
                return Result.Failure("Promo code is expired or not yet valid");

            if (MaxUsages.HasValue && CurrentUsageCount >= MaxUsages.Value)
                return Result.Failure("Promo code usage limit exceeded");

            if (_eligibleCustomers.Any() && !_eligibleCustomers.Contains(customerId))
                return Result.Failure("Customer not eligible for this promo code");

            return Result.Success();
        }

        public void RecordUsage()
        {
            CurrentUsageCount++;
        }
    }

    public enum TariffType
    {
        Standard = 1,
        Premium = 2,
        Corporate = 3,
        Seasonal = 4,
        Promotional = 5
    }

    public enum DiscountType
    {
        Percentage = 1,
        FixedAmount = 2
    }

    public enum PromoCodeType
    {
        General = 1,
        CustomerSpecific = 2,
        CategorySpecific = 3,
        FirstTime = 4,
        Loyalty = 5,
        Seasonal = 6
    }
}
```

#### Customer Management Entities

```csharp
namespace DriveOps.Rental.Domain.Entities
{
    public class RentalCustomer : AggregateRoot
    {
        public RentalCustomerId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public UserId? UserId { get; private set; } // Linked to Users module if registered
        public CustomerNumber Number { get; private set; }
        
        // Personal information
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Email { get; private set; }
        public string Phone { get; private set; }
        public DateTime DateOfBirth { get; private set; }
        public Address Address { get; private set; }
        
        // Driver information
        public string DriverLicenseNumber { get; private set; }
        public DateTime DriverLicenseExpiry { get; private set; }
        public string DriverLicenseCountry { get; private set; }
        public bool IsDriverLicenseVerified { get; private set; }
        public DateTime? DriverLicenseVerifiedAt { get; private set; }
        
        // Customer status
        public CustomerStatus Status { get; private set; }
        public CustomerTier Tier { get; private set; }
        public bool IsBlacklisted { get; private set; }
        public string? BlacklistReason { get; private set; }
        
        // Credit and risk assessment
        public CreditRating CreditRating { get; private set; }
        public decimal CreditLimit { get; private set; }
        public decimal AvailableCredit { get; private set; }
        public RiskLevel RiskLevel { get; private set; }
        
        // Loyalty program
        public int LoyaltyPoints { get; private set; }
        public CustomerTier LoyaltyTier { get; private set; }
        public DateTime? LoyaltyTierAchievedAt { get; private set; }
        
        // Statistics
        public int TotalRentals { get; private set; }
        public decimal TotalSpent { get; private set; }
        public DateTime? LastRentalDate { get; private set; }
        public decimal AverageRentalValue { get; private set; }
        
        // Preferences
        public CustomerPreferences Preferences { get; private set; }
        
        private readonly List<CustomerDocument> _documents = new();
        public IReadOnlyCollection<CustomerDocument> Documents => _documents.AsReadOnly();

        private readonly List<PaymentMethod> _paymentMethods = new();
        public IReadOnlyCollection<PaymentMethod> PaymentMethods => _paymentMethods.AsReadOnly();

        private readonly List<EmergencyContact> _emergencyContacts = new();
        public IReadOnlyCollection<EmergencyContact> EmergencyContacts => _emergencyContacts.AsReadOnly();

        public Result VerifyDriverLicense(bool isValid, UserId verifiedBy)
        {
            IsDriverLicenseVerified = isValid;
            if (isValid)
            {
                DriverLicenseVerifiedAt = DateTime.UtcNow;
                AddDomainEvent(new DriverLicenseVerifiedEvent(Id, TenantId, verifiedBy));
            }

            return Result.Success();
        }

        public Result AddLoyaltyPoints(int points, string reason, UserId addedBy)
        {
            if (points <= 0)
                return Result.Failure("Points must be positive");

            LoyaltyPoints += points;
            
            // Check for tier upgrades
            var newTier = CalculateLoyaltyTier(LoyaltyPoints);
            if (newTier > LoyaltyTier)
            {
                LoyaltyTier = newTier;
                LoyaltyTierAchievedAt = DateTime.UtcNow;
                AddDomainEvent(new CustomerTierUpgradedEvent(Id, TenantId, newTier));
            }

            AddDomainEvent(new LoyaltyPointsAddedEvent(Id, TenantId, points, reason));
            return Result.Success();
        }

        public Result RedeemLoyaltyPoints(int points, string reason, UserId redeemedBy)
        {
            if (points <= 0)
                return Result.Failure("Points must be positive");

            if (LoyaltyPoints < points)
                return Result.Failure("Insufficient loyalty points");

            LoyaltyPoints -= points;
            AddDomainEvent(new LoyaltyPointsRedeemedEvent(Id, TenantId, points, reason));
            
            return Result.Success();
        }

        public Result UpdateCreditRating(CreditRating newRating, decimal newLimit, UserId updatedBy)
#### Vehicle Handover and Damage Assessment

```csharp
namespace DriveOps.Rental.Domain.Entities
{
    public class VehicleHandover : AggregateRoot
    {
        public VehicleHandoverId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public RentalVehicleId VehicleId { get; private set; }
        public RentalCustomerId CustomerId { get; private set; }
        public ReservationId? ReservationId { get; private set; }
        public RentalContractId? ContractId { get; private set; }
        
        // Handover details
        public HandoverType Type { get; private set; }
        public DateTime ScheduledDateTime { get; private set; }
        public DateTime? ActualDateTime { get; private set; }
        public RentalLocationId LocationId { get; private set; }
        
        // Personnel
        public UserId AssignedStaffId { get; private set; }
        public UserId? CompletedByStaffId { get; private set; }
        
        // Vehicle condition
        public VehicleInspectionId? InspectionId { get; private set; }
        public int VehicleMileage { get; private set; }
        public FuelLevel FuelLevel { get; private set; }
        public VehicleCondition OverallCondition { get; private set; }
        
        // Digital signature and photos
        public string? CustomerSignature { get; private set; }
        public string? StaffSignature { get; private set; }
        public DateTime? CustomerSignedAt { get; private set; }
        public DateTime? StaffSignedAt { get; private set; }
        
        // Status
        public HandoverStatus Status { get; private set; }
        public string? Notes { get; private set; }
        public string? CustomerComments { get; private set; }
        
        private readonly List<HandoverPhoto> _photos = new();
        public IReadOnlyCollection<HandoverPhoto> Photos => _photos.AsReadOnly();

        private readonly List<HandoverChecklist> _checklistItems = new();
        public IReadOnlyCollection<HandoverChecklist> ChecklistItems => _checklistItems.AsReadOnly();

        public static VehicleHandover Create(
            TenantId tenantId,
            RentalVehicleId vehicleId,
            RentalCustomerId customerId,
            HandoverType type,
            DateTime scheduledDateTime,
            RentalLocationId locationId,
            UserId assignedStaffId)
        {
            var handover = new VehicleHandover
            {
                Id = VehicleHandoverId.New(),
                TenantId = tenantId,
                VehicleId = vehicleId,
                CustomerId = customerId,
                Type = type,
                ScheduledDateTime = scheduledDateTime,
                LocationId = locationId,
                AssignedStaffId = assignedStaffId,
                Status = HandoverStatus.Scheduled,
                OverallCondition = VehicleCondition.Unknown
            };

            handover.AddDomainEvent(new HandoverScheduledEvent(handover.Id, tenantId, type, scheduledDateTime));
            return handover;
        }

        public Result StartHandover(int currentMileage, FuelLevel fuelLevel, UserId staffId)
        {
            if (Status != HandoverStatus.Scheduled)
                return Result.Failure("Can only start scheduled handovers");

            Status = HandoverStatus.InProgress;
            ActualDateTime = DateTime.UtcNow;
            VehicleMileage = currentMileage;
            FuelLevel = fuelLevel;
            CompletedByStaffId = staffId;

            AddDomainEvent(new HandoverStartedEvent(Id, TenantId, Type, staffId));
            return Result.Success();
        }

        public Result AddPhoto(string photoUrl, PhotoType photoType, string? description, UserId takenBy)
        {
            var photo = new HandoverPhoto(
                HandoverPhotoId.New(),
                photoUrl,
                photoType,
                description,
                takenBy,
                DateTime.UtcNow
            );

            _photos.Add(photo);
            AddDomainEvent(new HandoverPhotoAddedEvent(Id, TenantId, photo.Id, photoType));
            
            return Result.Success();
        }

        public Result CompleteHandover(
            VehicleCondition overallCondition,
            string? customerSignature,
            string? notes,
            UserId completedBy)
        {
            if (Status != HandoverStatus.InProgress)
                return Result.Failure("Can only complete handovers in progress");

            // Validate required checklist items are completed
            var requiredItems = _checklistItems.Where(x => x.IsRequired);
            if (requiredItems.Any(x => !x.IsCompleted))
                return Result.Failure("All required checklist items must be completed");

            Status = HandoverStatus.Completed;
            OverallCondition = overallCondition;
            CustomerSignature = customerSignature;
            StaffSignature = "STAFF_SIGNATURE"; // Would be actual signature in real implementation
            CustomerSignedAt = DateTime.UtcNow;
            StaffSignedAt = DateTime.UtcNow;
            Notes = notes;

            AddDomainEvent(new HandoverCompletedEvent(Id, TenantId, Type, overallCondition, completedBy));
            return Result.Success();
        }

        public Result UpdateChecklistItem(string itemCode, bool isCompleted, string? notes, UserId updatedBy)
        {
            var item = _checklistItems.FirstOrDefault(x => x.ItemCode == itemCode);
            if (item == null)
                return Result.Failure("Checklist item not found");

            item.Update(isCompleted, notes, updatedBy);
            return Result.Success();
        }
    }

    public class VehicleInspection : Entity
    {
        public VehicleInspectionId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public RentalVehicleId VehicleId { get; private set; }
        public VehicleHandoverId? HandoverId { get; private set; }
        
        public InspectionType Type { get; private set; }
        public DateTime InspectionDateTime { get; private set; }
        public UserId InspectedBy { get; private set; }
        public int VehicleMileage { get; private set; }
        public FuelLevel FuelLevel { get; private set; }
        
        // Overall assessment
        public VehicleCondition OverallCondition { get; private set; }
        public bool HasDamages { get; private set; }
        public string? GeneralNotes { get; private set; }
        
        private readonly List<DamageReport> _damageReports = new();
        public IReadOnlyCollection<DamageReport> DamageReports => _damageReports.AsReadOnly();

        private readonly List<InspectionPhoto> _photos = new();
        public IReadOnlyCollection<InspectionPhoto> Photos => _photos.AsReadOnly();

        public Result AddDamageReport(
            VehicleArea area,
            DamageType damageType,
            DamageSeverity severity,
            string description,
            decimal? estimatedCost,
            UserId reportedBy)
        {
            var damage = DamageReport.Create(
                TenantId,
                VehicleId,
                Id,
                area,
                damageType,
                severity,
                description,
                estimatedCost,
                reportedBy
            );

            _damageReports.Add(damage);
            HasDamages = true;

            // Adjust overall condition based on damage severity
            if (severity >= DamageSeverity.Major && OverallCondition < VehicleCondition.Damaged)
            {
                OverallCondition = VehicleCondition.Damaged;
            }

            AddDomainEvent(new DamageReportCreatedEvent(damage.Id, TenantId, VehicleId, damageType, severity));
            return Result.Success();
        }
    }

    public class DamageReport : Entity
    {
        public DamageReportId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public RentalVehicleId VehicleId { get; private set; }
        public VehicleInspectionId? InspectionId { get; private set; }
        public RentalContractId? ContractId { get; private set; }
        
        // Damage details
        public VehicleArea Area { get; private set; }
        public DamageType Type { get; private set; }
        public DamageSeverity Severity { get; private set; }
        public string Description { get; private set; }
        
        // Financial impact
        public decimal? EstimatedRepairCost { get; private set; }
        public decimal? ActualRepairCost { get; private set; }
        public bool IsCustomerLiable { get; private set; }
        public decimal CustomerLiabilityAmount { get; private set; }
        
        // Processing
        public DamageStatus Status { get; private set; }
        public DateTime ReportedAt { get; private set; }
        public UserId ReportedBy { get; private set; }
        public DateTime? ResolvedAt { get; private set; }
        public UserId? ResolvedBy { get; private set; }
        
        // Insurance
        public InsuranceClaimId? InsuranceClaimId { get; private set; }
        public bool IsInsuranceCovered { get; private set; }
        
        private readonly List<DamagePhoto> _photos = new();
        public IReadOnlyCollection<DamagePhoto> Photos => _photos.AsReadOnly();

        public static DamageReport Create(
            TenantId tenantId,
            RentalVehicleId vehicleId,
            VehicleInspectionId? inspectionId,
            VehicleArea area,
            DamageType type,
            DamageSeverity severity,
            string description,
            decimal? estimatedCost,
            UserId reportedBy)
        {
            return new DamageReport
            {
                Id = DamageReportId.New(),
                TenantId = tenantId,
                VehicleId = vehicleId,
                InspectionId = inspectionId,
                Area = area,
                Type = type,
                Severity = severity,
                Description = description,
                EstimatedRepairCost = estimatedCost,
                Status = DamageStatus.Reported,
                ReportedAt = DateTime.UtcNow,
                ReportedBy = reportedBy
            };
        }

        public Result AssessCustomerLiability(bool isLiable, decimal liabilityAmount, UserId assessedBy)
        {
            IsCustomerLiable = isLiable;
            CustomerLiabilityAmount = liabilityAmount;
            
            if (Status == DamageStatus.Reported)
                Status = DamageStatus.Assessed;

            return Result.Success();
        }

        public Result CreateInsuranceClaim(InsuranceClaimId claimId, bool isCovered)
        {
            InsuranceClaimId = claimId;
            IsInsuranceCovered = isCovered;
            return Result.Success();
        }
    }

    // Enums for handover and damage management
    public enum HandoverType
    {
        Pickup = 1,
        Return = 2,
        Transfer = 3,
        Maintenance = 4
    }

    public enum HandoverStatus
    {
        Scheduled = 1,
        InProgress = 2,
        Completed = 3,
        Cancelled = 4,
        NoShow = 5
    }

    public enum VehicleCondition
    {
        Unknown = 0,
        Excellent = 1,
        Good = 2,
        Fair = 3,
        Poor = 4,
        Damaged = 5
    }

    public enum FuelLevel
    {
        Empty = 0,
        Quarter = 25,
        Half = 50,
        ThreeQuarters = 75,
        Full = 100
    }

    public enum VehicleArea
    {
        FrontBumper = 1,
        RearBumper = 2,
        LeftSide = 3,
        RightSide = 4,
        Roof = 5,
        Hood = 6,
        Trunk = 7,
        LeftMirror = 8,
        RightMirror = 9,
        Windshield = 10,
        RearWindow = 11,
        LeftHeadlight = 12,
        RightHeadlight = 13,
        LeftTaillight = 14,
        RightTaillight = 15,
        Interior = 16,
        Wheels = 17,
        Undercarriage = 18
    }

    public enum DamageType
```csharp
namespace DriveOps.Rental.Application.Queries
{
    // Reservation Queries
    public record GetReservationByIdQuery(
        string TenantId,
        string ReservationId
    ) : IRequest<Result<ReservationDto>>;

    public record GetReservationsByCustomerQuery(
        string TenantId,
        string CustomerId,
        int Page = 1,
        int PageSize = 50,
        ReservationStatus? Status = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null
    ) : IRequest<Result<PagedResult<ReservationDto>>>;

    public record GetReservationsByDateRangeQuery(
        string TenantId,
        DateTime StartDate,
        DateTime EndDate,
        string? LocationId = null,
        ReservationStatus? Status = null,
        int Page = 1,
        int PageSize = 50
    ) : IRequest<Result<PagedResult<ReservationDto>>>;

    // Vehicle Availability Queries
    public record CheckVehicleAvailabilityQuery(
        string TenantId,
        string? CategoryId,
        DateTime StartDate,
        DateTime EndDate,
        string? LocationId = null
    ) : IRequest<Result<VehicleAvailabilityDto>>;

    public record GetVehicleAvailabilityCalendarQuery(
        string TenantId,
        string VehicleId,
        DateTime StartDate,
        DateTime EndDate
    ) : IRequest<Result<List<AvailabilityPeriodDto>>>;

    // Fleet Management Queries
    public record GetRentalVehiclesQuery(
        string TenantId,
        string? CategoryId = null,
        RentalVehicleStatus? Status = null,
        string? LocationId = null,
        bool? IsAvailable = null,
        int Page = 1,
        int PageSize = 50
    ) : IRequest<Result<PagedResult<RentalVehicleDto>>>;

    public record GetVehicleUtilizationReportQuery(
        string TenantId,
        DateTime StartDate,
        DateTime EndDate,
        string? CategoryId = null,
        string? VehicleId = null
    ) : IRequest<Result<VehicleUtilizationReportDto>>;

    // Customer Queries
    public record GetRentalCustomerByIdQuery(
        string TenantId,
        string CustomerId
    ) : IRequest<Result<RentalCustomerDto>>;

    public record SearchRentalCustomersQuery(
        string TenantId,
        string? SearchTerm = null,
        CustomerStatus? Status = null,
        CustomerTier? Tier = null,
        int Page = 1,
        int PageSize = 50
    ) : IRequest<Result<PagedResult<RentalCustomerDto>>>;

    // Query Handlers
    public class GetReservationByIdQueryHandler : IRequestHandler<GetReservationByIdQuery, Result<ReservationDto>>
    {
        private readonly IReservationRepository _repository;
        private readonly IMapper _mapper;

        public GetReservationByIdQueryHandler(IReservationRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public async Task<Result<ReservationDto>> Handle(GetReservationByIdQuery request, CancellationToken cancellationToken)
        {
            var reservation = await _repository.GetByIdAsync(
                ReservationId.From(Guid.Parse(request.ReservationId)),
                cancellationToken);

            if (reservation == null || reservation.TenantId.Value.ToString() != request.TenantId)
                return Result<ReservationDto>.Failure("Reservation not found");

            var dto = _mapper.Map<ReservationDto>(reservation);
            return Result<ReservationDto>.Success(dto);
        }
    }

    public class CheckVehicleAvailabilityQueryHandler : IRequestHandler<CheckVehicleAvailabilityQuery, Result<VehicleAvailabilityDto>>
    {
        private readonly IAvailabilityService _availabilityService;
        private readonly IVehicleCategoryRepository _categoryRepository;

        public CheckVehicleAvailabilityQueryHandler(
            IAvailabilityService availabilityService,
            IVehicleCategoryRepository categoryRepository)
        {
            _availabilityService = availabilityService;
            _categoryRepository = categoryRepository;
        }

        public async Task<Result<VehicleAvailabilityDto>> Handle(CheckVehicleAvailabilityQuery request, CancellationToken cancellationToken)
        {
            var tenantId = TenantId.From(Guid.Parse(request.TenantId));

            if (!string.IsNullOrEmpty(request.CategoryId))
            {
                var categoryId = VehicleCategoryId.From(Guid.Parse(request.CategoryId));
                var result = await _availabilityService.CheckAvailabilityAsync(
                    tenantId, categoryId, request.StartDate, request.EndDate, cancellationToken);
                
                return result.IsSuccess ? 
                    Result<VehicleAvailabilityDto>.Success(result.Value) : 
                    Result<VehicleAvailabilityDto>.Failure(result.Error);
            }

            // Check availability across all categories
            var categories = await _categoryRepository.GetActiveCategoriesAsync(tenantId, cancellationToken);
            var availabilityTasks = categories.Select(async category =>
            {
                var availability = await _availabilityService.CheckAvailabilityAsync(
                    tenantId, category.Id, request.StartDate, request.EndDate, cancellationToken);
                
                return new CategoryAvailabilityDto
                {
                    CategoryId = category.Id.Value.ToString(),
                    CategoryName = category.Name,
                    IsAvailable = availability.IsSuccess && availability.Value.IsAvailable,
                    AvailableCount = availability.IsSuccess ? availability.Value.AvailableCount : 0,
                    TotalCount = availability.IsSuccess ? availability.Value.TotalCount : 0
                };
            });

            var categoryAvailabilities = await Task.WhenAll(availabilityTasks);
            
            var overallAvailability = new VehicleAvailabilityDto
            {
                IsAvailable = categoryAvailabilities.Any(ca => ca.IsAvailable),
                TotalAvailableVehicles = categoryAvailabilities.Sum(ca => ca.AvailableCount),
                TotalVehicles = categoryAvailabilities.Sum(ca => ca.TotalCount),
                CategoryAvailabilities = categoryAvailabilities.ToList(),
                SearchCriteria = new AvailabilitySearchCriteriaDto
                {
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    LocationId = request.LocationId
                }
            };

            return Result<VehicleAvailabilityDto>.Success(overallAvailability);
        }
    }
}
```

### 2.2 Application Services

```csharp
namespace DriveOps.Rental.Application.Services
{
    public interface IAvailabilityService
    {
        Task<Result<VehicleAvailabilityDto>> CheckAvailabilityAsync(
            TenantId tenantId,
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);
            
        Task<Result> ReserveVehicleAsync(
            ReservationId reservationId,
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default);
            
        Task<Result> ReleaseReservationAsync(
            ReservationId reservationId,
            CancellationToken cancellationToken = default);
            
        Task<Result<RentalVehicle>> AssignOptimalVehicleAsync(
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            RentalLocationId? preferredLocationId = null,
            CancellationToken cancellationToken = default);
    }

    public class AvailabilityService : IAvailabilityService
    {
        private readonly IRentalVehicleRepository _vehicleRepository;
        private readonly IReservationRepository _reservationRepository;
        private readonly IDistributedCache _cache;
        private readonly ILogger<AvailabilityService> _logger;

        public AvailabilityService(
            IRentalVehicleRepository vehicleRepository,
            IReservationRepository reservationRepository,
            IDistributedCache cache,
            ILogger<AvailabilityService> logger)
        {
            _vehicleRepository = vehicleRepository;
            _reservationRepository = reservationRepository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<Result<VehicleAvailabilityDto>> CheckAvailabilityAsync(
            TenantId tenantId,
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            // Check cache first for recent availability queries
            var cacheKey = $"availability:{tenantId}:{categoryId}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
            var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedResult))
            {
                var cached = JsonSerializer.Deserialize<VehicleAvailabilityDto>(cachedResult);
                return Result<VehicleAvailabilityDto>.Success(cached!);
            }

            // Get all vehicles in category
            var vehicles = await _vehicleRepository.GetByCategoryAsync(tenantId, categoryId, cancellationToken);
            var totalVehicles = vehicles.Count;

            if (totalVehicles == 0)
            {
                return Result<VehicleAvailabilityDto>.Success(new VehicleAvailabilityDto
                {
                    IsAvailable = false,
                    AvailableCount = 0,
                    TotalCount = 0
                });
            }

            // Check availability for each vehicle
            var availableVehicles = new List<RentalVehicle>();
            
            foreach (var vehicle in vehicles)
            {
                if (vehicle.IsAvailableForPeriod(startDate, endDate))
                {
                    // Double-check against existing reservations
                    var hasConflictingReservation = await _reservationRepository
                        .HasConflictingReservationAsync(vehicle.Id, startDate, endDate, cancellationToken);
                        
                    if (!hasConflictingReservation)
                    {
                        availableVehicles.Add(vehicle);
                    }
                }
            }

            var result = new VehicleAvailabilityDto
            {
                IsAvailable = availableVehicles.Count > 0,
                AvailableCount = availableVehicles.Count,
                TotalCount = totalVehicles,
                AvailableVehicleIds = availableVehicles.Select(v => v.Id.Value.ToString()).ToList()
            };

            // Cache result for 5 minutes
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };
            
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(result),
                cacheOptions,
                cancellationToken);

            return Result<VehicleAvailabilityDto>.Success(result);
        }

        public async Task<Result<RentalVehicle>> AssignOptimalVehicleAsync(
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            RentalLocationId? preferredLocationId = null,
            CancellationToken cancellationToken = default)
        {
            var vehicles = await _vehicleRepository.GetByCategoryAsync(
                categoryId.TenantId, categoryId, cancellationToken);

            var availableVehicles = vehicles
                .Where(v => v.IsAvailableForPeriod(startDate, endDate))
                .ToList();

            if (!availableVehicles.Any())
                return Result<RentalVehicle>.Failure("No vehicles available for the specified period");

            // Optimization algorithm: prefer vehicles at preferred location, 
            // then by lowest mileage, then by last rental date
            var optimalVehicle = availableVehicles
                .OrderByDescending(v => preferredLocationId.HasValue && v.CurrentLocationId == preferredLocationId ? 1 : 0)
                .ThenBy(v => v.CurrentMileage)
                .ThenBy(v => v.LastRentalEndDate)
                .First();

            return Result<RentalVehicle>.Success(optimalVehicle);
        }

### 2.3 Infrastructure Layer

```csharp
namespace DriveOps.Rental.Infrastructure.Repositories
{
    public class ReservationRepository : IReservationRepository
    {
        private readonly RentalDbContext _context;
        private readonly IEventDispatcher _eventDispatcher;

        public ReservationRepository(RentalDbContext context, IEventDispatcher eventDispatcher)
        {
            _context = context;
            _eventDispatcher = eventDispatcher;
        }

        public async Task<Reservation?> GetByIdAsync(ReservationId id, CancellationToken cancellationToken = default)
        {
            return await _context.Reservations
                .Include(r => r.Customer)
                .Include(r => r.Vehicle)
                .Include(r => r.Category)
                .Include(r => r.PickupLocation)
                .Include(r => r.ReturnLocation)
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task<PagedResult<Reservation>> GetByCustomerAsync(
            RentalCustomerId customerId,
            int page,
            int pageSize,
            ReservationStatus? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Reservations
                .Include(r => r.Vehicle)
                .Include(r => r.Category)
                .Where(r => r.CustomerId == customerId);

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            if (fromDate.HasValue)
                query = query.Where(r => r.PickupDateTime >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(r => r.ReturnDateTime <= toDate.Value);

            var totalCount = await query.CountAsync(cancellationToken);
            
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<Reservation>(items, totalCount, page, pageSize);
        }

        public async Task<bool> HasConflictingReservationAsync(
            RentalVehicleId vehicleId,
            DateTime startDate,
            DateTime endDate,
            ReservationId? excludeReservationId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Reservations
                .Where(r => r.VehicleId == vehicleId)
                .Where(r => r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.InProgress)
                .Where(r => r.PickupDateTime < endDate && r.ReturnDateTime > startDate);

            if (excludeReservationId.HasValue)
                query = query.Where(r => r.Id != excludeReservationId.Value);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task AddAsync(Reservation reservation, CancellationToken cancellationToken = default)
        {
            await _context.Reservations.AddAsync(reservation, cancellationToken);
        }

        public async Task UpdateAsync(Reservation reservation, CancellationToken cancellationToken = default)
        {
            _context.Reservations.Update(reservation);
            
            // Dispatch domain events
            await _eventDispatcher.DispatchAsync(reservation.DomainEvents, cancellationToken);
            reservation.ClearDomainEvents();
        }

        public async Task DeleteAsync(ReservationId id, CancellationToken cancellationToken = default)
        {
            var reservation = await GetByIdAsync(id, cancellationToken);
            if (reservation != null)
            {
                _context.Reservations.Remove(reservation);
            }
        }
    }

    public class RentalVehicleRepository : IRentalVehicleRepository
    {
        private readonly RentalDbContext _context;

        public RentalVehicleRepository(RentalDbContext context)
        {
            _context = context;
        }

        public async Task<RentalVehicle?> GetByIdAsync(RentalVehicleId id, CancellationToken cancellationToken = default)
        {
            return await _context.RentalVehicles
                .Include(v => v.Category)
                .Include(v => v.HomeLocation)
                .Include(v => v.CurrentLocation)
                .Include(v => v.BaseVehicle)
                .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        }

        public async Task<List<RentalVehicle>> GetByCategoryAsync(
            TenantId tenantId,
            VehicleCategoryId categoryId,
            CancellationToken cancellationToken = default)
        {
            return await _context.RentalVehicles
                .Include(v => v.Category)
                .Where(v => v.TenantId == tenantId && v.CategoryId == categoryId)
                .Where(v => v.Status != RentalVehicleStatus.OutOfService)
                .ToListAsync(cancellationToken);
        }

        public async Task<PagedResult<RentalVehicle>> GetVehiclesAsync(
            TenantId tenantId,
            VehicleCategoryId? categoryId = null,
            RentalVehicleStatus? status = null,
            RentalLocationId? locationId = null,
            bool? isAvailable = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            var query = _context.RentalVehicles
                .Include(v => v.Category)
                .Include(v => v.BaseVehicle)
                .Where(v => v.TenantId == tenantId);

            if (categoryId.HasValue)
                query = query.Where(v => v.CategoryId == categoryId.Value);

            if (status.HasValue)
                query = query.Where(v => v.Status == status.Value);

            if (locationId.HasValue)
                query = query.Where(v => v.CurrentLocationId == locationId.Value);

            if (isAvailable.HasValue)
                query = query.Where(v => v.IsAvailableForRental == isAvailable.Value);

            var totalCount = await query.CountAsync(cancellationToken);
            
            var items = await query
                .OrderBy(v => v.Category.Name)
                .ThenBy(v => v.BaseVehicle.LicensePlate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<RentalVehicle>(items, totalCount, page, pageSize);
        }

        public async Task AddAsync(RentalVehicle vehicle, CancellationToken cancellationToken = default)
        {
            await _context.RentalVehicles.AddAsync(vehicle, cancellationToken);
        }

        public async Task UpdateAsync(RentalVehicle vehicle, CancellationToken cancellationToken = default)
        {
            _context.RentalVehicles.Update(vehicle);
        }
    }
}

namespace DriveOps.Rental.Infrastructure.Persistence
{
    public class RentalDbContext : DbContext
    {
        public RentalDbContext(DbContextOptions<RentalDbContext> options) : base(options) { }

        // Reservation and contract entities
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<RentalContract> RentalContracts { get; set; }
        public DbSet<ContractExtension> ContractExtensions { get; set; }
        public DbSet<AdditionalCharge> AdditionalCharges { get; set; }

        // Fleet management entities
        public DbSet<RentalVehicle> RentalVehicles { get; set; }
        public DbSet<VehicleCategory> VehicleCategories { get; set; }
        public DbSet<VehicleAvailabilityPeriod> VehicleAvailabilityPeriods { get; set; }

        // Customer management entities
        public DbSet<RentalCustomer> RentalCustomers { get; set; }
        public DbSet<CustomerDocument> CustomerDocuments { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<EmergencyContact> EmergencyContacts { get; set; }

        // Pricing entities
        public DbSet<TariffPlan> TariffPlans { get; set; }
        public DbSet<SeasonalPricing> SeasonalPricing { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }

        // Handover and inspection entities
        public DbSet<VehicleHandover> VehicleHandovers { get; set; }
        public DbSet<VehicleInspection> VehicleInspections { get; set; }
        public DbSet<DamageReport> DamageReports { get; set; }
        public DbSet<HandoverPhoto> HandoverPhotos { get; set; }
        public DbSet<InspectionPhoto> InspectionPhotos { get; set; }
        public DbSet<DamagePhoto> DamagePhotos { get; set; }

        // Location and insurance entities
        public DbSet<RentalLocation> RentalLocations { get; set; }
        public DbSet<InsurancePolicy> InsurancePolicies { get; set; }
        public DbSet<InsuranceClaim> InsuranceClaims { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure schema
            modelBuilder.HasDefaultSchema("rental");

            // Apply configurations
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(RentalDbContext).Assembly);
        }
    }

    // Entity Configurations
    public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
    {
        public void Configure(EntityTypeBuilder<Reservation> builder)
        {
            builder.ToTable("reservations");
            
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id)
                .HasConversion(id => id.Value, value => ReservationId.From(value));

            builder.Property(r => r.TenantId)
                .HasConversion(id => id.Value, value => TenantId.From(value))
                .IsRequired();

            builder.Property(r => r.Number)
                .HasConversion(n => n.Value, value => ReservationNumber.From(value))
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(r => r.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(r => r.PaymentStatus)
                .HasConversion<int>()
                .IsRequired();

            // Indexes for performance
            builder.HasIndex(r => new { r.TenantId, r.Number }).IsUnique();
            builder.HasIndex(r => new { r.TenantId, r.CustomerId });
            builder.HasIndex(r => new { r.TenantId, r.VehicleId });
            builder.HasIndex(r => new { r.TenantId, r.PickupDateTime, r.ReturnDateTime });
            builder.HasIndex(r => new { r.TenantId, r.Status });

            // Relationships
            builder.HasOne<RentalCustomer>()
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<RentalVehicle>()
                .WithMany()
                .HasForeignKey(r => r.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class RentalVehicleConfiguration : IEntityTypeConfiguration<RentalVehicle>
    {
        public void Configure(EntityTypeBuilder<RentalVehicle> builder)
        {
            builder.ToTable("rental_vehicles");
            
            builder.HasKey(v => v.Id);
            builder.Property(v => v.Id)
                .HasConversion(id => id.Value, value => RentalVehicleId.From(value));

            builder.Property(v => v.TenantId)
                .HasConversion(id => id.Value, value => TenantId.From(value))
                .IsRequired();

            builder.Property(v => v.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(v => v.DailyRate)
                .HasPrecision(10, 2)
                .IsRequired();

            builder.Property(v => v.WeeklyRate)
                .HasPrecision(10, 2)
                .IsRequired();

            builder.Property(v => v.MonthlyRate)
                .HasPrecision(10, 2)
-- Vehicle handovers table
CREATE TABLE rental.vehicle_handovers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    vehicle_id UUID NOT NULL REFERENCES rental.rental_vehicles(id),
    customer_id UUID NOT NULL REFERENCES rental.rental_customers(id),
    reservation_id UUID REFERENCES rental.reservations(id),
    contract_id UUID REFERENCES rental.rental_contracts(id),
    
    -- Handover details
    type INTEGER NOT NULL, -- 1: Pickup, 2: Return, 3: Transfer, 4: Maintenance
    scheduled_datetime TIMESTAMP WITH TIME ZONE NOT NULL,
    actual_datetime TIMESTAMP WITH TIME ZONE,
    location_id UUID NOT NULL REFERENCES rental.rental_locations(id),
    
    -- Personnel
    assigned_staff_id UUID NOT NULL, -- References users.users(id)
    completed_by_staff_id UUID, -- References users.users(id)
    
    -- Vehicle condition
    inspection_id UUID REFERENCES rental.vehicle_inspections(id),
    vehicle_mileage INTEGER NOT NULL DEFAULT 0,
    fuel_level INTEGER NOT NULL DEFAULT 0, -- 0: Empty, 25: Quarter, 50: Half, 75: ThreeQuarters, 100: Full
    overall_condition INTEGER NOT NULL DEFAULT 0, -- 0: Unknown, 1: Excellent, 2: Good, 3: Fair, 4: Poor, 5: Damaged
    
    -- Digital signature and photos
    customer_signature TEXT,
    staff_signature TEXT,
    customer_signed_at TIMESTAMP WITH TIME ZONE,
    staff_signed_at TIMESTAMP WITH TIME ZONE,
    
    -- Status
    status INTEGER NOT NULL DEFAULT 1, -- 1: Scheduled, 2: InProgress, 3: Completed, 4: Cancelled, 5: NoShow
    notes TEXT,
    customer_comments TEXT,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Vehicle inspections table
CREATE TABLE rental.vehicle_inspections (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    vehicle_id UUID NOT NULL REFERENCES rental.rental_vehicles(id),
    handover_id UUID REFERENCES rental.vehicle_handovers(id),
    
    type INTEGER NOT NULL, -- 1: PreRental, 2: PostRental, 3: Maintenance, 4: Damage, 5: Insurance
    inspection_datetime TIMESTAMP WITH TIME ZONE NOT NULL,
    inspected_by UUID NOT NULL, -- References users.users(id)
    vehicle_mileage INTEGER NOT NULL,
    fuel_level INTEGER NOT NULL,
    
    -- Overall assessment
    overall_condition INTEGER NOT NULL, -- 1: Excellent, 2: Good, 3: Fair, 4: Poor, 5: Damaged
    has_damages BOOLEAN NOT NULL DEFAULT FALSE,
    general_notes TEXT,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Damage reports table
CREATE TABLE rental.damage_reports (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    vehicle_id UUID NOT NULL REFERENCES rental.rental_vehicles(id),
    inspection_id UUID REFERENCES rental.vehicle_inspections(id),
    contract_id UUID REFERENCES rental.rental_contracts(id),
    
    -- Damage details
    area INTEGER NOT NULL, -- 1: FrontBumper, 2: RearBumper, etc.
    type INTEGER NOT NULL, -- 1: Scratch, 2: Dent, 3: Crack, etc.
    severity INTEGER NOT NULL, -- 1: Minor, 2: Moderate, 3: Major, 4: Severe
    description TEXT NOT NULL,
    
    -- Financial impact
    estimated_repair_cost DECIMAL(10,2),
    actual_repair_cost DECIMAL(10,2),
    is_customer_liable BOOLEAN NOT NULL DEFAULT FALSE,
    customer_liability_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    
    -- Processing
    status INTEGER NOT NULL DEFAULT 1, -- 1: Reported, 2: Assessed, 3: UnderRepair, 4: Repaired, 5: WrittenOff
    reported_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    reported_by UUID NOT NULL, -- References users.users(id)
    resolved_at TIMESTAMP WITH TIME ZONE,
    resolved_by UUID, -- References users.users(id)
    
    -- Insurance
    insurance_claim_id UUID REFERENCES rental.insurance_claims(id),
    is_insurance_covered BOOLEAN NOT NULL DEFAULT FALSE,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Rental locations table
CREATE TABLE rental.rental_locations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    location_type INTEGER NOT NULL, -- 1: MainOffice, 2: Airport, 3: Hotel, 4: TrainStation, 5: Mall
    
    -- Address
    street VARCHAR(255) NOT NULL,
    city VARCHAR(100) NOT NULL,
    postal_code VARCHAR(20) NOT NULL,
    country VARCHAR(100) NOT NULL,
    
    -- Contact information
    phone VARCHAR(50),
    email VARCHAR(255),
    
    -- Operational details
    is_pickup_location BOOLEAN NOT NULL DEFAULT TRUE,
    is_return_location BOOLEAN NOT NULL DEFAULT TRUE,
    is_24_hours BOOLEAN NOT NULL DEFAULT FALSE,
    opening_hours JSONB, -- JSON object with opening hours
    
    -- Geographical data
    latitude DECIMAL(10, 8),
    longitude DECIMAL(11, 8),
    
    -- Capacity
    vehicle_capacity INTEGER NOT NULL DEFAULT 10,
    
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, name)
);

-- Insurance policies table
CREATE TABLE rental.insurance_policies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    policy_number VARCHAR(50) NOT NULL,
    insurance_provider VARCHAR(100) NOT NULL,
    policy_type INTEGER NOT NULL, -- 1: Comprehensive, 2: ThirdParty, 3: Collision, 4: Theft
    
    -- Coverage details
    coverage_amount DECIMAL(12,2) NOT NULL,
    deductible_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    premium_amount DECIMAL(10,2) NOT NULL,
    
    -- Validity
    effective_date DATE NOT NULL,
    expiry_date DATE NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Coverage options
    covers_theft BOOLEAN NOT NULL DEFAULT FALSE,
    covers_vandalism BOOLEAN NOT NULL DEFAULT FALSE,
    covers_collision BOOLEAN NOT NULL DEFAULT FALSE,
    covers_comprehensive BOOLEAN NOT NULL DEFAULT FALSE,
    covers_third_party BOOLEAN NOT NULL DEFAULT TRUE,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, policy_number),
    CHECK (effective_date < expiry_date)
);

-- Insurance claims table
CREATE TABLE rental.insurance_claims (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    policy_id UUID NOT NULL REFERENCES rental.insurance_policies(id),
    claim_number VARCHAR(50) NOT NULL,
    
    -- Incident details
    incident_date TIMESTAMP WITH TIME ZONE NOT NULL,
    incident_location VARCHAR(255),
    incident_description TEXT NOT NULL,
    
    -- Claim details
    claim_amount DECIMAL(12,2) NOT NULL,
    deductible_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    settlement_amount DECIMAL(12,2),
    
    -- Status tracking
    status INTEGER NOT NULL DEFAULT 1, -- 1: Submitted, 2: UnderReview, 3: Approved, 4: Denied, 5: Settled
    submitted_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    reviewed_at TIMESTAMP WITH TIME ZONE,
    settled_at TIMESTAMP WITH TIME ZONE,
    
    -- External reference
    insurance_company_claim_number VARCHAR(50),
    adjuster_name VARCHAR(100),
    adjuster_contact VARCHAR(100),
    
    notes TEXT,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, claim_number)
);

-- Customer documents table
CREATE TABLE rental.customer_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES rental.rental_customers(id) ON DELETE CASCADE,
    type INTEGER NOT NULL, -- 1: DriverLicense, 2: Passport, 3: IdentityCard, etc.
    file_name VARCHAR(255) NOT NULL,
    file_url VARCHAR(500) NOT NULL,
    file_id UUID NOT NULL, -- References files module
    
    is_verified BOOLEAN NOT NULL DEFAULT FALSE,
    verified_at TIMESTAMP WITH TIME ZONE,
    verified_by UUID, -- References users.users(id)
    verification_notes TEXT,
    
    uploaded_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    uploaded_by UUID NOT NULL, -- References users.users(id)
    expiry_date DATE,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Payment methods table
CREATE TABLE rental.payment_methods (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL REFERENCES rental.rental_customers(id) ON DELETE CASCADE,
    type INTEGER NOT NULL, -- 1: CreditCard, 2: DebitCard, 3: BankAccount, 4: PayPal, 5: ApplePay
    
    -- Card/Account details (encrypted/tokenized)
    masked_number VARCHAR(20), -- e.g., "**** **** **** 1234"
    expiry_month INTEGER,
    expiry_year INTEGER,
    cardholder_name VARCHAR(100),
    
    -- Payment processor details
    payment_processor VARCHAR(50), -- Stripe, PayPal, etc.
    external_payment_method_id VARCHAR(100), -- Token from payment processor
    
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    is_verified BOOLEAN NOT NULL DEFAULT FALSE,
    verified_at TIMESTAMP WITH TIME ZONE,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Photos tables
CREATE TABLE rental.handover_photos (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    handover_id UUID NOT NULL REFERENCES rental.vehicle_handovers(id) ON DELETE CASCADE,
    photo_url VARCHAR(500) NOT NULL,
    photo_type INTEGER NOT NULL, -- 1: Overview, 2: FrontView, etc.
    description TEXT,
    taken_by UUID NOT NULL, -- References users.users(id)
    taken_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE TABLE rental.inspection_photos (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    inspection_id UUID NOT NULL REFERENCES rental.vehicle_inspections(id) ON DELETE CASCADE,
    photo_url VARCHAR(500) NOT NULL,
    photo_type INTEGER NOT NULL,
    description TEXT,
    taken_by UUID NOT NULL,
    taken_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE TABLE rental.damage_photos (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    damage_report_id UUID NOT NULL REFERENCES rental.damage_reports(id) ON DELETE CASCADE,
    photo_url VARCHAR(500) NOT NULL,
    description TEXT,
    taken_by UUID NOT NULL,
    taken_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Additional charges table
CREATE TABLE rental.additional_charges (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    contract_id UUID NOT NULL REFERENCES rental.rental_contracts(id) ON DELETE CASCADE,
    type INTEGER NOT NULL, -- 1: ExcessMileage, 2: FuelCharge, 3: CleaningFee, 4: DamageCharge, 5: LateReturn
    amount DECIMAL(10,2) NOT NULL,
    description TEXT NOT NULL,
    charged_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    charged_by UUID NOT NULL, -- References users.users(id)
    
    CHECK (amount >= 0)
);

-- Contract extensions table
CREATE TABLE rental.contract_extensions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    contract_id UUID NOT NULL REFERENCES rental.rental_contracts(id) ON DELETE CASCADE,
    original_end_date TIMESTAMP WITH TIME ZONE NOT NULL,
    new_end_date TIMESTAMP WITH TIME ZONE NOT NULL,
    extension_rate DECIMAL(10,2) NOT NULL,
    extended_by UUID NOT NULL, -- References users.users(id)
    extended_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CHECK (new_end_date > original_end_date)
);

-- Vehicle availability periods table
CREATE TABLE rental.vehicle_availability_periods (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    vehicle_id UUID NOT NULL REFERENCES rental.rental_vehicles(id) ON DELETE CASCADE,
    start_date TIMESTAMP WITH TIME ZONE NOT NULL,
    end_date TIMESTAMP WITH TIME ZONE NOT NULL,
    is_unavailable BOOLEAN NOT NULL DEFAULT FALSE,
    reason VARCHAR(255),
    created_by UUID NOT NULL, -- References users.users(id)
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
### 4.4 Digital Handover Process

```csharp
namespace DriveOps.Rental.Application.Services
{
    public class DigitalHandoverService
    {
        private readonly IVehicleHandoverRepository _handoverRepository;
        private readonly IVehicleInspectionRepository _inspectionRepository;
        private readonly IPhotoStorageService _photoStorage;
        private readonly ISignatureService _signatureService;
        private readonly INotificationService _notificationService;

        public async Task<Result<VehicleHandoverDto>> StartHandoverProcessAsync(
            StartHandoverCommand command)
        {
            // Create handover record
            var handover = VehicleHandover.Create(
                TenantId.From(Guid.Parse(command.TenantId)),
                RentalVehicleId.From(Guid.Parse(command.VehicleId)),
                RentalCustomerId.From(Guid.Parse(command.CustomerId)),
                command.Type,
                command.ScheduledDateTime,
                RentalLocationId.From(Guid.Parse(command.LocationId)),
                UserId.From(Guid.Parse(command.StaffId))
            );

            // Start handover
            var startResult = handover.StartHandover(
                command.CurrentMileage,
                command.FuelLevel,
                UserId.From(Guid.Parse(command.StaffId))
            );

            if (!startResult.IsSuccess)
                return Result<VehicleHandoverDto>.Failure(startResult.Error);

            // Create initial vehicle inspection
            var inspection = VehicleInspection.Create(
                TenantId.From(Guid.Parse(command.TenantId)),
                RentalVehicleId.From(Guid.Parse(command.VehicleId)),
                handover.Id,
                command.Type == HandoverType.Pickup ? InspectionType.PreRental : InspectionType.PostRental,
                DateTime.UtcNow,
                UserId.From(Guid.Parse(command.StaffId)),
                command.CurrentMileage,
                command.FuelLevel
            );

            // Save to repository
            await _handoverRepository.AddAsync(handover);
            await _inspectionRepository.AddAsync(inspection);

            // Send notification to customer
            await _notificationService.SendHandoverStartedNotificationAsync(
                handover.CustomerId, handover.Id);

            return Result<VehicleHandoverDto>.Success(_mapper.Map<VehicleHandoverDto>(handover));
        }

        public async Task<Result> AddHandoverPhotoAsync(AddHandoverPhotoCommand command)
        {
            var handover = await _handoverRepository.GetByIdAsync(
                VehicleHandoverId.From(Guid.Parse(command.HandoverId)));

            if (handover == null)
                return Result.Failure("Handover not found");

            // Upload photo to storage
            var photoUrl = await _photoStorage.UploadPhotoAsync(
                command.Photo,
                $"handovers/{handover.Id}/photos",
                command.PhotoType.ToString().ToLower()
            );

            // Add photo to handover
            var result = handover.AddPhoto(
                photoUrl,
                command.PhotoType,
                command.Description,
                UserId.From(Guid.Parse(command.UserId))
            );

            if (!result.IsSuccess)
                return result;

            await _handoverRepository.UpdateAsync(handover);
            return Result.Success();
        }

        public async Task<Result> CompleteHandoverAsync(CompleteHandoverCommand command)
        {
            var handover = await _handoverRepository.GetByIdAsync(
                VehicleHandoverId.From(Guid.Parse(command.HandoverId)));

            if (handover == null)
                return Result.Failure("Handover not found");

            // Process digital signature
            var signatureUrl = await _signatureService.ProcessSignatureAsync(
                command.CustomerSignature, handover.Id);

            // Complete handover
            var result = handover.CompleteHandover(
                command.OverallCondition,
                signatureUrl,
                command.Notes,
                UserId.From(Guid.Parse(command.UserId))
            );

            if (!result.IsSuccess)
                return result;

            await _handoverRepository.UpdateAsync(handover);

            // Send completion notification
            await _notificationService.SendHandoverCompletedNotificationAsync(
                handover.CustomerId, handover.Id);

            return Result.Success();
        }
    }
}
```

---

## 5. Integration Points

### 5.1 Core Modules Integration

#### Integration with Users Module
```csharp
namespace DriveOps.Rental.Infrastructure.Integration
{
    public class UsersModuleIntegration
    {
        private readonly IUserServiceClient _userServiceClient;
        
        public async Task<UserProfile> GetUserProfileAsync(UserId userId)
        {
            return await _userServiceClient.GetUserProfileAsync(userId.Value.ToString());
        }
        
        public async Task<bool> ValidateUserPermissionsAsync(UserId userId, string permission)
        {
            return await _userServiceClient.HasPermissionAsync(userId.Value.ToString(), permission);
        }
    }
}
```

#### Integration with Vehicles Module
```csharp
namespace DriveOps.Rental.Infrastructure.Integration
{
    public class VehiclesModuleIntegration
    {
        private readonly IVehicleServiceClient _vehicleServiceClient;
        
        public async Task<VehicleDetails> GetVehicleDetailsAsync(VehicleId vehicleId)
        {
            return await _vehicleServiceClient.GetVehicleDetailsAsync(vehicleId.Value.ToString());
        }
        
        public async Task<Result> UpdateVehicleStatusAsync(VehicleId vehicleId, VehicleStatus status)
        {
            return await _vehicleServiceClient.UpdateVehicleStatusAsync(
                vehicleId.Value.ToString(), status);
        }
    }
}
```

#### Integration with Payments Module
```csharp
namespace DriveOps.Rental.Infrastructure.Integration
{
    public class PaymentsModuleIntegration
    {
        private readonly IPaymentServiceClient _paymentServiceClient;
        
        public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
        {
            return await _paymentServiceClient.ProcessPaymentAsync(request);
        }
        
        public async Task<Result> AuthorizeSecurityDepositAsync(
            decimal amount, 
            string paymentMethodId, 
            string customerId)
        {
            var authRequest = new AuthorizationRequest
            {
                Amount = amount,
                PaymentMethodId = paymentMethodId,
                CustomerId = customerId,
                Description = "Rental security deposit authorization",
                HoldDuration = TimeSpan.FromDays(30)
            };
            
            return await _paymentServiceClient.AuthorizePaymentAsync(authRequest);
        }
    }
}
```

### 5.2 External Service Integrations

#### Payment Gateway Integration (Stripe)
```csharp
namespace DriveOps.Rental.Infrastructure.ExternalServices
{
    public class StripePaymentService : IPaymentProcessorService
    {
        private readonly StripeClient _stripeClient;
        private readonly ILogger<StripePaymentService> _logger;

        public async Task<PaymentResult> ProcessRentalPaymentAsync(RentalPaymentRequest request)
        {
            try
            {
                var paymentIntentOptions = new PaymentIntentCreateOptions
                {
                    Amount = (long)(request.Amount * 100), // Convert to cents
                    Currency = request.Currency.ToLower(),
                    PaymentMethod = request.PaymentMethodId,
                    Customer = request.CustomerId,
                    Confirm = true,
                    Metadata = new Dictionary<string, string>
                    {
                        { "tenant_id", request.TenantId },
                        { "reservation_id", request.ReservationId },
                        { "rental_type", "booking_payment" }
                    }
                };

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(paymentIntentOptions);

                return new PaymentResult
                {
                    IsSuccess = paymentIntent.Status == "succeeded",
                    TransactionId = paymentIntent.Id,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    ProcessedAt = DateTime.UtcNow,
                    GatewayResponse = paymentIntent.ToJson()
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe payment failed for reservation {ReservationId}", request.ReservationId);
                return PaymentResult.Failed(ex.Message);
            }
        }

        public async Task<AuthorizationResult> AuthorizeSecurityDepositAsync(SecurityDepositRequest request)
        {
            var setupIntentOptions = new SetupIntentCreateOptions
            {
                Customer = request.CustomerId,
                PaymentMethod = request.PaymentMethodId,
                Confirm = true,
                Usage = "off_session",
                Metadata = new Dictionary<string, string>
                {
                    { "tenant_id", request.TenantId },
                    { "deposit_amount", request.Amount.ToString() },
                    { "authorization_type", "security_deposit" }
                }
            };

            var service = new SetupIntentService();
            var setupIntent = await service.CreateAsync(setupIntentOptions);

            return new AuthorizationResult
            {
                IsSuccess = setupIntent.Status == "succeeded",
                AuthorizationId = setupIntent.Id,
                Amount = request.Amount,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
        }
    }
}
```

#### Insurance API Integration
```csharp
namespace DriveOps.Rental.Infrastructure.ExternalServices
{
    public class InsuranceApiService : IInsuranceService
    {
        private readonly HttpClient _httpClient;
        private readonly InsuranceApiOptions _options;

        public async Task<InsuranceQuoteResult> GetInsuranceQuoteAsync(InsuranceQuoteRequest request)
        {
            var apiRequest = new
            {
                vehicle = new
                {
                    make = request.VehicleMake,
                    model = request.VehicleModel,
                    year = request.VehicleYear,
                    vin = request.VehicleVin
                },
                driver = new
                {
                    age = request.DriverAge,
                    licenseNumber = request.DriverLicenseNumber,
                    drivingExperience = request.DrivingExperienceYears
                },
                coverage = new
                {
                    rentalPeriod = request.RentalPeriodDays,
                    coverage_type = request.CoverageType,
                    deductible = request.PreferredDeductible
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/quotes", apiRequest);
            
            if (response.IsSuccessStatusCode)
            {
                var quote = await response.Content.ReadFromJsonAsync<InsuranceQuote>();
                return InsuranceQuoteResult.Success(quote);
            }

            return InsuranceQuoteResult.Failed("Failed to get insurance quote");
        }

        public async Task<ClaimResult> SubmitInsuranceClaimAsync(InsuranceClaimRequest request)
        {
            var claimData = new
            {
                policyNumber = request.PolicyNumber,
                incidentDate = request.IncidentDate,
                incidentDescription = request.Description,
                estimatedDamage = request.EstimatedDamageAmount,
                photos = request.PhotoUrls,
                vehicleLocation = request.VehicleLocation
            };

            var response = await _httpClient.PostAsJsonAsync("/api/claims", claimData);
            
            if (response.IsSuccessStatusCode)
            {
                var claim = await response.Content.ReadFromJsonAsync<InsuranceClaim>();
                return ClaimResult.Success(claim.ClaimNumber, claim.Status);
            }

            return ClaimResult.Failed("Failed to submit insurance claim");
        }
    }
}
```

---

## 6. Mobile Applications Specifications

### 6.1 Customer Mobile App (React Native / Flutter)

#### Core Features
- **Vehicle Search & Booking**: Real-time availability search with map integration
- **Account Management**: Profile, payment methods, booking history
- **Digital Wallet**: Loyalty points, promotions, payment cards
- **Trip Management**: Active rental tracking, extension requests
- **Vehicle Controls**: Remote unlock/lock (IoT integration)
- **Digital Handover**: Photo capture, damage reporting, digital signatures
- **Support & Chat**: In-app customer support with real-time chat

#### Technical Architecture
```typescript
// React Native App Structure
interface CustomerAppState {
  user: UserProfile;
  activeRental: ActiveRental | null;
  bookingFlow: BookingFlowState;
  vehicleSearch: VehicleSearchState;
  notifications: Notification[];
}

interface BookingFlowState {
  step: 'search' | 'selection' | 'details' | 'payment' | 'confirmation';
  searchCriteria: VehicleSearchCriteria;
  selectedVehicle: VehicleCategory | null;
  pricing: PricingCalculation | null;
  customerDetails: CustomerDetails;
  paymentMethod: PaymentMethod | null;
}

// Real-time features with WebSocket
class RentalWebSocketService {
  private socket: WebSocket;
  
  connectToRental(rentalId: string) {
    this.socket = new WebSocket(`wss://api.driveops.com/rental/${rentalId}`);
    
    this.socket.onmessage = (event) => {
      const message = JSON.parse(event.data);
      
      switch (message.type) {
        case 'vehicle_location_update':
          this.updateVehicleLocation(message.data);
          break;
        case 'rental_status_change':
          this.updateRentalStatus(message.data);
          break;
        case 'handover_scheduled':
          this.showHandoverNotification(message.data);
          break;
      }
    };
  }
}

// Offline capability
class OfflineBookingService {
  async saveBookingDraft(booking: BookingDraft) {
    await AsyncStorage.setItem(`booking_draft_${booking.id}`, JSON.stringify(booking));
  }
  
  async syncPendingBookings() {
    const drafts = await this.getPendingBookingDrafts();
    
    for (const draft of drafts) {
      try {
        await this.submitBooking(draft);
        await this.removeDraft(draft.id);
      } catch (error) {
        console.log(`Failed to sync booking ${draft.id}:`, error);
      }
    }
  }
}
```

### 6.2 Fleet Manager Mobile App

#### Core Features
- **Fleet Overview**: Real-time fleet status dashboard
- **Vehicle Management**: Status updates, location tracking
- **Handover Management**: Digital inspection, photo capture
- **Maintenance Scheduling**: Schedule and track maintenance
- **Damage Assessment**: AI-powered damage detection and cost estimation
- **Customer Interaction**: Check-in/check-out process management
- **Analytics Dashboard**: Utilization rates, revenue metrics

#### Damage Assessment with AI
```typescript
// AI-powered damage detection
class DamageDetectionService {
  async analyzeVehiclePhotos(photos: PhotoInput[]): Promise<DamageAnalysisResult[]> {
    const analysisResults: DamageAnalysisResult[] = [];
    
    for (const photo of photos) {
      const formData = new FormData();
      formData.append('image', photo.file);
      formData.append('vehicle_area', photo.vehicleArea);
      
      const response = await fetch('/api/ai/damage-detection', {
        method: 'POST',
        body: formData,
        headers: {
          'Authorization': `Bearer ${this.authToken}`,
          'Content-Type': 'multipart/form-data'
        }
      });
      
      if (response.ok) {
        const analysis = await response.json();
        analysisResults.push({
          photoId: photo.id,
          damages: analysis.detected_damages.map(damage => ({
            type: damage.damage_type,
            severity: damage.severity_level,
            confidence: damage.confidence_score,
            estimatedCost: damage.estimated_repair_cost,
            boundingBox: damage.bounding_box,
            description: damage.ai_description
          })),
          overallCondition: analysis.overall_condition,
          recommendations: analysis.recommendations
        });
      }
    }
    
    return analysisResults;
  }
  
  async generateDamageReport(
    vehicleId: string, 
    damages: DamageAnalysisResult[]
  ): Promise<DamageReport> {
    const report = {
      vehicleId,
      inspectionDate: new Date(),
      damages: damages.flatMap(analysis => analysis.damages),
      totalEstimatedCost: damages.reduce((total, analysis) => 
        total + analysis.damages.reduce((sum, damage) => sum + damage.estimatedCost, 0), 0),
      aiConfidenceScore: this.calculateOverallConfidence(damages),
      requiresHumanReview: this.requiresHumanReview(damages)
    };
    
    return await this.submitDamageReport(report);
  }
}
```

---

## 7. Security & Compliance

### 7.1 Data Protection (GDPR Compliance)

```csharp
namespace DriveOps.Rental.Application.Services
{
    public class GdprComplianceService
    {
        private readonly IRentalCustomerRepository _customerRepository;
        private readonly IDataEncryptionService _encryptionService;
        private readonly IAuditLogService _auditService;

        public async Task<PersonalDataExportResult> ExportCustomerDataAsync(
            RentalCustomerId customerId,
            UserId requestedBy)
        {
            // Log data access request
            await _auditService.LogDataAccessAsync(customerId, requestedBy, "GDPR_EXPORT_REQUEST");

            var customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer == null)
                return PersonalDataExportResult.NotFound();

            // Gather all personal data
            var personalData = new PersonalDataExport
            {
                CustomerDetails = _mapper.Map<CustomerPersonalData>(customer),
                Reservations = await GetCustomerReservationsAsync(customerId),
                PaymentMethods = await GetCustomerPaymentMethodsAsync(customerId),
                Documents = await GetCustomerDocumentsAsync(customerId),
                Handovers = await GetCustomerHandoversAsync(customerId),
                LoyaltyHistory = await GetLoyaltyHistoryAsync(customerId)
            };

            // Encrypt sensitive data for export
            var encryptedExport = await _encryptionService.EncryptForExportAsync(personalData);

            return PersonalDataExportResult.Success(encryptedExport);
        }

        public async Task<DataDeletionResult> DeleteCustomerDataAsync(
            RentalCustomerId customerId,
            UserId requestedBy,
            DataDeletionReason reason)
        {
            // Validate deletion request
            var validationResult = await ValidateDataDeletionRequestAsync(customerId, reason);
            if (!validationResult.IsValid)
                return DataDeletionResult.Invalid(validationResult.Reason);

            // Log deletion request
            await _auditService.LogDataDeletionAsync(customerId, requestedBy, reason);

            // Anonymize instead of hard delete for audit compliance
            var customer = await _customerRepository.GetByIdAsync(customerId);
            customer.AnonymizePersonalData();

            // Anonymize related data
            await AnonymizeCustomerReservationsAsync(customerId);
            await AnonymizeCustomerPaymentMethodsAsync(customerId);
            await DeleteCustomerDocumentsAsync(customerId);

            await _customerRepository.UpdateAsync(customer);

            return DataDeletionResult.Success();
        }
    }
}
```

### 7.2 Payment Security (PCI Compliance)

```csharp
namespace DriveOps.Rental.Infrastructure.Security
{
    public class PciComplianceService
    {
        private readonly ITokenizationService _tokenizationService;
        private readonly IEncryptionService _encryptionService;
        
        public async Task<TokenizedPaymentMethod> TokenizePaymentMethodAsync(
            PaymentMethodDetails paymentMethod)
        {
            // Never store actual card numbers - use tokenization
            var token = await _tokenizationService.TokenizeAsync(
                paymentMethod.CardNumber,
                paymentMethod.ExpiryDate,
                paymentMethod.CardholderName
            );

            return new TokenizedPaymentMethod
            {
                Token = token,
                MaskedNumber = MaskCardNumber(paymentMethod.CardNumber),
                LastFourDigits = paymentMethod.CardNumber.Substring(paymentMethod.CardNumber.Length - 4),
                ExpiryMonth = paymentMethod.ExpiryDate.Month,
                ExpiryYear = paymentMethod.ExpiryDate.Year,
                CardBrand = DetectCardBrand(paymentMethod.CardNumber)
            };
        }

        public async Task<PaymentProcessingResult> ProcessSecurePaymentAsync(
            SecurePaymentRequest request)
        {
            // All payment processing through secure, PCI-compliant gateway
            var encryptedRequest = await _encryptionService.EncryptPaymentRequestAsync(request);
            
            // Use payment processor's secure API
            var result = await _paymentProcessor.ProcessPaymentAsync(encryptedRequest);
            
            // Log transaction (without sensitive data)
            await LogSecureTransactionAsync(request.TransactionId, result.Status);
            
            return result;
        }

        private string MaskCardNumber(string cardNumber)
        {
            if (cardNumber.Length < 8)
                return new string('*', cardNumber.Length);
                
            var firstFour = cardNumber.Substring(0, 4);
            var lastFour = cardNumber.Substring(cardNumber.Length - 4);
            var middle = new string('*', cardNumber.Length - 8);
            
            return $"{firstFour}{middle}{lastFour}";
        }
    }
}
```

### 7.3 Fraud Detection

```csharp
namespace DriveOps.Rental.Application.Services
{
    public class FraudDetectionService
    {
        private readonly ICustomerBehaviorAnalyzer _behaviorAnalyzer;
        private readonly IGeolocationService _geolocationService;
        private readonly IRiskScoringEngine _riskEngine;

        public async Task<FraudAssessmentResult> AssessBookingRiskAsync(
            CreateReservationCommand command)
        {
            var riskFactors = new List<RiskFactor>();
            var riskScore = 0;

            // Customer behavior analysis
            var behaviorRisk = await _behaviorAnalyzer.AnalyzeCustomerBehaviorAsync(
                RentalCustomerId.From(Guid.Parse(command.CustomerId)));
            riskFactors.AddRange(behaviorRisk.RiskFactors);
            riskScore += behaviorRisk.Score;

            // Geolocation analysis
            var locationRisk = await AnalyzeLocationRiskAsync(command);
            riskFactors.AddRange(locationRisk.RiskFactors);
            riskScore += locationRisk.Score;

            // Booking pattern analysis
            var patternRisk = await AnalyzeBookingPatternAsync(command);
            riskFactors.AddRange(patternRisk.RiskFactors);
            riskScore += patternRisk.Score;

            // Payment method analysis
            var paymentRisk = await AnalyzePaymentMethodRiskAsync(command);
            riskFactors.AddRange(paymentRisk.RiskFactors);
            riskScore += paymentRisk.Score;

            var riskLevel = CalculateRiskLevel(riskScore);
            var requiresManualReview = riskLevel >= RiskLevel.High;

            return new FraudAssessmentResult
            {
                RiskScore = riskScore,
                RiskLevel = riskLevel,
                RiskFactors = riskFactors,
                RequiresManualReview = requiresManualReview,
                RecommendedActions = GenerateRecommendedActions(riskLevel, riskFactors)
            };
        }

        private async Task<LocationRiskAssessment> AnalyzeLocationRiskAsync(
            CreateReservationCommand command)
        {
            var riskFactors = new List<RiskFactor>();
            var score = 0;

            // Check if pickup location is in high-risk area
            var pickupLocation = await _geolocationService.GetLocationDetailsAsync(command.PickupLocationId);
            if (pickupLocation.IsHighRiskArea)
            {
                riskFactors.Add(new RiskFactor("HIGH_RISK_PICKUP_LOCATION", 15));
                score += 15;
            }

            // Check for unusual location patterns
            var customer = await _customerRepository.GetByIdAsync(
                RentalCustomerId.From(Guid.Parse(command.CustomerId)));
            
            var customerHistory = await _reservationRepository.GetCustomerLocationHistoryAsync(customer.Id);
            
            var isUnusualLocation = !customerHistory.Any(h => 
                _geolocationService.CalculateDistance(h.PickupLocation, pickupLocation) < 50); // 50km radius
                
            if (isUnusualLocation && customerHistory.Count > 5)
            {
                riskFactors.Add(new RiskFactor("UNUSUAL_LOCATION_PATTERN", 10));
                score += 10;
            }

            return new LocationRiskAssessment { RiskFactors = riskFactors, Score = score };
        }

        private List<string> GenerateRecommendedActions(RiskLevel riskLevel, List<RiskFactor> riskFactors)
        {
            var actions = new List<string>();

            if (riskLevel >= RiskLevel.Medium)
            {
                actions.Add("REQUIRE_ADDITIONAL_IDENTITY_VERIFICATION");
                actions.Add("INCREASE_SECURITY_DEPOSIT");
            }

            if (riskLevel >= RiskLevel.High)
            {
                actions.Add("MANUAL_REVIEW_REQUIRED");
                actions.Add("REQUIRE_IN_PERSON_PICKUP");
                actions.Add("ENHANCED_VEHICLE_TRACKING");
            }

            if (riskFactors.Any(rf => rf.Type == "PAYMENT_METHOD_RISK"))
            {
                actions.Add("REQUIRE_ALTERNATIVE_PAYMENT_METHOD");
            }

            return actions;
        }
    }
}
```

---

## 8. Performance Optimization

### 8.1 High-Performance Availability Queries

```sql
-- Optimized availability query with materialized view
CREATE MATERIALIZED VIEW rental.vehicle_availability_matrix AS
SELECT 
    v.tenant_id,
    v.category_id,
    v.id as vehicle_id,
    v.status,
    v.is_available_for_rental,
    COALESCE(next_unavailable.start_date, '2099-12-31'::date) as next_unavailable_date,
    COALESCE(next_available.end_date, '1900-01-01'::date) as available_from_date
FROM rental.rental_vehicles v
LEFT JOIN (
    SELECT vehicle_id, MIN(start_date) as start_date
    FROM rental.vehicle_availability_periods 
    WHERE is_unavailable = true AND start_date > CURRENT_DATE
    GROUP BY vehicle_id
) next_unavailable ON v.id = next_unavailable.vehicle_id
LEFT JOIN (
    SELECT vehicle_id, MAX(end_date) as end_date
    FROM rental.vehicle_availability_periods 
    WHERE is_unavailable = true AND end_date <= CURRENT_DATE
    GROUP BY vehicle_id
) next_available ON v.id = next_available.vehicle_id
WHERE v.status != 6; -- Exclude out-of-service vehicles

-- Create indexes for optimal performance
CREATE INDEX idx_availability_matrix_category_dates 
ON rental.vehicle_availability_matrix(tenant_id, category_id, next_unavailable_date, available_from_date);

-- Refresh materialized view periodically
CREATE OR REPLACE FUNCTION refresh_availability_matrix()
RETURNS void AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY rental.vehicle_availability_matrix;
END;
$$ LANGUAGE plpgsql;

-- Fast availability check function
CREATE OR REPLACE FUNCTION check_category_availability(
    p_tenant_id UUID,
    p_category_id UUID,
    p_start_date TIMESTAMP WITH TIME ZONE,
    p_end_date TIMESTAMP WITH TIME ZONE
)
RETURNS TABLE(
    total_vehicles INT,
    available_vehicles INT,
    vehicle_ids UUID[]
) AS $$
BEGIN
    RETURN QUERY
    WITH available_vehicles AS (
        SELECT v.vehicle_id
        FROM rental.vehicle_availability_matrix v
        WHERE v.tenant_id = p_tenant_id
          AND v.category_id = p_category_id
          AND v.is_available_for_rental = true
          AND v.status = 1 -- Available status
          AND (v.available_from_date <= p_start_date::date)
          AND (v.next_unavailable_date > p_end_date::date)
          AND NOT EXISTS (
              SELECT 1 FROM rental.reservations r
              WHERE r.vehicle_id = v.vehicle_id
                AND r.status IN (2, 3) -- Confirmed or InProgress
                AND r.pickup_datetime < p_end_date
                AND r.return_datetime > p_start_date
          )
    ),
    category_totals AS (
        SELECT COUNT(*) as total_count
        FROM rental.vehicle_availability_matrix v
        WHERE v.tenant_id = p_tenant_id
          AND v.category_id = p_category_id
          AND v.status != 6 -- Exclude out-of-service
    )
    SELECT 
        category_totals.total_count::INT,
        COUNT(available_vehicles.vehicle_id)::INT,
        ARRAY_AGG(available_vehicles.vehicle_id) FILTER (WHERE available_vehicles.vehicle_id IS NOT NULL)
    FROM category_totals
    LEFT JOIN available_vehicles ON true
    GROUP BY category_totals.total_count;
END;
$$ LANGUAGE plpgsql;
```

### 8.2 Caching Strategy

```csharp
namespace DriveOps.Rental.Infrastructure.Caching
{
    public class RentalCacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<RentalCacheService> _logger;

        public async Task<VehicleAvailabilityDto?> GetCachedAvailabilityAsync(
            TenantId tenantId,
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate)
        {
            var cacheKey = GenerateAvailabilityCacheKey(tenantId, categoryId, startDate, endDate);
            
            // Try memory cache first (fastest)
            if (_memoryCache.TryGetValue(cacheKey, out VehicleAvailabilityDto? memoryResult))
            {
                return memoryResult;
            }

            // Try distributed cache (Redis)
            var distributedResult = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(distributedResult))
            {
                var availability = JsonSerializer.Deserialize<VehicleAvailabilityDto>(distributedResult);
                
                // Store in memory cache for faster subsequent access
                _memoryCache.Set(cacheKey, availability, TimeSpan.FromMinutes(2));
                
                return availability;
            }

            return null;
        }

        public async Task SetAvailabilityCacheAsync(
            TenantId tenantId,
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            VehicleAvailabilityDto availability)
        {
            var cacheKey = GenerateAvailabilityCacheKey(tenantId, categoryId, startDate, endDate);
            var serializedData = JsonSerializer.Serialize(availability);

            // Store in both caches
            var distributedOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                SlidingExpiration = TimeSpan.FromMinutes(2)
            };

            var memoryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
                Priority = CacheItemPriority.High
            };

            await _distributedCache.SetStringAsync(cacheKey, serializedData, distributedOptions);
            _memoryCache.Set(cacheKey, availability, memoryOptions);
        }

        public async Task InvalidateAvailabilityCacheAsync(
            TenantId tenantId,
            VehicleCategoryId categoryId,
            DateTime affectedStartDate,
            DateTime affectedEndDate)
        {
            // Invalidate all cache entries that could be affected by this change
            var baseKey = $"availability:{tenantId}:{categoryId}";
            
            // Use a cache invalidation pattern or tag-based invalidation if supported
            await InvalidateCachePatternAsync(baseKey);
            
            _logger.LogInformation("Invalidated availability cache for tenant {TenantId}, category {CategoryId}", 
                tenantId, categoryId);
        }

        public async Task<PricingCalculation?> GetCachedPricingAsync(
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            CustomerTier customerTier)
        {
            var cacheKey = $"pricing:{categoryId}:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}:{customerTier}";
            
            var cachedPricing = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedPricing))
            {
                var pricing = JsonSerializer.Deserialize<PricingCalculation>(cachedPricing);
                
                // Check if pricing is still valid (not expired)
                if (pricing?.ValidUntil > DateTime.UtcNow)
                {
                    return pricing;
                }
            }

            return null;
        }

        private string GenerateAvailabilityCacheKey(
            TenantId tenantId,
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate)
        {
            return $"availability:{tenantId}:{categoryId}:{startDate:yyyyMMddHH}:{endDate:yyyyMMddHH}";
        }

        private async Task InvalidateCachePatternAsync(string pattern)
        {
            // Implementation depends on cache provider
            // For Redis, you could use SCAN with pattern matching
            // For in-memory cache, maintain a registry of related keys
        }
    }

    // Cache warming service for peak periods
    public class CacheWarmingService : IHostedService
    {
        private readonly IRentalCacheService _cacheService;
        private readonly IAvailabilityService _availabilityService;
        private readonly Timer _timer;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Warm cache every 5 minutes during business hours
            _timer = new Timer(WarmCache, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        private async void WarmCache(object? state)
        {
            try
            {
                var currentHour = DateTime.Now.Hour;
                
                // Only warm cache during business hours (6 AM - 11 PM)
                if (currentHour >= 6 && currentHour <= 23)
                {
                    await WarmPopularSearchesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error warming cache");
            }
        }

        private async Task WarmPopularSearchesAsync()
        {
            // Pre-calculate availability for popular date ranges and categories
            var popularDateRanges = GetPopularDateRanges();
            var popularCategories = await GetPopularCategoriesAsync();

            foreach (var dateRange in popularDateRanges)
            {
                foreach (var category in popularCategories)
                {
                    var availability = await _availabilityService.CheckAvailabilityAsync(
                        category.TenantId, category.Id, dateRange.Start, dateRange.End);
                        
                    if (availability.IsSuccess)
                    {
                        await _cacheService.SetAvailabilityCacheAsync(
                            category.TenantId, category.Id, dateRange.Start, dateRange.End, availability.Value);
                    }
                }
            }
        }
    }
}
```

---

## 9. Conclusion

The RENTAL module represents a comprehensive vehicle rental and sharing management solution that transforms DriveOps into a complete rental platform. With its sophisticated booking engine, dynamic pricing algorithms, digital handover processes, and advanced fleet optimization, it provides everything needed to operate a modern rental business.

### Key Benefits

1. **Complete Rental Operations**: From initial booking to final handover, every aspect of the rental process is digitized and optimized
2. **Revenue Optimization**: Dynamic pricing and yield management maximize profitability
3. **Operational Efficiency**: Automated workflows and mobile apps reduce manual work and errors
4. **Customer Experience**: Self-service booking, digital handovers, and mobile apps provide superior customer experience
5. **Scalability**: Cloud-native architecture supports growth from small operations to enterprise fleets
6. **Compliance**: Built-in GDPR, PCI, and automotive industry compliance features

### Technical Excellence

- **Domain-Driven Design**: Clean architecture with proper aggregates and business logic encapsulation
- **Event-Driven Architecture**: Real-time updates and loose coupling between components
- **Performance Optimization**: Advanced caching, database optimization, and async processing
- **Security First**: Comprehensive security measures including fraud detection and data protection
- **Mobile-First**: Native mobile applications for customers and fleet managers
- **Integration Ready**: Seamless integration with payment gateways, insurance providers, and IoT systems

This module positions DriveOps as a leading rental management platform capable of serving everything from peer-to-peer car sharing to enterprise fleet operations, making it a valuable €59/month premium offering for automotive industry clients.
    CHECK (start_date < end_date)
);

-- Performance indexes
CREATE INDEX idx_reservations_tenant_customer ON rental.reservations(tenant_id, customer_id);
CREATE INDEX idx_reservations_tenant_vehicle ON rental.reservations(tenant_id, vehicle_id);
CREATE INDEX idx_reservations_pickup_return_dates ON rental.reservations(pickup_datetime, return_datetime);
CREATE INDEX idx_reservations_status ON rental.reservations(tenant_id, status);

CREATE INDEX idx_rental_vehicles_tenant_category ON rental.rental_vehicles(tenant_id, category_id);
CREATE INDEX idx_rental_vehicles_status ON rental.rental_vehicles(tenant_id, status);
CREATE INDEX idx_rental_vehicles_location ON rental.rental_vehicles(tenant_id, current_location_id);
CREATE INDEX idx_rental_vehicles_availability ON rental.rental_vehicles(tenant_id, is_available_for_rental);

CREATE INDEX idx_rental_customers_status ON rental.rental_customers(tenant_id, status);
CREATE INDEX idx_rental_customers_tier ON rental.rental_customers(tenant_id, tier);

CREATE INDEX idx_vehicle_handovers_vehicle ON rental.vehicle_handovers(tenant_id, vehicle_id);
CREATE INDEX idx_vehicle_handovers_customer ON rental.vehicle_handovers(tenant_id, customer_id);
CREATE INDEX idx_vehicle_handovers_scheduled_date ON rental.vehicle_handovers(scheduled_datetime);

CREATE INDEX idx_damage_reports_vehicle ON rental.damage_reports(tenant_id, vehicle_id);
CREATE INDEX idx_damage_reports_status ON rental.damage_reports(tenant_id, status);

CREATE INDEX idx_tariff_plans_validity ON rental.tariff_plans(tenant_id, valid_from, valid_until, is_active);
CREATE INDEX idx_seasonal_pricing_dates ON rental.seasonal_pricing(start_date, end_date);

CREATE INDEX idx_promo_codes_code ON rental.promo_codes(tenant_id, code, is_active);
CREATE INDEX idx_promo_codes_validity ON rental.promo_codes(valid_from, valid_until, is_active);

-- Full-text search indexes
CREATE INDEX idx_rental_customers_search ON rental.rental_customers 
    USING gin(to_tsvector('english', first_name || ' ' || last_name || ' ' || email));

CREATE INDEX idx_vehicle_categories_search ON rental.vehicle_categories 
    USING gin(to_tsvector('english', name || ' ' || description));
```

---

## 4. Key Features Implementation

### 4.1 Online Booking System

The online booking system provides real-time availability checking and instant reservations with the following key components:

#### Real-time Availability Engine
```csharp
namespace DriveOps.Rental.Application.Services
{
    public class RealTimeAvailabilityEngine
    {
        private readonly IMemoryCache _cache;
        private readonly IHubContext<AvailabilityHub> _hubContext;
        private readonly IAvailabilityService _availabilityService;

        public async Task<AvailabilityMatrix> GetAvailabilityMatrixAsync(
            TenantId tenantId,
            DateTime startDate,
            DateTime endDate,
            List<VehicleCategoryId> categoryIds)
        {
            var matrix = new AvailabilityMatrix(startDate, endDate);
            
            foreach (var categoryId in categoryIds)
            {
                var availability = await _availabilityService.CheckAvailabilityAsync(
                    tenantId, categoryId, startDate, endDate);
                    
                matrix.AddCategoryAvailability(categoryId, availability.Value);
            }
            
            // Cache for 2 minutes to handle concurrent requests
            _cache.Set($"availability_matrix_{tenantId}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}", 
                matrix, TimeSpan.FromMinutes(2));
                
            return matrix;
        }

        public async Task NotifyAvailabilityChangeAsync(
            TenantId tenantId,
            VehicleCategoryId categoryId,
            DateTime affectedStartDate,
            DateTime affectedEndDate)
        {
            // Invalidate cache
            var cacheKeys = GetRelatedCacheKeys(tenantId, categoryId, affectedStartDate, affectedEndDate);
            foreach (var key in cacheKeys)
            {
                _cache.Remove(key);
            }

            // Notify connected clients via SignalR
            await _hubContext.Clients.Group($"tenant_{tenantId}")
                .SendAsync("AvailabilityChanged", new
                {
                    CategoryId = categoryId.Value,
                    StartDate = affectedStartDate,
                    EndDate = affectedEndDate
                });
        }
    }
}
```

#### Booking Workflow State Machine
```csharp
namespace DriveOps.Rental.Domain.StateMachines
{
    public class BookingWorkflow
    {
        public enum BookingState
        {
            AvailabilityCheck,
            PricingCalculation,
            CustomerValidation,
            VehicleReservation,
            PaymentProcessing,
            BookingConfirmation,
            BookingCompleted,
            BookingFailed
        }

        public class BookingContext
        {
            public ReservationId ReservationId { get; set; }
            public VehicleCategoryId CategoryId { get; set; }
            public DateTime PickupDateTime { get; set; }
            public DateTime ReturnDateTime { get; set; }
            public RentalCustomerId CustomerId { get; set; }
            public PricingCalculation? Pricing { get; set; }
            public string? ErrorMessage { get; set; }
            public Dictionary<string, object> Data { get; set; } = new();
        }

        private readonly Dictionary<BookingState, Func<BookingContext, Task<BookingState>>> _transitions;

        public BookingWorkflow()
        {
            _transitions = new Dictionary<BookingState, Func<BookingContext, Task<BookingState>>>
            {
                { BookingState.AvailabilityCheck, CheckAvailabilityAsync },
                { BookingState.PricingCalculation, CalculatePricingAsync },
                { BookingState.CustomerValidation, ValidateCustomerAsync },
                { BookingState.VehicleReservation, ReserveVehicleAsync },
                { BookingState.PaymentProcessing, ProcessPaymentAsync },
                { BookingState.BookingConfirmation, ConfirmBookingAsync }
            };
        }

        public async Task<BookingResult> ExecuteWorkflowAsync(BookingContext context)
        {
            var currentState = BookingState.AvailabilityCheck;
            
            while (currentState != BookingState.BookingCompleted && currentState != BookingState.BookingFailed)
            {
                if (_transitions.TryGetValue(currentState, out var transition))
                {
                    currentState = await transition(context);
                }
                else
                {
                    context.ErrorMessage = $"Unknown state: {currentState}";
                    currentState = BookingState.BookingFailed;
                }
            }
            
            return new BookingResult
            {
                IsSuccess = currentState == BookingState.BookingCompleted,
                ReservationId = context.ReservationId,
                ErrorMessage = context.ErrorMessage
            };
        }

        private async Task<BookingState> CheckAvailabilityAsync(BookingContext context)
        {
            // Implementation for availability checking
            // Return next state based on availability
            return BookingState.PricingCalculation;
        }

        private async Task<BookingState> CalculatePricingAsync(BookingContext context)
        {
            // Implementation for pricing calculation
            return BookingState.CustomerValidation;
        }

        // Additional state transition methods...
    }
}
```

### 4.2 Dynamic Pricing Engine

The dynamic pricing engine calculates optimal rental rates based on multiple factors:

#### Pricing Algorithm Components
```csharp
namespace DriveOps.Rental.Application.Services
{
    public class DynamicPricingEngine
    {
        private readonly ITariffPlanRepository _tariffRepository;
        private readonly IMarketDataService _marketDataService;
        private readonly IWeatherService _weatherService;
        private readonly IEventDataService _eventDataService;

        public async Task<PricingCalculation> CalculateOptimalPricingAsync(
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            RentalLocationId pickupLocationId)
        {
            // Base pricing from tariff plan
            var basePricing = await GetBasePricingAsync(categoryId, startDate, endDate);
            
            // Apply dynamic multipliers
            var demandMultiplier = await CalculateDemandMultiplierAsync(categoryId, startDate, endDate);
            var seasonalMultiplier = CalculateSeasonalMultiplier(startDate, endDate);
            var eventMultiplier = await CalculateEventMultiplierAsync(pickupLocationId, startDate, endDate);
            var weatherMultiplier = await CalculateWeatherMultiplierAsync(pickupLocationId, startDate);
            var competitorMultiplier = await CalculateCompetitorMultiplierAsync(categoryId, startDate, endDate);
            
            // Inventory pressure multiplier (increases price when low availability)
            var inventoryMultiplier = await CalculateInventoryPressureAsync(categoryId, startDate, endDate);
            
            // Combined multiplier (with bounds to prevent extreme pricing)
            var totalMultiplier = Math.Max(0.5m, Math.Min(3.0m, 
                demandMultiplier * seasonalMultiplier * eventMultiplier * 
                weatherMultiplier * competitorMultiplier * inventoryMultiplier));
            
            var adjustedPricing = basePricing.ApplyMultiplier(totalMultiplier);
            
            // Log pricing decision for analysis
            await LogPricingDecisionAsync(categoryId, startDate, endDate, new PricingFactors
            {
                BasePricing = basePricing,
                DemandMultiplier = demandMultiplier,
                SeasonalMultiplier = seasonalMultiplier,
                EventMultiplier = eventMultiplier,
                WeatherMultiplier = weatherMultiplier,
                CompetitorMultiplier = competitorMultiplier,
                InventoryMultiplier = inventoryMultiplier,
                FinalMultiplier = totalMultiplier,
                FinalPricing = adjustedPricing
            });
            
            return adjustedPricing;
        }

        private async Task<decimal> CalculateDemandMultiplierAsync(
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate)
        {
            // Analyze booking patterns and demand trends
            var historicalDemand = await _marketDataService.GetHistoricalDemandAsync(
                categoryId, startDate.Month, startDate.DayOfWeek);
                
            var currentDemand = await _marketDataService.GetCurrentDemandAsync(
                categoryId, startDate, endDate);
                
            // Calculate demand ratio (current vs historical average)
            var demandRatio = currentDemand / Math.Max(historicalDemand, 0.1m);
            
            // Convert to multiplier with diminishing returns
            return 1.0m + (decimal)Math.Log(Math.Max(demandRatio, 0.1)) * 0.2m;
        }

        private async Task<decimal> CalculateInventoryPressureAsync(
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate)
        {
            var availability = await _availabilityService.CheckAvailabilityAsync(
                categoryId.TenantId, categoryId, startDate, endDate);
                
            if (!availability.IsSuccess || availability.Value.TotalCount == 0)
                return 1.0m;
                
            var utilizationRate = 1.0m - ((decimal)availability.Value.AvailableCount / availability.Value.TotalCount);
            
            // Exponential pricing increase as availability decreases
            return utilizationRate switch
            {
                >= 0.9m => 2.0m,  // 90%+ utilization: 2x multiplier
                >= 0.8m => 1.5m,  // 80-90% utilization: 1.5x multiplier
                >= 0.7m => 1.2m,  // 70-80% utilization: 1.2x multiplier
                _ => 1.0m         // <70% utilization: no multiplier
            };
        }

        private async Task<decimal> CalculateEventMultiplierAsync(
            RentalLocationId locationId,
            DateTime startDate,
            DateTime endDate)
        {
            var events = await _eventDataService.GetEventsInAreaAsync(locationId, startDate, endDate);
            
            var maxMultiplier = 1.0m;
            
            foreach (var eventInfo in events)
            {
                var eventMultiplier = eventInfo.EventType switch
                {
                    EventType.MajorConference => 1.3m,
                    EventType.SportsEvent => 1.4m,
                    EventType.Festival => 1.5m,
                    EventType.Concert => 1.2m,
                    EventType.Convention => 1.3m,
                    _ => 1.0m
                };
                
                // Apply distance decay (closer events have more impact)
                var distanceDecay = Math.Max(0.1m, 1.0m - (eventInfo.DistanceKm / 50.0m));
                var adjustedMultiplier = 1.0m + ((eventMultiplier - 1.0m) * distanceDecay);
                
                maxMultiplier = Math.Max(maxMultiplier, adjustedMultiplier);
            }
            
            return maxMultiplier;
        }
    }
}
```

### 4.3 Fleet Management System

#### Vehicle Optimization Engine
```csharp
namespace DriveOps.Rental.Application.Services
{
    public class FleetOptimizationService
    {
        public async Task<FleetOptimizationResult> OptimizeFleetAllocationAsync(
            TenantId tenantId,
            DateTime optimizationDate,
            int forecastDays = 7)
        {
            // Get upcoming reservations
            var upcomingReservations = await _reservationRepository.GetUpcomingReservationsAsync(
                tenantId, optimizationDate, optimizationDate.AddDays(forecastDays));
                
            // Get available vehicles
            var availableVehicles = await _vehicleRepository.GetAvailableVehiclesAsync(
                tenantId, optimizationDate, optimizationDate.AddDays(forecastDays));
                
            // Demand forecasting
            var demandForecast = await _demandForecastService.ForecastDemandAsync(
                tenantId, optimizationDate, forecastDays);
                
            // Optimization algorithm
            var optimizationResult = await SolveFleetAllocationProblemAsync(
                upcomingReservations, availableVehicles, demandForecast);
                
            return optimizationResult;
        }

        private async Task<FleetOptimizationResult> SolveFleetAllocationProblemAsync(
            List<Reservation> reservations,
            List<RentalVehicle> vehicles,
            DemandForecast demandForecast)
        {
            // Integer Linear Programming approach for optimal assignment
            var solver = new OptimizationSolver();
            
            // Decision variables: x[i,j] = 1 if reservation i is assigned to vehicle j
            var variables = new Dictionary<(int reservation, int vehicle), bool>();
            
            // Objective: Maximize revenue while minimizing costs
            var objective = new ObjectiveFunction();
            
            foreach (var (reservationIndex, reservation) in reservations.Select((r, i) => (i, r)))
            {
                foreach (var (vehicleIndex, vehicle) in vehicles.Select((v, i) => (i, v)))
                {
                    if (IsCompatible(reservation, vehicle))
                    {
                        var revenue = CalculateRevenue(reservation, vehicle);
                        var cost = CalculateOperationalCost(reservation, vehicle);
                        var profit = revenue - cost;
                        
                        objective.AddTerm(profit, variables[(reservationIndex, vehicleIndex)]);
                    }
                }
            }
            
            // Constraints
            AddCapacityConstraints(solver, variables, reservations, vehicles);
            AddLocationConstraints(solver, variables, reservations, vehicles);
            AddMaintenanceConstraints(solver, variables, vehicles);
            
            var solution = await solver.SolveAsync(objective, variables);
            
            return new FleetOptimizationResult
            {
                VehicleAssignments = ExtractAssignments(solution, reservations, vehicles),
                ExpectedRevenue = solution.ObjectiveValue,
                OptimizationMetrics = CalculateOptimizationMetrics(solution)
            };
        }

        private bool IsCompatible(Reservation reservation, RentalVehicle vehicle)
        {
            // Check category compatibility
            if (vehicle.CategoryId != reservation.CategoryId)
                return false;
                
            // Check location compatibility
            if (!IsLocationAccessible(vehicle, reservation.PickupLocationId))
                return false;
                
            // Check maintenance schedule
            if (IsMaintenanceDue(vehicle, reservation.PickupDateTime))
                return false;
                
            return true;
        }

        public async Task<MaintenanceSchedule> OptimizeMaintenanceScheduleAsync(
            TenantId tenantId,
            int planningHorizonDays = 30)
        {
            var vehicles = await _vehicleRepository.GetVehiclesRequiringMaintenanceAsync(
                tenantId, DateTime.UtcNow.AddDays(planningHorizonDays));
                
            var reservations = await _reservationRepository.GetUpcomingReservationsAsync(
                tenantId, DateTime.UtcNow, DateTime.UtcNow.AddDays(planningHorizonDays));
                
            var maintenanceSlots = await _maintenanceService.GetAvailableMaintenanceSlotsAsync(
                DateTime.UtcNow, planningHorizonDays);
                
            // Schedule maintenance to minimize revenue impact
            var schedule = new MaintenanceSchedule();
            
            foreach (var vehicle in vehicles.OrderByDescending(v => GetMaintenanceUrgency(v)))
            {
                var optimalSlot = FindOptimalMaintenanceSlot(vehicle, maintenanceSlots, reservations);
                if (optimalSlot != null)
                {
                    schedule.AddMaintenanceSlot(vehicle.Id, optimalSlot);
                    maintenanceSlots.Remove(optimalSlot);
                }
            }
            
            return schedule;
        }
    }
}
```
                .IsRequired();

            // Indexes
            builder.HasIndex(v => new { v.TenantId, v.CategoryId });
            builder.HasIndex(v => new { v.TenantId, v.Status });
            builder.HasIndex(v => new { v.TenantId, v.CurrentLocationId });
            builder.HasIndex(v => new { v.TenantId, v.IsAvailableForRental });

            // Relationships
            builder.HasOne<VehicleCategory>()
                .WithMany()
                .HasForeignKey(v => v.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Reference to core Vehicles module
            builder.HasOne<Vehicle>()
                .WithMany()
                .HasForeignKey(v => v.BaseVehicleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class RentalCustomerConfiguration : IEntityTypeConfiguration<RentalCustomer>
    {
        public void Configure(EntityTypeBuilder<RentalCustomer> builder)
        {
            builder.ToTable("rental_customers");
            
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id)
                .HasConversion(id => id.Value, value => RentalCustomerId.From(value));

            builder.Property(c => c.TenantId)
                .HasConversion(id => id.Value, value => TenantId.From(value))
                .IsRequired();

            builder.Property(c => c.Number)
                .HasConversion(n => n.Value, value => CustomerNumber.From(value))
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(c => c.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(c => c.Tier)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(c => c.CreditRating)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(c => c.Email)
                .HasMaxLength(255)
                .IsRequired();

            // Indexes
            builder.HasIndex(c => new { c.TenantId, c.Number }).IsUnique();
            builder.HasIndex(c => new { c.TenantId, c.Email }).IsUnique();
            builder.HasIndex(c => new { c.TenantId, c.Status });
            builder.HasIndex(c => new { c.TenantId, c.Tier });

            // Value object configurations
            builder.OwnsOne(c => c.Address, address =>
            {
                address.Property(a => a.Street).HasMaxLength(255);
                address.Property(a => a.City).HasMaxLength(100);
                address.Property(a => a.PostalCode).HasMaxLength(20);
                address.Property(a => a.Country).HasMaxLength(100);
            });

            builder.OwnsOne(c => c.Preferences, prefs =>
            {
                prefs.Property(p => p.PreferredLanguage).HasMaxLength(10);
                prefs.Property(p => p.PreferredCurrency).HasMaxLength(3);
                prefs.Property(p => p.NotificationPreferences).HasConversion(
                    np => JsonSerializer.Serialize(np, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<NotificationPreferences>(json, (JsonSerializerOptions?)null)!);
            });
        }
    }
}
```

---

## 3. Database Schema (PostgreSQL)

### 3.1 Rental Module Tables

```sql
-- Create rental schema
CREATE SCHEMA IF NOT EXISTS rental;

-- Rental customers table
CREATE TABLE rental.rental_customers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL, -- References admin.tenants(id)
    user_id UUID, -- References users.users(id) if registered user
    number VARCHAR(20) NOT NULL,
    
    -- Personal information
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL,
    phone VARCHAR(50) NOT NULL,
    date_of_birth DATE NOT NULL,
    
    -- Address (embedded value object)
    address_street VARCHAR(255),
    address_city VARCHAR(100),
    address_postal_code VARCHAR(20),
    address_country VARCHAR(100),
    
    -- Driver information
    driver_license_number VARCHAR(50) NOT NULL,
    driver_license_expiry DATE NOT NULL,
    driver_license_country VARCHAR(3) NOT NULL,
    is_driver_license_verified BOOLEAN NOT NULL DEFAULT FALSE,
    driver_license_verified_at TIMESTAMP WITH TIME ZONE,
    
    -- Customer status
    status INTEGER NOT NULL DEFAULT 1, -- 1: Pending, 2: Active, 3: Suspended, 4: Inactive, 5: Blacklisted
    tier INTEGER NOT NULL DEFAULT 1, -- 1: Bronze, 2: Silver, 3: Gold, 4: Platinum
    is_blacklisted BOOLEAN NOT NULL DEFAULT FALSE,
    blacklist_reason TEXT,
    
    -- Credit and risk
    credit_rating INTEGER NOT NULL DEFAULT 5, -- 1: Excellent, 2: Good, 3: Fair, 4: Poor, 5: NoCredit
    credit_limit DECIMAL(12,2) NOT NULL DEFAULT 0,
    available_credit DECIMAL(12,2) NOT NULL DEFAULT 0,
    risk_level INTEGER NOT NULL DEFAULT 1, -- 1: Low, 2: Medium, 3: High, 4: Critical
    
    -- Loyalty program
    loyalty_points INTEGER NOT NULL DEFAULT 0,
    loyalty_tier INTEGER NOT NULL DEFAULT 1,
    loyalty_tier_achieved_at TIMESTAMP WITH TIME ZONE,
    
    -- Statistics
    total_rentals INTEGER NOT NULL DEFAULT 0,
    total_spent DECIMAL(12,2) NOT NULL DEFAULT 0,
    last_rental_date TIMESTAMP WITH TIME ZONE,
    average_rental_value DECIMAL(12,2) NOT NULL DEFAULT 0,
    
    -- Preferences (JSON)
    preferences JSONB,
    
    -- Audit
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    -- Constraints
    UNIQUE(tenant_id, number),
    UNIQUE(tenant_id, email),
    CHECK (total_rentals >= 0),
    CHECK (total_spent >= 0),
    CHECK (loyalty_points >= 0)
);

-- Vehicle categories table
CREATE TABLE rental.vehicle_categories (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    class INTEGER NOT NULL, -- 1: Economy, 2: Compact, etc.
    
    -- Capacity specifications
    passenger_capacity INTEGER NOT NULL,
    luggage_capacity INTEGER NOT NULL,
    door_count INTEGER NOT NULL,
    
    -- Technical specifications
    transmission_type INTEGER NOT NULL, -- 1: Manual, 2: Automatic, 3: CVT
    fuel_type INTEGER NOT NULL, -- 1: Gasoline, 2: Diesel, 3: Electric, 4: Hybrid
    has_air_conditioning BOOLEAN NOT NULL DEFAULT FALSE,
    has_gps BOOLEAN NOT NULL DEFAULT FALSE,
    
    -- Pricing
    base_daily_rate DECIMAL(10,2) NOT NULL,
    base_weekly_rate DECIMAL(10,2) NOT NULL,
    base_monthly_rate DECIMAL(10,2) NOT NULL,
    base_security_deposit DECIMAL(10,2) NOT NULL,
    
    -- Marketing
    image_url VARCHAR(500),
    features TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, name)
);

-- Rental vehicles table
CREATE TABLE rental.rental_vehicles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    base_vehicle_id UUID NOT NULL, -- References vehicles.vehicles(id)
    category_id UUID NOT NULL REFERENCES rental.vehicle_categories(id),
    home_location_id UUID NOT NULL REFERENCES rental.rental_locations(id),
    current_location_id UUID REFERENCES rental.rental_locations(id),
    
    -- Rental-specific properties
    status INTEGER NOT NULL DEFAULT 1, -- 1: Available, 2: Rented, 3: Maintenance, etc.
    daily_rate DECIMAL(10,2) NOT NULL,
    weekly_rate DECIMAL(10,2) NOT NULL,
    monthly_rate DECIMAL(10,2) NOT NULL,
    security_deposit_amount DECIMAL(10,2) NOT NULL,
    
    -- Availability management
    is_available_for_rental BOOLEAN NOT NULL DEFAULT TRUE,
    available_from TIMESTAMP WITH TIME ZONE,
    available_until TIMESTAMP WITH TIME ZONE,
    unavailability_reason VARCHAR(255),
    
    -- Utilization tracking
    total_rental_days INTEGER NOT NULL DEFAULT 0,
    total_revenue DECIMAL(12,2) NOT NULL DEFAULT 0,
    last_rental_end_date TIMESTAMP WITH TIME ZONE,
    
    -- Maintenance scheduling
    current_mileage INTEGER NOT NULL DEFAULT 0,
    next_maintenance_date DATE,
    next_maintenance_mileage INTEGER,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CHECK (daily_rate > 0),
    CHECK (weekly_rate > 0),
    CHECK (monthly_rate > 0),
    CHECK (current_mileage >= 0)
);

-- Reservations table
CREATE TABLE rental.reservations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    customer_id UUID NOT NULL REFERENCES rental.rental_customers(id),
    vehicle_id UUID REFERENCES rental.rental_vehicles(id),
    category_id UUID NOT NULL REFERENCES rental.vehicle_categories(id),
    number VARCHAR(20) NOT NULL,
    
    -- Booking details
    pickup_datetime TIMESTAMP WITH TIME ZONE NOT NULL,
    return_datetime TIMESTAMP WITH TIME ZONE NOT NULL,
    pickup_location_id UUID NOT NULL REFERENCES rental.rental_locations(id),
    return_location_id UUID NOT NULL REFERENCES rental.rental_locations(id),
    
    -- Pricing
    tariff_plan_id UUID NOT NULL REFERENCES rental.tariff_plans(id),
    base_amount DECIMAL(10,2) NOT NULL,
    discount_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    tax_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
    total_amount DECIMAL(10,2) NOT NULL,
    security_deposit DECIMAL(10,2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    
    -- Status management
    status INTEGER NOT NULL DEFAULT 1, -- 1: Pending, 2: Confirmed, 3: InProgress, 4: Completed, 5: Cancelled, 6: NoShow
    payment_status INTEGER NOT NULL DEFAULT 1, -- 1: Pending, 2: Authorized, 3: Paid, 4: Failed, 5: Refunded
    confirmed_at TIMESTAMP WITH TIME ZONE,
    cancelled_at TIMESTAMP WITH TIME ZONE,
    cancellation_reason TEXT,
    
    -- Payment
    payment_method_id UUID REFERENCES rental.payment_methods(id),
    
    -- Audit
    created_by UUID NOT NULL, -- References users.users(id)
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    -- Constraints
    UNIQUE(tenant_id, number),
    CHECK (pickup_datetime < return_datetime),
    CHECK (base_amount >= 0),
    CHECK (total_amount >= 0)
);

-- Rental contracts table
CREATE TABLE rental.rental_contracts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    reservation_id UUID NOT NULL REFERENCES rental.reservations(id),
    customer_id UUID NOT NULL REFERENCES rental.rental_customers(id),
    vehicle_id UUID NOT NULL REFERENCES rental.rental_vehicles(id),
    number VARCHAR(20) NOT NULL,
    
    -- Contract terms
    effective_start_date TIMESTAMP WITH TIME ZONE NOT NULL,
    effective_end_date TIMESTAMP WITH TIME ZONE NOT NULL,
    actual_start_date TIMESTAMP WITH TIME ZONE,
    actual_end_date TIMESTAMP WITH TIME ZONE,
    
    -- Handover process
    pickup_handover_id UUID REFERENCES rental.vehicle_handovers(id),
    return_handover_id UUID REFERENCES rental.vehicle_handovers(id),
    
    -- Financial details
    contract_amount DECIMAL(10,2) NOT NULL,
    security_deposit_amount DECIMAL(10,2) NOT NULL,
    deposit_status INTEGER NOT NULL DEFAULT 1, -- 1: Pending, 2: Authorized, 3: Captured, 4: Released
    
    -- Mileage tracking
    start_mileage INTEGER NOT NULL DEFAULT 0,
    end_mileage INTEGER,
    included_mileage_limit INTEGER NOT NULL DEFAULT 0,
    excess_mileage_rate DECIMAL(8,4) NOT NULL DEFAULT 0,
    
    -- Contract status
    status INTEGER NOT NULL DEFAULT 1, -- 1: Draft, 2: Ready, 3: Active, 4: Completed, 5: Terminated
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, number)
);

-- Tariff plans table
CREATE TABLE rental.tariff_plans (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    type INTEGER NOT NULL, -- 1: Standard, 2: Premium, 3: Corporate, etc.
    
    -- Validity period
    valid_from TIMESTAMP WITH TIME ZONE NOT NULL,
    valid_until TIMESTAMP WITH TIME ZONE NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Base rates
    hourly_rate DECIMAL(10,2) NOT NULL DEFAULT 0,
    daily_rate DECIMAL(10,2) NOT NULL DEFAULT 0,
    weekly_rate DECIMAL(10,2) NOT NULL DEFAULT 0,
    monthly_rate DECIMAL(10,2) NOT NULL DEFAULT 0,
    
    -- Mileage
    included_mileage_per_day DECIMAL(8,2) NOT NULL DEFAULT 0,
    excess_mileage_rate DECIMAL(8,4) NOT NULL DEFAULT 0,
    
    -- Discounts and multipliers
    long_term_discount_percentage DECIMAL(5,2) NOT NULL DEFAULT 0,
    long_term_discount_threshold_days INTEGER NOT NULL DEFAULT 7,
    weekend_multiplier DECIMAL(5,2) NOT NULL DEFAULT 1.0,
    holiday_multiplier DECIMAL(5,2) NOT NULL DEFAULT 1.0,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, name),
    CHECK (valid_from < valid_until)
);

-- Seasonal pricing table
CREATE TABLE rental.seasonal_pricing (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tariff_plan_id UUID NOT NULL REFERENCES rental.tariff_plans(id) ON DELETE CASCADE,
    season_name VARCHAR(100) NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    multiplier DECIMAL(5,2) NOT NULL DEFAULT 1.0,
    priority INTEGER NOT NULL DEFAULT 1,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    CHECK (start_date <= end_date),
    CHECK (multiplier > 0)
);

-- Promo codes table
CREATE TABLE rental.promo_codes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    code VARCHAR(50) NOT NULL,
    description TEXT,
    type INTEGER NOT NULL, -- 1: General, 2: CustomerSpecific, etc.
    
    -- Discount configuration
    discount_value DECIMAL(10,2) NOT NULL,
    discount_type INTEGER NOT NULL, -- 1: Percentage, 2: FixedAmount
    max_discount_amount DECIMAL(10,2),
    min_order_amount DECIMAL(10,2),
    
    -- Validity and usage
    valid_from TIMESTAMP WITH TIME ZONE NOT NULL,
    valid_until TIMESTAMP WITH TIME ZONE NOT NULL,
    max_usages INTEGER,
    max_usages_per_customer INTEGER,
    current_usage_count INTEGER NOT NULL DEFAULT 0,
    
    -- Applicability
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_stackable BOOLEAN NOT NULL DEFAULT FALSE,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, code),
    CHECK (discount_value > 0),
    CHECK (valid_from < valid_until)
);
```
        public async Task<Result> ReserveVehicleAsync(
            ReservationId reservationId,
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            // This would typically involve creating a temporary hold on the vehicle
            // and would be implemented with database locks or a reservation system
            _logger.LogInformation("Reserving vehicle for reservation {ReservationId}", reservationId);
            
            // Invalidate availability cache for this category
            var cachePattern = $"availability:*:{categoryId}:*";
            // Would need to implement cache invalidation by pattern
            
            return Result.Success();
        }

        public async Task<Result> ReleaseReservationAsync(
            ReservationId reservationId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Releasing reservation {ReservationId}", reservationId);
            return Result.Success();
        }
    }

    public interface IPricingService
    {
        Task<Result<PricingCalculation>> CalculatePricingAsync(
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            CustomerTier customerTier,
            int estimatedMileage = 0,
            CancellationToken cancellationToken = default);
            
        Task<Result<PricingCalculation>> RecalculatePricingAsync(
            ReservationId reservationId,
            DateTime newStartDate,
            DateTime newEndDate,
            CancellationToken cancellationToken = default);
    }

    public class PricingService : IPricingService
    {
        private readonly ITariffPlanRepository _tariffRepository;
        private readonly IVehicleCategoryRepository _categoryRepository;
        private readonly ISeasonalPricingRepository _seasonalPricingRepository;
        private readonly IDistributedCache _cache;

        public PricingService(
            ITariffPlanRepository tariffRepository,
            IVehicleCategoryRepository categoryRepository,
            ISeasonalPricingRepository seasonalPricingRepository,
            IDistributedCache cache)
        {
            _tariffRepository = tariffRepository;
            _categoryRepository = categoryRepository;
            _seasonalPricingRepository = seasonalPricingRepository;
            _cache = cache;
        }

        public async Task<Result<PricingCalculation>> CalculatePricingAsync(
            VehicleCategoryId categoryId,
            DateTime startDate,
            DateTime endDate,
            CustomerTier customerTier,
            int estimatedMileage = 0,
            CancellationToken cancellationToken = default)
        {
            // Get applicable tariff plan
            var tariffPlan = await _tariffRepository.GetApplicableTariffAsync(
                categoryId, startDate, customerTier, cancellationToken);
                
            if (tariffPlan == null)
                return Result<PricingCalculation>.Failure("No applicable tariff plan found");

            // Get vehicle category base rates
            var category = await _categoryRepository.GetByIdAsync(categoryId, cancellationToken);
            if (category == null)
                return Result<PricingCalculation>.Failure("Vehicle category not found");

            // Calculate base rental cost
            var basePrice = tariffPlan.CalculatePrice(categoryId, startDate, endDate, estimatedMileage);
            
            // Apply customer tier discounts
            var tierDiscount = GetTierDiscount(customerTier);
            var discountAmount = basePrice * tierDiscount;
            
            // Calculate taxes (would be configurable by location)
            var taxRate = 0.20m; // 20% VAT example
            var taxAmount = (basePrice - discountAmount) * taxRate;
            
            // Calculate security deposit
            var securityDeposit = category.BaseSecurityDeposit;
            
            var pricing = new PricingCalculation
            {
                TariffPlanId = tariffPlan.Id,
                BaseAmount = basePrice,
                DiscountAmount = discountAmount,
                TaxAmount = taxAmount,
                SecurityDepositAmount = securityDeposit,
                TotalAmount = basePrice - discountAmount + taxAmount,
                Currency = "EUR",
                CalculatedAt = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddMinutes(30) // Price hold for 30 minutes
            };

            return Result<PricingCalculation>.Success(pricing);
        }

        public async Task<Result<PricingCalculation>> RecalculatePricingAsync(
            ReservationId reservationId,
            DateTime newStartDate,
            DateTime newEndDate,
            CancellationToken cancellationToken = default)
        {
            // Implementation would fetch the reservation and recalculate pricing
            // with the new dates while preserving applied discounts and promo codes
            throw new NotImplementedException();
        }

        private decimal GetTierDiscount(CustomerTier tier)
        {
            return tier switch
            {
                CustomerTier.Bronze => 0.00m,
                CustomerTier.Silver => 0.05m,
                CustomerTier.Gold => 0.10m,
                CustomerTier.Platinum => 0.15m,
                _ => 0.00m
            };
        }
    }

    public interface IPromoCodeService
    {
        Task<Result<decimal>> ApplyPromoCodeAsync(
            string promoCode,
            RentalCustomerId customerId,
            decimal baseAmount,
            CancellationToken cancellationToken = default);
            
        Task<Result> ValidatePromoCodeAsync(
            string promoCode,
            RentalCustomerId customerId,
            CancellationToken cancellationToken = default);
    }

    public class PromoCodeService : IPromoCodeService
    {
        private readonly IPromoCodeRepository _promoCodeRepository;

        public PromoCodeService(IPromoCodeRepository promoCodeRepository)
        {
            _promoCodeRepository = promoCodeRepository;
        }

        public async Task<Result<decimal>> ApplyPromoCodeAsync(
            string promoCode,
            RentalCustomerId customerId,
            decimal baseAmount,
            CancellationToken cancellationToken = default)
        {
            var promo = await _promoCodeRepository.GetByCodeAsync(promoCode, cancellationToken);
            if (promo == null)
                return Result<decimal>.Failure("Invalid promo code");

            var discountResult = promo.CalculateDiscount(baseAmount, customerId);
            if (!discountResult.IsSuccess)
                return discountResult;

            // Record usage
            promo.RecordUsage();
            await _promoCodeRepository.UpdateAsync(promo, cancellationToken);

            return discountResult;
        }

        public async Task<Result> ValidatePromoCodeAsync(
            string promoCode,
            RentalCustomerId customerId,
            CancellationToken cancellationToken = default)
        {
            var promo = await _promoCodeRepository.GetByCodeAsync(promoCode, cancellationToken);
            if (promo == null)
                return Result.Failure("Invalid promo code");

            return promo.ValidateUsage(customerId);
        }
    }
}
```
    {
        Scratch = 1,
        Dent = 2,
        Crack = 3,
        Chip = 4,
        Tear = 5,
        Burn = 6,
        Stain = 7,
        Missing = 8,
        Broken = 9,
        Flat = 10
    }

    public enum DamageSeverity
    {
        Minor = 1,
        Moderate = 2,
        Major = 3,
        Severe = 4
    }

    public enum DamageStatus
    {
        Reported = 1,
        Assessed = 2,
        UnderRepair = 3,
        Repaired = 4,
        WrittenOff = 5
    }

    public enum InspectionType
    {
        PreRental = 1,
        PostRental = 2,
        Maintenance = 3,
        Damage = 4,
        Insurance = 5
    }

    public enum PhotoType
    {
        Overview = 1,
        FrontView = 2,
        RearView = 3,
        LeftSide = 4,
        RightSide = 5,
        Interior = 6,
        Dashboard = 7,
        Mileage = 8,
        FuelGauge = 9,
        Damage = 10,
        DriverLicense = 11,
        Signature = 12
    }
}
```

---

## 2. Complete C# Architecture

### 2.1 Application Layer - CQRS Commands and Queries

```csharp
namespace DriveOps.Rental.Application.Commands
{
    // Reservation Commands
    public record CreateReservationCommand(
        string TenantId,
        string CustomerId,
        string CategoryId,
        DateTime PickupDateTime,
        DateTime ReturnDateTime,
        string PickupLocationId,
        string ReturnLocationId,
        string? PromoCode,
        string UserId
    ) : IRequest<Result<ReservationDto>>;

    public class CreateReservationHandler : IRequestHandler<CreateReservationCommand, Result<ReservationDto>>
    {
        private readonly IReservationRepository _reservationRepository;
        private readonly IRentalCustomerRepository _customerRepository;
        private readonly IVehicleCategoryRepository _categoryRepository;
        private readonly IAvailabilityService _availabilityService;
        private readonly IPricingService _pricingService;
        private readonly IPromoCodeService _promoCodeService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CreateReservationHandler(
            IReservationRepository reservationRepository,
            IRentalCustomerRepository customerRepository,
            IVehicleCategoryRepository categoryRepository,
            IAvailabilityService availabilityService,
            IPricingService pricingService,
            IPromoCodeService promoCodeService,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _reservationRepository = reservationRepository;
            _customerRepository = customerRepository;
            _categoryRepository = categoryRepository;
            _availabilityService = availabilityService;
            _pricingService = pricingService;
            _promoCodeService = promoCodeService;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<Result<ReservationDto>> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
        {
            // Validate customer exists and is eligible
            var customer = await _customerRepository.GetByIdAsync(
                RentalCustomerId.From(Guid.Parse(request.CustomerId)), 
                cancellationToken);
                
            if (customer == null)
                return Result<ReservationDto>.Failure("Customer not found");

            if (customer.Status != CustomerStatus.Active)
                return Result<ReservationDto>.Failure("Customer account is not active");

            // Validate vehicle category
            var category = await _categoryRepository.GetByIdAsync(
                VehicleCategoryId.From(Guid.Parse(request.CategoryId)), 
                cancellationToken);
                
            if (category == null || !category.IsActive)
                return Result<ReservationDto>.Failure("Vehicle category not available");

            // Check availability
            var availabilityResult = await _availabilityService.CheckAvailabilityAsync(
                TenantId.From(Guid.Parse(request.TenantId)),
                category.Id,
                request.PickupDateTime,
                request.ReturnDateTime,
                cancellationToken);

            if (!availabilityResult.IsSuccess || !availabilityResult.Value.IsAvailable)
                return Result<ReservationDto>.Failure("No vehicles available for the selected period");

            // Calculate pricing
            var pricingResult = await _pricingService.CalculatePricingAsync(
                category.Id,
                request.PickupDateTime,
                request.ReturnDateTime,
                customer.Tier,
                cancellationToken);

            if (!pricingResult.IsSuccess)
                return Result<ReservationDto>.Failure("Unable to calculate pricing");

            var pricing = pricingResult.Value;

            // Apply promo code if provided
            if (!string.IsNullOrEmpty(request.PromoCode))
            {
                var promoResult = await _promoCodeService.ApplyPromoCodeAsync(
                    request.PromoCode,
                    customer.Id,
                    pricing.TotalAmount,
                    cancellationToken);

                if (promoResult.IsSuccess)
                {
                    pricing = pricing.ApplyDiscount(promoResult.Value);
                }
            }

            // Create reservation
            var reservation = Reservation.Create(
                TenantId.From(Guid.Parse(request.TenantId)),
                customer.Id,
                category.Id,
                request.PickupDateTime,
                request.ReturnDateTime,
                RentalLocationId.From(Guid.Parse(request.PickupLocationId)),
                RentalLocationId.From(Guid.Parse(request.ReturnLocationId)),
                pricing.TariffPlanId,
                UserId.From(Guid.Parse(request.UserId))
            );

            // Set pricing
            var confirmResult = reservation.Confirm(pricing, UserId.From(Guid.Parse(request.UserId)));
            if (!confirmResult.IsSuccess)
                return Result<ReservationDto>.Failure(confirmResult.Error);

            // Save reservation
            await _reservationRepository.AddAsync(reservation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Map to DTO
            var reservationDto = _mapper.Map<ReservationDto>(reservation);
            return Result<ReservationDto>.Success(reservationDto);
        }
    }

    public record ConfirmReservationCommand(
        string TenantId,
        string ReservationId,
        string PaymentMethodId,
        string UserId
    ) : IRequest<Result>;

    public record CancelReservationCommand(
        string TenantId,
        string ReservationId,
        string CancellationReason,
        string UserId
    ) : IRequest<Result>;

    public record AssignVehicleToReservationCommand(
        string TenantId,
        string ReservationId,
        string VehicleId,
        string UserId
    ) : IRequest<Result>;

    // Vehicle Handover Commands
    public record CreateHandoverCommand(
        string TenantId,
        string VehicleId,
        string CustomerId,
        HandoverType Type,
        DateTime ScheduledDateTime,
        string LocationId,
        string AssignedStaffId,
        string? ReservationId = null,
        string? ContractId = null
    ) : IRequest<Result<VehicleHandoverDto>>;

    public record StartHandoverCommand(
        string TenantId,
        string HandoverId,
        int CurrentMileage,
        FuelLevel FuelLevel,
        string StaffId
    ) : IRequest<Result>;

    public record AddHandoverPhotoCommand(
        string TenantId,
        string HandoverId,
        IFormFile Photo,
        PhotoType PhotoType,
        string? Description,
        string UserId
    ) : IRequest<Result<HandoverPhotoDto>>;

    public record CompleteHandoverCommand(
        string TenantId,
        string HandoverId,
        VehicleCondition OverallCondition,
        string? CustomerSignature,
        string? Notes,
        List<ChecklistItemDto> ChecklistItems,
        string UserId
    ) : IRequest<Result>;

    // Damage Report Commands
    public record CreateDamageReportCommand(
        string TenantId,
        string VehicleId,
        string? InspectionId,
        string? ContractId,
        VehicleArea Area,
        DamageType Type,
        DamageSeverity Severity,
        string Description,
        decimal? EstimatedCost,
        List<IFormFile> Photos,
        string UserId
    ) : IRequest<Result<DamageReportDto>>;

    public record AssessDamageCustomerLiabilityCommand(
        string TenantId,
        string DamageReportId,
        bool IsCustomerLiable,
        decimal LiabilityAmount,
        string AssessedBy
    ) : IRequest<Result>;
}
```
        {
            var previousRating = CreditRating;
            var previousLimit = CreditLimit;
            
            CreditRating = newRating;
            CreditLimit = newLimit;
            AvailableCredit = newLimit; // Reset available credit
            
            AddDomainEvent(new CustomerCreditRatingUpdatedEvent(
                Id, TenantId, previousRating, newRating, previousLimit, newLimit, updatedBy));
                
            return Result.Success();
        }

        public Result CompleteRental(decimal rentalAmount)
        {
            TotalRentals++;
            TotalSpent += rentalAmount;
            LastRentalDate = DateTime.UtcNow;
            AverageRentalValue = TotalSpent / TotalRentals;
            
            // Award loyalty points (1 point per euro spent)
            var pointsEarned = (int)Math.Floor(rentalAmount);
            if (pointsEarned > 0)
            {
                AddLoyaltyPoints(pointsEarned, "Rental completion", UserId.System());
            }

            AddDomainEvent(new CustomerRentalCompletedEvent(Id, TenantId, rentalAmount));
            return Result.Success();
        }

        private CustomerTier CalculateLoyaltyTier(int points)
        {
            return points switch
            {
                >= 10000 => CustomerTier.Platinum,
                >= 5000 => CustomerTier.Gold,
                >= 1000 => CustomerTier.Silver,
                _ => CustomerTier.Bronze
            };
        }
    }

    public class CustomerDocument : Entity
    {
        public CustomerDocumentId Id { get; private set; }
        public RentalCustomerId CustomerId { get; private set; }
        public DocumentType Type { get; private set; }
        public string FileName { get; private set; }
        public string FileUrl { get; private set; }
        public FileId FileId { get; private set; } // Reference to Files module
        
        public bool IsVerified { get; private set; }
        public DateTime? VerifiedAt { get; private set; }
        public UserId? VerifiedBy { get; private set; }
        public string? VerificationNotes { get; private set; }
        
        public DateTime UploadedAt { get; private set; }
        public UserId UploadedBy { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow;
        public bool RequiresRenewal => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.UtcNow.AddDays(30);
    }

    public enum CustomerStatus
    {
        Pending = 1,
        Active = 2,
        Suspended = 3,
        Inactive = 4,
        Blacklisted = 5
    }

    public enum CustomerTier
    {
        Bronze = 1,
        Silver = 2,
        Gold = 3,
        Platinum = 4
    }

    public enum CreditRating
    {
        Excellent = 1,
        Good = 2,
        Fair = 3,
        Poor = 4,
        NoCredit = 5
    }

    public enum RiskLevel
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public enum DocumentType
    {
        DriverLicense = 1,
        Passport = 2,
        IdentityCard = 3,
        CreditCard = 4,
        BankStatement = 5,
        UtilityBill = 6,
        InsuranceCard = 7
    }
}
```
            AddDomainEvent(new VehicleLocationChangedEvent(Id, TenantId, previousLocationId, newLocationId, updatedBy));
            return Result.Success();
        }

        public bool IsAvailableForPeriod(DateTime startDate, DateTime endDate)
        {
            if (!IsAvailableForRental)
                return false;

            if (AvailableFrom.HasValue && startDate < AvailableFrom.Value)
                return false;

            if (AvailableUntil.HasValue && endDate > AvailableUntil.Value)
                return false;

            // Check for conflicting reservations
            return !_availabilityPeriods.Any(period => 
                period.IsUnavailable && 
                period.StartDate <= endDate && 
                period.EndDate >= startDate);
        }
    }

    public class VehicleCategory : Entity
    {
        public VehicleCategoryId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public VehicleClass Class { get; private set; }
        
        // Capacity specifications
        public int PassengerCapacity { get; private set; }
        public int LuggageCapacity { get; private set; }
        public int DoorCount { get; private set; }
        
        // Technical specifications
        public TransmissionType TransmissionType { get; private set; }
        public FuelType FuelType { get; private set; }
        public bool HasAirConditioning { get; private set; }
        public bool HasGps { get; private set; }
        
        // Pricing
        public decimal BaseDailyRate { get; private set; }
        public decimal BaseWeeklyRate { get; private set; }
        public decimal BaseMonthlyRate { get; private set; }
        public decimal BaseSecurityDeposit { get; private set; }
        
        // Marketing
        public string? ImageUrl { get; private set; }
        public string? Features { get; private set; }
        public bool IsActive { get; private set; }
        public int SortOrder { get; private set; }

        private readonly List<RentalVehicle> _vehicles = new();
        public IReadOnlyCollection<RentalVehicle> Vehicles => _vehicles.AsReadOnly();

        public int GetAvailableVehicleCount(DateTime startDate, DateTime endDate)
        {
            return _vehicles.Count(v => v.IsAvailableForPeriod(startDate, endDate));
        }
    }

    public enum RentalVehicleStatus
    {
        Available = 1,
        Rented = 2,
        Maintenance = 3,
        Cleaning = 4,
        Damaged = 5,
        OutOfService = 6
    }

    public enum VehicleClass
    {
        Economy = 1,
        Compact = 2,
        Intermediate = 3,
        Standard = 4,
        FullSize = 5,
        Premium = 6,
        Luxury = 7,
        SUV = 8,
        Van = 9,
        Convertible = 10,
        Electric = 11,
        Hybrid = 12
    }
}
```
    {
        Pending = 1,
        Confirmed = 2,
        InProgress = 3,
        Completed = 4,
        Cancelled = 5,
        NoShow = 6
    }

    public enum PaymentStatus
    {
        Pending = 1,
        Authorized = 2,
        Paid = 3,
        Failed = 4,
        Refunded = 5,
        PartiallyRefunded = 6
    }
}
```
