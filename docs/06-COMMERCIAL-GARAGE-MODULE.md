# DriveOps GARAGE Commercial Module Documentation

This document provides comprehensive technical documentation for the GARAGE commercial module - a premium 49€/month workshop management system for automotive repair shops and service centers. The GARAGE module provides complete workshop operations management including intervention management, mechanic scheduling, parts inventory, automated invoicing, and performance analytics.

## Table of Contents

1. [Module Overview](#1-module-overview)
2. [Business Domain Model](#2-business-domain-model)
3. [Technical Architecture](#3-technical-architecture)
4. [Database Schema](#4-database-schema)
5. [Application Layer](#5-application-layer)
6. [API Design](#6-api-design)
7. [Blazor UI Components](#7-blazor-ui-components)
8. [Integration Points](#8-integration-points)
9. [Performance Considerations](#9-performance-considerations)
10. [Security & Compliance](#10-security--compliance)
11. [Business Logic Implementation](#11-business-logic-implementation)
12. [Testing Strategy](#12-testing-strategy)

---

## 1. Module Overview

### 1.1 Purpose and Scope

The GARAGE module is a comprehensive workshop management system designed for automotive repair shops, service centers, and independent mechanics. It provides end-to-end management of workshop operations from customer intake to service completion and invoicing.

### 1.2 Key Features

#### Workshop Management
- **Intervention Lifecycle**: Complete management from estimate to completion
- **Bay Management**: Workshop bay allocation and scheduling
- **Equipment Tracking**: Tool and equipment availability management
- **Resource Planning**: Mechanic assignment and workload optimization

#### Customer & Vehicle Management
- **Customer Profiles**: Complete customer information and service history
- **Vehicle History**: Comprehensive service records and maintenance tracking
- **Service Reminders**: Automated maintenance scheduling and notifications
- **Warranty Tracking**: Service warranty management and claims

#### Parts & Inventory
- **Parts Catalog**: Comprehensive automotive parts database
- **Inventory Management**: Real-time stock tracking with automated reordering
- **Supplier Integration**: Direct integration with parts suppliers
- **Cost Management**: Parts pricing and margin control

#### Financial Operations
- **Estimate Generation**: Detailed service estimates with parts and labor
- **Work Order Management**: Service authorization and progress tracking
- **Automated Invoicing**: Seamless billing with tax calculations
- **Payment Processing**: Multiple payment methods and tracking

#### Analytics & Reporting
- **Workshop KPIs**: Performance metrics and productivity analysis
- **Financial Reporting**: Revenue, costs, and profitability analysis
- **Customer Analytics**: Service patterns and customer satisfaction
- **Mechanic Performance**: Individual and team productivity metrics

### 1.3 Dependencies

The GARAGE module integrates with the following core modules:

- **Users & Permissions**: Authentication, authorization, and role management
- **Vehicles**: Vehicle information and maintenance history
- **Files**: Document storage for estimates, invoices, and service documentation
- **Notifications**: Customer communication and internal alerts
- **Admin & SaaS**: Tenant management and billing integration
- **Observability**: Performance monitoring and analytics

### 1.4 Pricing and Business Model

- **Monthly Subscription**: 49€ per month per workshop
- **User Scaling**: Additional per-user fees for teams over base limit
- **Feature Tiers**: Basic, Professional, and Enterprise feature sets
- **Integration Costs**: Additional fees for premium supplier integrations

---

## 2. Business Domain Model

### 2.1 Core Aggregates

#### Workshop Aggregate
The workshop represents the physical location and operational context for all garage activities.

#### Intervention Aggregate
The intervention is the central business entity representing a service request from estimate through completion.

#### Mechanic Aggregate
Mechanics are specialized resources with skills, schedules, and performance tracking.

#### Parts Aggregate
Parts management including catalog, inventory, and supplier relationships.

#### Customer Aggregate (Extension)
Extends the core Users module with garage-specific customer information.

### 2.2 Domain Entities

#### Workshop Entities
```csharp
namespace DriveOps.Garage.Domain.Entities
{
    public class Workshop : AggregateRoot
    {
        public WorkshopId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string Name { get; private set; }
        public string RegistrationNumber { get; private set; }
        public Address Address { get; private set; }
        public ContactInfo ContactInfo { get; private set; }
        public WorkshopSettings Settings { get; private set; }
        public bool IsActive { get; private set; }
        
        private readonly List<WorkshopBay> _bays = new();
        public IReadOnlyCollection<WorkshopBay> Bays => _bays.AsReadOnly();
        
        private readonly List<WorkshopEquipment> _equipment = new();
        public IReadOnlyCollection<WorkshopEquipment> Equipment => _equipment.AsReadOnly();

        public void AddBay(string name, BayType type, bool hasLift)
        {
            var bay = new WorkshopBay(Id, name, type, hasLift);
            _bays.Add(bay);
            AddDomainEvent(new WorkshopBayAddedEvent(TenantId, Id, bay.Id));
        }

        public void AddEquipment(string name, EquipmentType type, string manufacturer, string model)
        {
            var equipment = new WorkshopEquipment(Id, name, type, manufacturer, model);
            _equipment.Add(equipment);
            AddDomainEvent(new WorkshopEquipmentAddedEvent(TenantId, Id, equipment.Id));
        }

        public void UpdateSettings(WorkshopSettings newSettings)
        {
            Settings = newSettings;
            AddDomainEvent(new WorkshopSettingsUpdatedEvent(TenantId, Id, newSettings));
        }
    }

    public class WorkshopBay : Entity
    {
        public WorkshopBayId Id { get; private set; }
        public WorkshopId WorkshopId { get; private set; }
        public string Name { get; private set; }
        public BayType Type { get; private set; }
        public bool HasLift { get; private set; }
        public BayStatus Status { get; private set; }
        public int MaxVehicleLength { get; private set; }
        public int MaxVehicleWidth { get; private set; }
        public bool IsActive { get; private set; }

        public void Reserve(InterventionId interventionId, DateTime from, DateTime to)
        {
            if (Status != BayStatus.Available)
                throw new DomainException("Bay is not available for reservation");

            Status = BayStatus.Reserved;
            AddDomainEvent(new BayReservedEvent(WorkshopId, Id, interventionId, from, to));
        }

        public void StartWork(InterventionId interventionId)
        {
            if (Status != BayStatus.Reserved)
                throw new DomainException("Bay must be reserved before starting work");

            Status = BayStatus.InUse;
            AddDomainEvent(new BayWorkStartedEvent(WorkshopId, Id, interventionId));
        }

        public void CompleteWork()
        {
            Status = BayStatus.Available;
            AddDomainEvent(new BayWorkCompletedEvent(WorkshopId, Id));
        }
    }

    public class WorkshopEquipment : Entity
    {
        public WorkshopEquipmentId Id { get; private set; }
        public WorkshopId WorkshopId { get; private set; }
        public string Name { get; private set; }
        public EquipmentType Type { get; private set; }
        public string Manufacturer { get; private set; }
        public string Model { get; private set; }
        public string SerialNumber { get; private set; }
        public DateTime PurchaseDate { get; private set; }
        public DateTime? WarrantyExpiry { get; private set; }
        public EquipmentStatus Status { get; private set; }
        public DateTime? LastMaintenanceDate { get; private set; }
        public DateTime? NextMaintenanceDate { get; private set; }

        public void ScheduleMaintenance(DateTime maintenanceDate)
        {
            NextMaintenanceDate = maintenanceDate;
            AddDomainEvent(new EquipmentMaintenanceScheduledEvent(WorkshopId, Id, maintenanceDate));
        }

        public void CompleteMaintenance()
        {
            LastMaintenanceDate = DateTime.UtcNow;
            NextMaintenanceDate = CalculateNextMaintenanceDate();
            AddDomainEvent(new EquipmentMaintenanceCompletedEvent(WorkshopId, Id, LastMaintenanceDate.Value));
        }
    }
}
```

#### Intervention Entities
```csharp
namespace DriveOps.Garage.Domain.Entities
{
    public class Intervention : AggregateRoot
    {
        public InterventionId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public WorkshopId WorkshopId { get; private set; }
        public string InterventionNumber { get; private set; }
        public VehicleId VehicleId { get; private set; }
        public UserId CustomerId { get; private set; }
        public InterventionType Type { get; private set; }
        public InterventionStatus Status { get; private set; }
        public Priority Priority { get; private set; }
        public string Description { get; private set; }
        public string? CustomerNotes { get; private set; }
        public int CurrentMileage { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? EstimatedCompletionDate { get; private set; }
        public DateTime? ActualCompletionDate { get; private set; }
        public UserId CreatedBy { get; private set; }
        public UserId? AssignedMechanic { get; private set; }
        public WorkshopBayId? AssignedBay { get; private set; }
        
        private readonly List<InterventionItem> _items = new();
        public IReadOnlyCollection<InterventionItem> Items => _items.AsReadOnly();
        
        private readonly List<InterventionStatusHistory> _statusHistory = new();
        public IReadOnlyCollection<InterventionStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

        public Money TotalLaborCost => _items.Where(i => i.Type == InterventionItemType.Labor)
                                            .Sum(i => i.TotalCost);
        public Money TotalPartsCost => _items.Where(i => i.Type == InterventionItemType.Part)
                                            .Sum(i => i.TotalCost);
        public Money TotalCost => TotalLaborCost + TotalPartsCost;

        public void AddLaborItem(string description, decimal hours, decimal hourlyRate, UserId mechanicId)
        {
            var laborItem = new InterventionItem(
                Id, 
                InterventionItemType.Labor, 
                description, 
                hours, 
                Money.FromDecimal(hourlyRate, "EUR"), 
                mechanicId
            );
            _items.Add(laborItem);
            AddDomainEvent(new InterventionLaborAddedEvent(TenantId, Id, laborItem.Id));
        }

        public void AddPartItem(PartId partId, string description, int quantity, Money unitPrice)
        {
            var partItem = new InterventionItem(
                Id,
                InterventionItemType.Part,
                description,
                quantity,
                unitPrice,
                partId
            );
            _items.Add(partItem);
            AddDomainEvent(new InterventionPartAddedEvent(TenantId, Id, partItem.Id, partId, quantity));
        }

        public void AssignMechanic(UserId mechanicId)
        {
            AssignedMechanic = mechanicId;
            ChangeStatus(InterventionStatus.Assigned, mechanicId);
            AddDomainEvent(new InterventionMechanicAssignedEvent(TenantId, Id, mechanicId));
        }

        public void AssignBay(WorkshopBayId bayId)
        {
            AssignedBay = bayId;
            AddDomainEvent(new InterventionBayAssignedEvent(TenantId, Id, bayId));
        }

        public void StartWork(UserId mechanicId)
        {
            if (Status != InterventionStatus.Assigned)
                throw new DomainException("Intervention must be assigned before starting work");

            ChangeStatus(InterventionStatus.InProgress, mechanicId);
            AddDomainEvent(new InterventionWorkStartedEvent(TenantId, Id, mechanicId));
        }

        public void CompleteWork(UserId completedBy, string completionNotes)
        {
            if (Status != InterventionStatus.InProgress)
                throw new DomainException("Intervention must be in progress to complete");

            ActualCompletionDate = DateTime.UtcNow;
            ChangeStatus(InterventionStatus.Completed, completedBy);
            AddDomainEvent(new InterventionCompletedEvent(TenantId, Id, completedBy, completionNotes));
        }

        public void GenerateInvoice(UserId generatedBy)
        {
            if (Status != InterventionStatus.Completed)
                throw new DomainException("Intervention must be completed before generating invoice");

            ChangeStatus(InterventionStatus.Invoiced, generatedBy);
            AddDomainEvent(new InterventionInvoiceGeneratedEvent(TenantId, Id, TotalCost));
        }

        private void ChangeStatus(InterventionStatus newStatus, UserId changedBy)
        {
            var previousStatus = Status;
            Status = newStatus;
            
            var statusChange = new InterventionStatusHistory(
                Id, 
                previousStatus, 
                newStatus, 
                DateTime.UtcNow, 
                changedBy
            );
            _statusHistory.Add(statusChange);
            
            AddDomainEvent(new InterventionStatusChangedEvent(TenantId, Id, previousStatus, newStatus, changedBy));
        }
    }

    public class InterventionItem : Entity
    {
        public InterventionItemId Id { get; private set; }
        public InterventionId InterventionId { get; private set; }
        public InterventionItemType Type { get; private set; }
        public string Description { get; private set; }
        public decimal Quantity { get; private set; }
        public Money UnitPrice { get; private set; }
        public Money TotalCost => Money.FromDecimal(UnitPrice.Amount * Quantity, UnitPrice.Currency);
        public PartId? PartId { get; private set; } // For parts
        public UserId? MechanicId { get; private set; } // For labor
        public DateTime CreatedAt { get; private set; }

        public void UpdateQuantity(decimal newQuantity)
        {
            if (newQuantity <= 0)
                throw new DomainException("Quantity must be greater than zero");

            Quantity = newQuantity;
        }

        public void UpdateUnitPrice(Money newUnitPrice)
        {
            UnitPrice = newUnitPrice;
        }
    }

    public class InterventionStatusHistory : Entity
    {
        public InterventionStatusHistoryId Id { get; private set; }
        public InterventionId InterventionId { get; private set; }
        public InterventionStatus FromStatus { get; private set; }
        public InterventionStatus ToStatus { get; private set; }
        public DateTime ChangedAt { get; private set; }
        public UserId ChangedBy { get; private set; }
        public string? Notes { get; private set; }
    }
}
```

#### Mechanic Entities
```csharp
namespace DriveOps.Garage.Domain.Entities
{
    public class Mechanic : AggregateRoot
    {
        public MechanicId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public WorkshopId WorkshopId { get; private set; }
        public UserId UserId { get; private set; } // Link to Users module
        public string EmployeeNumber { get; private set; }
        public MechanicLevel Level { get; private set; }
        public decimal HourlyRate { get; private set; }
        public DateTime HireDate { get; private set; }
        public bool IsActive { get; private set; }
        
        private readonly List<MechanicSkill> _skills = new();
        public IReadOnlyCollection<MechanicSkill> Skills => _skills.AsReadOnly();
        
        private readonly List<MechanicSchedule> _schedules = new();
        public IReadOnlyCollection<MechanicSchedule> Schedules => _schedules.AsReadOnly();

        public void AddSkill(SkillType skillType, SkillLevel level, DateTime? certificationDate = null)
        {
            var existingSkill = _skills.FirstOrDefault(s => s.SkillType == skillType);
            if (existingSkill != null)
            {
                existingSkill.UpdateLevel(level);
                return;
            }

            var skill = new MechanicSkill(Id, skillType, level, certificationDate);
            _skills.Add(skill);
            AddDomainEvent(new MechanicSkillAddedEvent(TenantId, Id, skillType, level));
        }

        public void UpdateHourlyRate(decimal newRate)
        {
            var previousRate = HourlyRate;
            HourlyRate = newRate;
            AddDomainEvent(new MechanicRateUpdatedEvent(TenantId, Id, previousRate, newRate));
        }

        public void SetSchedule(DateOnly date, TimeOnly startTime, TimeOnly endTime)
        {
            var schedule = new MechanicSchedule(Id, date, startTime, endTime);
            _schedules.Add(schedule);
            AddDomainEvent(new MechanicScheduleSetEvent(TenantId, Id, date, startTime, endTime));
        }

        public bool IsAvailable(DateTime startTime, DateTime endTime)
        {
            var date = DateOnly.FromDateTime(startTime);
            var schedule = _schedules.FirstOrDefault(s => s.Date == date);
            
            if (schedule == null) return false;
            
            var requestStart = TimeOnly.FromDateTime(startTime);
            var requestEnd = TimeOnly.FromDateTime(endTime);
            
            return requestStart >= schedule.StartTime && requestEnd <= schedule.EndTime;
        }

        public bool HasSkill(SkillType skillType, SkillLevel minimumLevel = SkillLevel.Basic)
        {
            var skill = _skills.FirstOrDefault(s => s.SkillType == skillType);
            return skill != null && skill.Level >= minimumLevel;
        }
    }

    public class MechanicSkill : Entity
    {
        public MechanicSkillId Id { get; private set; }
        public MechanicId MechanicId { get; private set; }
        public SkillType SkillType { get; private set; }
        public SkillLevel Level { get; private set; }
        public DateTime? CertificationDate { get; private set; }
        public DateTime? CertificationExpiry { get; private set; }
        public string? CertificationProvider { get; private set; }

        public void UpdateLevel(SkillLevel newLevel)
        {
            Level = newLevel;
        }

        public void UpdateCertification(DateTime? certificationDate, DateTime? expiryDate, string? provider)
        {
            CertificationDate = certificationDate;
            CertificationExpiry = expiryDate;
            CertificationProvider = provider;
        }

        public bool IsCertificationValid => CertificationExpiry == null || CertificationExpiry > DateTime.UtcNow;
    }

    public class MechanicSchedule : Entity
    {
        public MechanicScheduleId Id { get; private set; }
        public MechanicId MechanicId { get; private set; }
        public DateOnly Date { get; private set; }
        public TimeOnly StartTime { get; private set; }
        public TimeOnly EndTime { get; private set; }
        public bool IsAvailable { get; private set; }
        public string? Notes { get; private set; }

        public void MarkUnavailable(string reason)
        {
            IsAvailable = false;
            Notes = reason;
        }

        public void MarkAvailable()
        {
            IsAvailable = true;
            Notes = null;
        }

        public TimeSpan WorkingHours => EndTime - StartTime;
    }
}
```

#### Parts Entities
```csharp
namespace DriveOps.Garage.Domain.Entities
{
    public class Part : AggregateRoot
    {
        public PartId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string PartNumber { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public PartCategory Category { get; private set; }
        public string Manufacturer { get; private set; }
        public string? OemPartNumber { get; private set; }
        public Money ListPrice { get; private set; }
        public Money CostPrice { get; private set; }
        public int CurrentStock { get; private set; }
        public int MinimumStock { get; private set; }
        public int ReorderQuantity { get; private set; }
        public PartStatus Status { get; private set; }
        public FileId? ImageFileId { get; private set; }
        
        private readonly List<PartSupplier> _suppliers = new();
        public IReadOnlyCollection<PartSupplier> Suppliers => _suppliers.AsReadOnly();
        
        private readonly List<PartVehicleCompatibility> _compatibilities = new();
        public IReadOnlyCollection<PartVehicleCompatibility> Compatibilities => _compatibilities.AsReadOnly();

        public void UpdateStock(int newStock, StockMovementType movementType, string reference)
        {
            var previousStock = CurrentStock;
            CurrentStock = newStock;
            
            AddDomainEvent(new PartStockUpdatedEvent(TenantId, Id, previousStock, newStock, movementType, reference));
            
            if (CurrentStock <= MinimumStock)
            {
                AddDomainEvent(new PartLowStockEvent(TenantId, Id, CurrentStock, MinimumStock));
            }
        }

        public void AddSupplier(SupplierId supplierId, string supplierPartNumber, Money supplierPrice, int leadTimeDays)
        {
            var supplier = new PartSupplier(Id, supplierId, supplierPartNumber, supplierPrice, leadTimeDays);
            _suppliers.Add(supplier);
            AddDomainEvent(new PartSupplierAddedEvent(TenantId, Id, supplierId));
        }

        public void AddVehicleCompatibility(VehicleBrandId brandId, VehicleModelId? modelId, int? yearFrom, int? yearTo)
        {
            var compatibility = new PartVehicleCompatibility(Id, brandId, modelId, yearFrom, yearTo);
            _compatibilities.Add(compatibility);
            AddDomainEvent(new PartCompatibilityAddedEvent(TenantId, Id, brandId, modelId));
        }

        public bool IsCompatibleWith(VehicleBrandId brandId, VehicleModelId? modelId, int year)
        {
            return _compatibilities.Any(c => 
                c.BrandId == brandId &&
                (c.ModelId == null || c.ModelId == modelId) &&
                (c.YearFrom == null || c.YearFrom <= year) &&
                (c.YearTo == null || c.YearTo >= year));
        }

        public PartSupplier? GetPreferredSupplier()
        {
            return _suppliers.OrderBy(s => s.LeadTimeDays).ThenBy(s => s.Price.Amount).FirstOrDefault();
        }
    }

    public class PartSupplier : Entity
    {
        public PartSupplierId Id { get; private set; }
        public PartId PartId { get; private set; }
        public SupplierId SupplierId { get; private set; }
        public string SupplierPartNumber { get; private set; }
        public Money Price { get; private set; }
        public int LeadTimeDays { get; private set; }
        public bool IsPreferred { get; private set; }
        public DateTime LastUpdated { get; private set; }

        public void UpdatePrice(Money newPrice)
        {
            Price = newPrice;
            LastUpdated = DateTime.UtcNow;
        }

        public void SetAsPreferred()
        {
            IsPreferred = true;
        }
    }

    public class PartVehicleCompatibility : Entity
    {
        public PartVehicleCompatibilityId Id { get; private set; }
        public PartId PartId { get; private set; }
        public VehicleBrandId BrandId { get; private set; }
        public VehicleModelId? ModelId { get; private set; }
        public int? YearFrom { get; private set; }
        public int? YearTo { get; private set; }
        public string? Notes { get; private set; }
    }

    public class Supplier : AggregateRoot
    {
        public SupplierId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string Name { get; private set; }
        public string Code { get; private set; }
        public Address Address { get; private set; }
        public ContactInfo ContactInfo { get; private set; }
        public SupplierType Type { get; private set; }
        public PaymentTerms PaymentTerms { get; private set; }
        public bool IsActive { get; private set; }
        public decimal DiscountPercentage { get; private set; }
        
        private readonly List<SupplierApiConfig> _apiConfigs = new();
        public IReadOnlyCollection<SupplierApiConfig> ApiConfigs => _apiConfigs.AsReadOnly();

        public void AddApiConfig(string apiEndpoint, string apiKey, string catalogFormat)
        {
            var config = new SupplierApiConfig(Id, apiEndpoint, apiKey, catalogFormat);
            _apiConfigs.Add(config);
            AddDomainEvent(new SupplierApiConfigAddedEvent(TenantId, Id));
        }

        public void UpdateDiscount(decimal newDiscountPercentage)
        {
            DiscountPercentage = newDiscountPercentage;
            AddDomainEvent(new SupplierDiscountUpdatedEvent(TenantId, Id, newDiscountPercentage));
        }
    }

    public class SupplierApiConfig : Entity
    {
        public SupplierApiConfigId Id { get; private set; }
        public SupplierId SupplierId { get; private set; }
        public string ApiEndpoint { get; private set; }
        public string ApiKey { get; private set; }
        public string CatalogFormat { get; private set; } // JSON, XML, CSV
        public DateTime LastSync { get; private set; }
        public bool IsActive { get; private set; }
    }
}
```

### 2.3 Value Objects and Enums

#### Value Objects
```csharp
namespace DriveOps.Garage.Domain.ValueObjects
{
    public record Money(decimal Amount, string Currency)
    {
        public static Money FromDecimal(decimal amount, string currency) => new(amount, currency);
        
        public static Money operator +(Money left, Money right)
        {
            if (left.Currency != right.Currency)
                throw new InvalidOperationException("Cannot add money with different currencies");
            
            return new Money(left.Amount + right.Amount, left.Currency);
        }
        
        public static Money operator -(Money left, Money right)
        {
            if (left.Currency != right.Currency)
                throw new InvalidOperationException("Cannot subtract money with different currencies");
            
            return new Money(left.Amount - right.Amount, left.Currency);
        }
        
        public static Money operator *(Money money, decimal multiplier)
        {
            return new Money(money.Amount * multiplier, money.Currency);
        }
    }

    public record Address(
        string Street,
        string City,
        string PostalCode,
        string Country,
        string? Region = null
    );

    public record ContactInfo(
        string? Phone,
        string? Email,
        string? Website = null,
        string? ContactPerson = null
    );

    public record WorkshopSettings(
        TimeOnly OpeningTime,
        TimeOnly ClosingTime,
        DayOfWeek[] WorkingDays,
        decimal DefaultHourlyRate,
        string Currency,
        int DefaultEstimateValidityDays,
        bool AutoAssignMechanics,
        bool RequireCustomerApproval
    );

    public record PaymentTerms(
        int DueDays,
        decimal EarlyPaymentDiscountPercentage,
        int EarlyPaymentDays,
        decimal LatePaymentFeePercentage
    );
}
```

#### Enums
```csharp
namespace DriveOps.Garage.Domain.Enums
{
    public enum InterventionType
    {
        Maintenance = 1,
        Repair = 2,
        Diagnosis = 3,
        Inspection = 4,
        Warranty = 5,
        Recall = 6
    }

    public enum InterventionStatus
    {
        Draft = 1,
        Estimated = 2,
        Approved = 3,
        Assigned = 4,
        InProgress = 5,
        Completed = 6,
        Invoiced = 7,
        Paid = 8,
        Cancelled = 9
    }

    public enum Priority
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Urgent = 4
    }

    public enum InterventionItemType
    {
        Labor = 1,
        Part = 2,
        Subcontract = 3,
        Miscellaneous = 4
    }

    public enum BayType
    {
        Standard = 1,
        Hydraulic = 2,
        Inspection = 3,
        Painting = 4,
        Wheel = 5,
        Engine = 6
    }

    public enum BayStatus
    {
        Available = 1,
        Reserved = 2,
        InUse = 3,
        Maintenance = 4,
        OutOfOrder = 5
    }

    public enum EquipmentType
    {
        Lift = 1,
        WheelBalancer = 2,
        WheelAligner = 3,
        DiagnosticTool = 4,
        Compressor = 5,
        Welder = 6,
        PaintBooth = 7,
        WashStation = 8,
        Tool = 9
    }

    public enum EquipmentStatus
    {
        Available = 1,
        InUse = 2,
        Maintenance = 3,
        OutOfOrder = 4,
        Retired = 5
    }

    public enum MechanicLevel
    {
        Apprentice = 1,
        Junior = 2,
        Senior = 3,
        Expert = 4,
        Master = 5
    }

    public enum SkillType
    {
        GeneralMaintenance = 1,
        Engine = 2,
        Transmission = 3,
        Brakes = 4,
        Suspension = 5,
        Electrical = 6,
        AirConditioning = 7,
        Bodywork = 8,
        Painting = 9,
        Welding = 10,
        Diagnosis = 11,
        Hybrid = 12,
        Electric = 13
    }

    public enum SkillLevel
    {
        Basic = 1,
        Intermediate = 2,
        Advanced = 3,
        Expert = 4
    }

    public enum PartCategory
    {
        Engine = 1,
        Transmission = 2,
        Brakes = 3,
        Suspension = 4,
        Exhaust = 5,
        Electrical = 6,
        Cooling = 7,
        Fuel = 8,
        Filters = 9,
        Oils = 10,
        Tires = 11,
        Battery = 12,
        Lights = 13,
        Interior = 14,
        Exterior = 15,
        Tools = 16,
        Consumables = 17
    }

    public enum PartStatus
    {
        Active = 1,
        Discontinued = 2,
        Seasonal = 3,
        BackOrder = 4,
        Obsolete = 5
    }

    public enum StockMovementType
    {
        Purchase = 1,
        Sale = 2,
        Return = 3,
        Adjustment = 4,
        Transfer = 5,
        Waste = 6
    }

    public enum SupplierType
    {
        Manufacturer = 1,
        Distributor = 2,
        Wholesaler = 3,
        Retailer = 4,
        Online = 5
    }
}
```

---

## 3. Technical Architecture

### 3.1 Domain Layer Structure

The GARAGE module follows Domain-Driven Design (DDD) patterns with clear separation of concerns:

```
DriveOps.Garage.Domain/
├── Entities/
│   ├── Workshop.cs
│   ├── WorkshopBay.cs
│   ├── WorkshopEquipment.cs
│   ├── Intervention.cs
│   ├── InterventionItem.cs
│   ├── Mechanic.cs
│   ├── MechanicSkill.cs
│   ├── Part.cs
│   ├── PartSupplier.cs
│   └── Supplier.cs
├── ValueObjects/
│   ├── Money.cs
│   ├── Address.cs
│   ├── ContactInfo.cs
│   └── WorkshopSettings.cs
├── Enums/
│   ├── InterventionStatus.cs
│   ├── BayType.cs
│   ├── SkillType.cs
│   └── PartCategory.cs
├── Events/
│   ├── InterventionEvents.cs
│   ├── WorkshopEvents.cs
│   ├── MechanicEvents.cs
│   └── PartEvents.cs
├── Exceptions/
│   ├── GarageDomainException.cs
│   ├── InterventionException.cs
│   └── InventoryException.cs
├── Repositories/
│   ├── IWorkshopRepository.cs
│   ├── IInterventionRepository.cs
│   ├── IMechanicRepository.cs
│   └── IPartRepository.cs
└── Services/
    ├── IPricingService.cs
    ├── ISchedulingService.cs
    └── IInventoryService.cs
```

### 3.2 Application Layer - CQRS Implementation

#### Commands
```csharp
namespace DriveOps.Garage.Application.Commands
{
    // Workshop Commands
    public record CreateWorkshopCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        string Name,
        string RegistrationNumber,
        Address Address,
        ContactInfo ContactInfo
    ) : ICommand<Guid>;

    public record AddWorkshopBayCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        Guid WorkshopId,
        string Name,
        BayType Type,
        bool HasLift,
        int MaxVehicleLength,
        int MaxVehicleWidth
    ) : ICommand<Guid>;

    // Intervention Commands
    public record CreateInterventionCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        Guid WorkshopId,
        Guid VehicleId,
        Guid CustomerId,
        InterventionType Type,
        string Description,
        Priority Priority,
        int CurrentMileage
    ) : ICommand<Guid>;

    public record AssignMechanicToInterventionCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        Guid InterventionId,
        Guid MechanicId
    ) : ICommand;

    public record StartInterventionWorkCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        Guid InterventionId
    ) : ICommand;

    public record CompleteInterventionCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        Guid InterventionId,
        string CompletionNotes
    ) : ICommand;

    public record AddInterventionLaborCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        Guid InterventionId,
        string Description,
        decimal Hours,
        decimal HourlyRate,
        Guid MechanicId
    ) : ICommand;

    public record AddInterventionPartCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        Guid InterventionId,
        Guid PartId,
        int Quantity
    ) : ICommand;

    // Mechanic Commands
    public record CreateMechanicCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        Guid WorkshopId,
        Guid UserId,
        string EmployeeNumber,
        MechanicLevel Level,
        decimal HourlyRate
    ) : ICommand<Guid>;

    public record AddMechanicSkillCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        Guid MechanicId,
        SkillType SkillType,
        SkillLevel Level,
        DateTime? CertificationDate
    ) : ICommand;

    // Parts Commands
    public record CreatePartCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        string PartNumber,
        string Name,
        string Description,
        PartCategory Category,
        string Manufacturer,
        decimal ListPrice,
        decimal CostPrice,
        int MinimumStock,
        int ReorderQuantity
    ) : ICommand<Guid>;

    public record UpdatePartStockCommand(
        string TenantId,
        string UserId,
        string CorrelationId,
        Guid PartId,
        int NewStock,
        StockMovementType MovementType,
        string Reference
    ) : ICommand;
}
```

#### Command Handlers
```csharp
namespace DriveOps.Garage.Application.CommandHandlers
{
    public class CreateInterventionCommandHandler : BaseCommandHandler<CreateInterventionCommand, Result<Guid>>
    {
        private readonly IInterventionRepository _interventionRepository;
        private readonly IWorkshopRepository _workshopRepository;
        private readonly IVehicleRepository _vehicleRepository;
        private readonly IInterventionNumberService _numberService;

        public CreateInterventionCommandHandler(
            ITenantContext tenantContext,
            ILogger<CreateInterventionCommandHandler> logger,
            IUnitOfWork unitOfWork,
            IInterventionRepository interventionRepository,
            IWorkshopRepository workshopRepository,
            IVehicleRepository vehicleRepository,
            IInterventionNumberService numberService)
            : base(tenantContext, logger, unitOfWork)
        {
            _interventionRepository = interventionRepository;
            _workshopRepository = workshopRepository;
            _vehicleRepository = vehicleRepository;
            _numberService = numberService;
        }

        protected override async Task<Result<Guid>> HandleCommandAsync(
            CreateInterventionCommand request, 
            CancellationToken cancellationToken)
        {
            // Validate workshop exists and belongs to tenant
            var workshop = await _workshopRepository.GetByIdAsync(request.WorkshopId);
            if (workshop == null || workshop.TenantId != TenantContext.TenantId)
            {
                return Result<Guid>.Failure("Workshop not found or access denied");
            }

            // Validate vehicle exists and belongs to tenant
            var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
            if (vehicle == null || vehicle.TenantId != TenantContext.TenantId)
            {
                return Result<Guid>.Failure("Vehicle not found or access denied");
            }

            // Generate intervention number
            var interventionNumber = await _numberService.GenerateNextNumberAsync(request.WorkshopId);

            // Create intervention
            var intervention = new Intervention(
                TenantContext.TenantId,
                request.WorkshopId,
                interventionNumber,
                request.VehicleId,
                request.CustomerId,
                request.Type,
                request.Description,
                request.Priority,
                request.CurrentMileage,
                request.UserId
            );

            await _interventionRepository.AddAsync(intervention);

            return Result<Guid>.Success(intervention.Id);
        }
    }

    public class AssignMechanicToInterventionCommandHandler : BaseCommandHandler<AssignMechanicToInterventionCommand>
    {
        private readonly IInterventionRepository _interventionRepository;
        private readonly IMechanicRepository _mechanicRepository;
        private readonly ISchedulingService _schedulingService;

        public AssignMechanicToInterventionCommandHandler(
            ITenantContext tenantContext,
            ILogger<AssignMechanicToInterventionCommandHandler> logger,
            IUnitOfWork unitOfWork,
            IInterventionRepository interventionRepository,
            IMechanicRepository mechanicRepository,
            ISchedulingService schedulingService)
            : base(tenantContext, logger, unitOfWork)
        {
            _interventionRepository = interventionRepository;
            _mechanicRepository = mechanicRepository;
            _schedulingService = schedulingService;
        }

        protected override async Task<Result> HandleCommandAsync(
            AssignMechanicToInterventionCommand request, 
            CancellationToken cancellationToken)
        {
            var intervention = await _interventionRepository.GetByIdAsync(request.InterventionId);
            if (intervention == null || intervention.TenantId != TenantContext.TenantId)
            {
                return Result.Failure("Intervention not found or access denied");
            }

            var mechanic = await _mechanicRepository.GetByIdAsync(request.MechanicId);
            if (mechanic == null || mechanic.TenantId != TenantContext.TenantId)
            {
                return Result.Failure("Mechanic not found or access denied");
            }

            // Check mechanic availability
            var isAvailable = await _schedulingService.IsMechanicAvailableAsync(
                request.MechanicId, 
                intervention.EstimatedCompletionDate ?? DateTime.UtcNow.AddHours(2)
            );

            if (!isAvailable)
            {
                return Result.Failure("Mechanic is not available for the requested time");
            }

            intervention.AssignMechanic(request.MechanicId);

            return Result.Success();
        }
    }
}
```

#### Queries
```csharp
namespace DriveOps.Garage.Application.Queries
{
    // Workshop Queries
    public record GetWorkshopByIdQuery(
        string TenantId,
        string UserId,
        Guid WorkshopId
    ) : IQuery<WorkshopDto>;

    public record GetWorkshopBaysQuery(
        string TenantId,
        string UserId,
        Guid WorkshopId,
        BayStatus? Status = null
    ) : IQuery<List<WorkshopBayDto>>;

    // Intervention Queries
    public record GetInterventionByIdQuery(
        string TenantId,
        string UserId,
        Guid InterventionId
    ) : IQuery<InterventionDetailDto>;

    public record GetInterventionsQuery(
        string TenantId,
        string UserId,
        Guid? WorkshopId = null,
        InterventionStatus? Status = null,
        Guid? MechanicId = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        int Page = 1,
        int PageSize = 20
    ) : IQuery<PagedResult<InterventionSummaryDto>>;

    public record GetInterventionsByVehicleQuery(
        string TenantId,
        string UserId,
        Guid VehicleId,
        int Page = 1,
        int PageSize = 20
    ) : IQuery<PagedResult<InterventionSummaryDto>>;

    // Mechanic Queries
    public record GetMechanicByIdQuery(
        string TenantId,
        string UserId,
        Guid MechanicId
    ) : IQuery<MechanicDetailDto>;

    public record GetAvailableMechanicsQuery(
        string TenantId,
        string UserId,
        Guid WorkshopId,
        DateTime StartTime,
        DateTime EndTime,
        SkillType? RequiredSkill = null
    ) : IQuery<List<MechanicAvailabilityDto>>;

    // Parts Queries
    public record GetPartByIdQuery(
        string TenantId,
        string UserId,
        Guid PartId
    ) : IQuery<PartDetailDto>;

    public record SearchPartsQuery(
        string TenantId,
        string UserId,
        string? SearchTerm = null,
        PartCategory? Category = null,
        Guid? VehicleBrandId = null,
        Guid? VehicleModelId = null,
        bool? InStock = null,
        int Page = 1,
        int PageSize = 20
    ) : IQuery<PagedResult<PartSummaryDto>>;

    public record GetLowStockPartsQuery(
        string TenantId,
        string UserId,
        Guid? WorkshopId = null
    ) : IQuery<List<PartStockAlertDto>>;
}
```

#### Query Handlers
```csharp
namespace DriveOps.Garage.Application.QueryHandlers
{
    public class GetInterventionByIdQueryHandler : BaseQueryHandler<GetInterventionByIdQuery, InterventionDetailDto>
    {
        private readonly IInterventionRepository _interventionRepository;
        private readonly IMapper _mapper;

        public GetInterventionByIdQueryHandler(
            ITenantContext tenantContext,
            ILogger<GetInterventionByIdQueryHandler> logger,
            IInterventionRepository interventionRepository,
            IMapper mapper)
            : base(tenantContext, logger)
        {
            _interventionRepository = interventionRepository;
            _mapper = mapper;
        }

        protected override async Task<Result<InterventionDetailDto>> HandleQueryAsync(
            GetInterventionByIdQuery request, 
            CancellationToken cancellationToken)
        {
            var intervention = await _interventionRepository.GetByIdWithDetailsAsync(request.InterventionId);
            
            if (intervention == null || intervention.TenantId != TenantContext.TenantId)
            {
                return Result<InterventionDetailDto>.Failure("Intervention not found or access denied");
            }

            var dto = _mapper.Map<InterventionDetailDto>(intervention);
            return Result<InterventionDetailDto>.Success(dto);
        }
    }

    public class GetAvailableMechanicsQueryHandler : BaseQueryHandler<GetAvailableMechanicsQuery, List<MechanicAvailabilityDto>>
    {
        private readonly IMechanicRepository _mechanicRepository;
        private readonly ISchedulingService _schedulingService;

        public GetAvailableMechanicsQueryHandler(
            ITenantContext tenantContext,
            ILogger<GetAvailableMechanicsQueryHandler> logger,
            IMechanicRepository mechanicRepository,
            ISchedulingService schedulingService)
            : base(tenantContext, logger)
        {
            _mechanicRepository = mechanicRepository;
            _schedulingService = schedulingService;
        }

        protected override async Task<Result<List<MechanicAvailabilityDto>>> HandleQueryAsync(
            GetAvailableMechanicsQuery request, 
            CancellationToken cancellationToken)
        {
            var mechanics = await _mechanicRepository.GetByWorkshopIdAsync(request.WorkshopId);
            
            var availableMechanics = new List<MechanicAvailabilityDto>();
            
            foreach (var mechanic in mechanics.Where(m => m.IsActive))
            {
                var isAvailable = await _schedulingService.IsMechanicAvailableAsync(
                    mechanic.Id, 
                    request.StartTime, 
                    request.EndTime
                );

                if (isAvailable)
                {
                    // Check skill requirement if specified
                    if (request.RequiredSkill.HasValue && !mechanic.HasSkill(request.RequiredSkill.Value))
                        continue;

                    availableMechanics.Add(new MechanicAvailabilityDto
                    {
                        MechanicId = mechanic.Id,
                        Name = $"{mechanic.User.FirstName} {mechanic.User.LastName}",
                        Level = mechanic.Level,
                        HourlyRate = mechanic.HourlyRate,
                        Skills = mechanic.Skills.Select(s => s.SkillType).ToList()
                    });
                }
            }

            return Result<List<MechanicAvailabilityDto>>.Success(availableMechanics);
        }
    }
}
```

---

## 4. Database Schema

### 4.1 PostgreSQL Schema

```sql
-- GARAGE Module Schema
CREATE SCHEMA IF NOT EXISTS garage;

-- Workshops table
CREATE TABLE garage.workshops (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    registration_number VARCHAR(100) NOT NULL,
    street VARCHAR(255) NOT NULL,
    city VARCHAR(100) NOT NULL,
    postal_code VARCHAR(20) NOT NULL,
    country VARCHAR(100) NOT NULL,
    region VARCHAR(100),
    phone VARCHAR(50),
    email VARCHAR(255),
    website VARCHAR(255),
    contact_person VARCHAR(255),
    opening_time TIME NOT NULL,
    closing_time TIME NOT NULL,
    working_days INTEGER[] NOT NULL, -- Array of day numbers (1=Monday, 7=Sunday)
    default_hourly_rate DECIMAL(10,2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    default_estimate_validity_days INTEGER NOT NULL DEFAULT 30,
    auto_assign_mechanics BOOLEAN NOT NULL DEFAULT false,
    require_customer_approval BOOLEAN NOT NULL DEFAULT true,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Workshop bays table
CREATE TABLE garage.workshop_bays (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workshop_id UUID NOT NULL REFERENCES garage.workshops(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    bay_type INTEGER NOT NULL, -- 1: Standard, 2: Hydraulic, 3: Inspection, etc.
    has_lift BOOLEAN NOT NULL DEFAULT false,
    max_vehicle_length INTEGER, -- in centimeters
    max_vehicle_width INTEGER, -- in centimeters
    status INTEGER NOT NULL DEFAULT 1, -- 1: Available, 2: Reserved, 3: InUse, etc.
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Workshop equipment table
CREATE TABLE garage.workshop_equipment (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workshop_id UUID NOT NULL REFERENCES garage.workshops(id) ON DELETE CASCADE,
    name VARCHAR(255) NOT NULL,
    equipment_type INTEGER NOT NULL, -- 1: Lift, 2: WheelBalancer, etc.
    manufacturer VARCHAR(255) NOT NULL,
    model VARCHAR(255) NOT NULL,
    serial_number VARCHAR(255),
    purchase_date DATE NOT NULL,
    warranty_expiry DATE,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Available, 2: InUse, 3: Maintenance, etc.
    last_maintenance_date DATE,
    next_maintenance_date DATE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Interventions table
CREATE TABLE garage.interventions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    workshop_id UUID NOT NULL REFERENCES garage.workshops(id),
    intervention_number VARCHAR(50) NOT NULL,
    vehicle_id UUID NOT NULL, -- References vehicles.vehicles(id)
    customer_id UUID NOT NULL, -- References users.users(id)
    intervention_type INTEGER NOT NULL, -- 1: Maintenance, 2: Repair, etc.
    status INTEGER NOT NULL DEFAULT 1, -- 1: Draft, 2: Estimated, etc.
    priority INTEGER NOT NULL DEFAULT 2, -- 1: Low, 2: Medium, 3: High, 4: Urgent
    description TEXT NOT NULL,
    customer_notes TEXT,
    current_mileage INTEGER NOT NULL,
    estimated_completion_date TIMESTAMP WITH TIME ZONE,
    actual_completion_date TIMESTAMP WITH TIME ZONE,
    created_by UUID NOT NULL, -- References users.users(id)
    assigned_mechanic_id UUID, -- References garage.mechanics(id)
    assigned_bay_id UUID REFERENCES garage.workshop_bays(id),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Intervention items (labor and parts)
CREATE TABLE garage.intervention_items (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    intervention_id UUID NOT NULL REFERENCES garage.interventions(id) ON DELETE CASCADE,
    item_type INTEGER NOT NULL, -- 1: Labor, 2: Part, 3: Subcontract, 4: Miscellaneous
    description TEXT NOT NULL,
    quantity DECIMAL(10,3) NOT NULL,
    unit_price_amount DECIMAL(10,2) NOT NULL,
    unit_price_currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    total_cost_amount DECIMAL(10,2) GENERATED ALWAYS AS (quantity * unit_price_amount) STORED,
    part_id UUID, -- References garage.parts(id) for parts
    mechanic_id UUID, -- References garage.mechanics(id) for labor
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Intervention status history
CREATE TABLE garage.intervention_status_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    intervention_id UUID NOT NULL REFERENCES garage.interventions(id) ON DELETE CASCADE,
    from_status INTEGER NOT NULL,
    to_status INTEGER NOT NULL,
    changed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    changed_by UUID NOT NULL, -- References users.users(id)
    notes TEXT
);

-- Mechanics table
CREATE TABLE garage.mechanics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    workshop_id UUID NOT NULL REFERENCES garage.workshops(id),
    user_id UUID NOT NULL, -- References users.users(id)
    employee_number VARCHAR(50) NOT NULL,
    level INTEGER NOT NULL, -- 1: Apprentice, 2: Junior, 3: Senior, etc.
    hourly_rate DECIMAL(10,2) NOT NULL,
    hire_date DATE NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Mechanic skills table
CREATE TABLE garage.mechanic_skills (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mechanic_id UUID NOT NULL REFERENCES garage.mechanics(id) ON DELETE CASCADE,
    skill_type INTEGER NOT NULL, -- 1: GeneralMaintenance, 2: Engine, etc.
    skill_level INTEGER NOT NULL, -- 1: Basic, 2: Intermediate, 3: Advanced, 4: Expert
    certification_date DATE,
    certification_expiry DATE,
    certification_provider VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Mechanic schedules table
CREATE TABLE garage.mechanic_schedules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mechanic_id UUID NOT NULL REFERENCES garage.mechanics(id) ON DELETE CASCADE,
    schedule_date DATE NOT NULL,
    start_time TIME NOT NULL,
    end_time TIME NOT NULL,
    is_available BOOLEAN NOT NULL DEFAULT true,
    notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Parts catalog table
CREATE TABLE garage.parts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    part_number VARCHAR(100) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    category INTEGER NOT NULL, -- 1: Engine, 2: Transmission, etc.
    manufacturer VARCHAR(255) NOT NULL,
    oem_part_number VARCHAR(100),
    list_price_amount DECIMAL(10,2) NOT NULL,
    list_price_currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    cost_price_amount DECIMAL(10,2) NOT NULL,
    cost_price_currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    current_stock INTEGER NOT NULL DEFAULT 0,
    minimum_stock INTEGER NOT NULL DEFAULT 0,
    reorder_quantity INTEGER NOT NULL DEFAULT 1,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: Discontinued, etc.
    image_file_id UUID, -- References files.file_metadata(id)
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Parts suppliers table
CREATE TABLE garage.parts_suppliers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    part_id UUID NOT NULL REFERENCES garage.parts(id) ON DELETE CASCADE,
    supplier_id UUID NOT NULL REFERENCES garage.suppliers(id),
    supplier_part_number VARCHAR(100) NOT NULL,
    price_amount DECIMAL(10,2) NOT NULL,
    price_currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    lead_time_days INTEGER NOT NULL,
    is_preferred BOOLEAN NOT NULL DEFAULT false,
    last_updated TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Part vehicle compatibility table
CREATE TABLE garage.part_vehicle_compatibility (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    part_id UUID NOT NULL REFERENCES garage.parts(id) ON DELETE CASCADE,
    brand_id UUID NOT NULL, -- References vehicles.vehicle_brands(id)
    model_id UUID, -- References vehicles.vehicle_models(id), null for all models
    year_from INTEGER,
    year_to INTEGER,
    notes TEXT
);

-- Suppliers table
CREATE TABLE garage.suppliers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    code VARCHAR(50) NOT NULL,
    supplier_type INTEGER NOT NULL, -- 1: Manufacturer, 2: Distributor, etc.
    street VARCHAR(255),
    city VARCHAR(100),
    postal_code VARCHAR(20),
    country VARCHAR(100),
    region VARCHAR(100),
    phone VARCHAR(50),
    email VARCHAR(255),
    website VARCHAR(255),
    contact_person VARCHAR(255),
    due_days INTEGER NOT NULL DEFAULT 30,
    early_payment_discount_percentage DECIMAL(5,2) NOT NULL DEFAULT 0,
    early_payment_days INTEGER NOT NULL DEFAULT 10,
    late_payment_fee_percentage DECIMAL(5,2) NOT NULL DEFAULT 0,
    discount_percentage DECIMAL(5,2) NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Supplier API configurations table
CREATE TABLE garage.supplier_api_configs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    supplier_id UUID NOT NULL REFERENCES garage.suppliers(id) ON DELETE CASCADE,
    api_endpoint VARCHAR(500) NOT NULL,
    api_key VARCHAR(500) NOT NULL,
    catalog_format VARCHAR(20) NOT NULL, -- JSON, XML, CSV
    last_sync TIMESTAMP WITH TIME ZONE,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Estimates table
CREATE TABLE garage.estimates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    intervention_id UUID NOT NULL REFERENCES garage.interventions(id),
    estimate_number VARCHAR(50) NOT NULL,
    total_labor_amount DECIMAL(10,2) NOT NULL,
    total_parts_amount DECIMAL(10,2) NOT NULL,
    total_amount DECIMAL(10,2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    valid_until DATE NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Draft, 2: Sent, 3: Approved, 4: Rejected, 5: Expired
    notes TEXT,
    created_by UUID NOT NULL, -- References users.users(id)
    approved_by UUID, -- References users.users(id)
    approved_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Work orders table
CREATE TABLE garage.work_orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    intervention_id UUID NOT NULL REFERENCES garage.interventions(id),
    estimate_id UUID REFERENCES garage.estimates(id),
    work_order_number VARCHAR(50) NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Created, 2: InProgress, 3: Completed, 4: Cancelled
    started_at TIMESTAMP WITH TIME ZONE,
    completed_at TIMESTAMP WITH TIME ZONE,
    created_by UUID NOT NULL, -- References users.users(id)
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Invoices table
CREATE TABLE garage.invoices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    intervention_id UUID NOT NULL REFERENCES garage.interventions(id),
    work_order_id UUID REFERENCES garage.work_orders(id),
    invoice_number VARCHAR(50) NOT NULL,
    total_labor_amount DECIMAL(10,2) NOT NULL,
    total_parts_amount DECIMAL(10,2) NOT NULL,
    subtotal_amount DECIMAL(10,2) NOT NULL,
    tax_amount DECIMAL(10,2) NOT NULL,
    total_amount DECIMAL(10,2) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    due_date DATE NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Draft, 2: Sent, 3: Paid, 4: Overdue, 5: Cancelled
    payment_method VARCHAR(50),
    payment_reference VARCHAR(100),
    paid_at TIMESTAMP WITH TIME ZONE,
    notes TEXT,
    created_by UUID NOT NULL, -- References users.users(id)
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Vehicle service history (extends core vehicles module)
CREATE TABLE garage.vehicle_service_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    vehicle_id UUID NOT NULL, -- References vehicles.vehicles(id)
    intervention_id UUID NOT NULL REFERENCES garage.interventions(id),
    service_date DATE NOT NULL,
    mileage INTEGER NOT NULL,
    service_type INTEGER NOT NULL, -- 1: Maintenance, 2: Repair, etc.
    description TEXT NOT NULL,
    cost_amount DECIMAL(10,2) NOT NULL,
    cost_currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    warranty_until DATE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Maintenance schedules table
CREATE TABLE garage.maintenance_schedules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    vehicle_id UUID NOT NULL, -- References vehicles.vehicles(id)
    service_type VARCHAR(100) NOT NULL,
    description TEXT NOT NULL,
    interval_mileage INTEGER, -- Service every X kilometers
    interval_months INTEGER, -- Service every X months
    last_service_date DATE,
    last_service_mileage INTEGER,
    next_service_date DATE,
    next_service_mileage INTEGER,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
```

### 4.2 Database Indexes

```sql
-- Performance indexes for GARAGE module

-- Workshops indexes
CREATE INDEX idx_workshops_tenant_id ON garage.workshops(tenant_id);
CREATE UNIQUE INDEX idx_workshops_tenant_registration ON garage.workshops(tenant_id, registration_number);

-- Workshop bays indexes
CREATE INDEX idx_workshop_bays_workshop_id ON garage.workshop_bays(workshop_id);
CREATE INDEX idx_workshop_bays_status ON garage.workshop_bays(status) WHERE is_active = true;

-- Interventions indexes
CREATE INDEX idx_interventions_tenant_id ON garage.interventions(tenant_id);
CREATE INDEX idx_interventions_workshop_id ON garage.interventions(workshop_id);
CREATE INDEX idx_interventions_vehicle_id ON garage.interventions(vehicle_id);
CREATE INDEX idx_interventions_customer_id ON garage.interventions(customer_id);
CREATE INDEX idx_interventions_status ON garage.interventions(status);
CREATE INDEX idx_interventions_assigned_mechanic ON garage.interventions(assigned_mechanic_id);
CREATE INDEX idx_interventions_created_at ON garage.interventions(created_at DESC);
CREATE UNIQUE INDEX idx_interventions_tenant_number ON garage.interventions(tenant_id, intervention_number);

-- Intervention items indexes
CREATE INDEX idx_intervention_items_intervention_id ON garage.intervention_items(intervention_id);
CREATE INDEX idx_intervention_items_part_id ON garage.intervention_items(part_id) WHERE part_id IS NOT NULL;

-- Mechanics indexes
CREATE INDEX idx_mechanics_tenant_id ON garage.mechanics(tenant_id);
CREATE INDEX idx_mechanics_workshop_id ON garage.mechanics(workshop_id);
CREATE INDEX idx_mechanics_user_id ON garage.mechanics(user_id);
CREATE UNIQUE INDEX idx_mechanics_tenant_employee ON garage.mechanics(tenant_id, employee_number);

-- Mechanic schedules indexes
CREATE INDEX idx_mechanic_schedules_mechanic_date ON garage.mechanic_schedules(mechanic_id, schedule_date);
CREATE INDEX idx_mechanic_schedules_date ON garage.mechanic_schedules(schedule_date);

-- Parts indexes
CREATE INDEX idx_parts_tenant_id ON garage.parts(tenant_id);
CREATE INDEX idx_parts_category ON garage.parts(category);
CREATE INDEX idx_parts_manufacturer ON garage.parts(manufacturer);
CREATE INDEX idx_parts_status ON garage.parts(status);
CREATE INDEX idx_parts_stock_level ON garage.parts(current_stock, minimum_stock) WHERE current_stock <= minimum_stock;
CREATE UNIQUE INDEX idx_parts_tenant_number ON garage.parts(tenant_id, part_number);
CREATE INDEX idx_parts_search_text ON garage.parts USING gin(to_tsvector('english', name || ' ' || description));

-- Part compatibility indexes
CREATE INDEX idx_part_compatibility_part_id ON garage.part_vehicle_compatibility(part_id);
CREATE INDEX idx_part_compatibility_brand_model ON garage.part_vehicle_compatibility(brand_id, model_id);

-- Suppliers indexes
CREATE INDEX idx_suppliers_tenant_id ON garage.suppliers(tenant_id);
CREATE UNIQUE INDEX idx_suppliers_tenant_code ON garage.suppliers(tenant_id, code);

-- Estimates/Work Orders/Invoices indexes
CREATE INDEX idx_estimates_tenant_id ON garage.estimates(tenant_id);
CREATE INDEX idx_estimates_intervention_id ON garage.estimates(intervention_id);
CREATE INDEX idx_work_orders_tenant_id ON garage.work_orders(tenant_id);
CREATE INDEX idx_work_orders_intervention_id ON garage.work_orders(intervention_id);
CREATE INDEX idx_invoices_tenant_id ON garage.invoices(tenant_id);
CREATE INDEX idx_invoices_intervention_id ON garage.invoices(intervention_id);
CREATE INDEX idx_invoices_due_date ON garage.invoices(due_date) WHERE status IN (1, 2); -- Draft, Sent

-- Service history indexes
CREATE INDEX idx_service_history_tenant_vehicle ON garage.vehicle_service_history(tenant_id, vehicle_id);
CREATE INDEX idx_service_history_date ON garage.vehicle_service_history(service_date DESC);

-- Maintenance schedules indexes
CREATE INDEX idx_maintenance_schedules_tenant_vehicle ON garage.maintenance_schedules(tenant_id, vehicle_id);
CREATE INDEX idx_maintenance_schedules_next_service ON garage.maintenance_schedules(next_service_date) WHERE is_active = true;
```

---

## 5. Application Layer

### 5.1 Application Services

#### Domain Services
```csharp
namespace DriveOps.Garage.Domain.Services
{
    public interface IPricingService
    {
        Task<Money> CalculateLaborCostAsync(Guid mechanicId, decimal hours);
        Task<Money> CalculatePartCostAsync(Guid partId, int quantity, Guid? customerId = null);
        Task<Money> CalculateInterventionTotalAsync(Guid interventionId);
        Task<decimal> GetApplicableTaxRateAsync(Guid customerId);
    }

    public interface ISchedulingService
    {
        Task<bool> IsMechanicAvailableAsync(Guid mechanicId, DateTime startTime, DateTime endTime);
        Task<bool> IsBayAvailableAsync(Guid bayId, DateTime startTime, DateTime endTime);
        Task<List<Guid>> FindAvailableMechanicsAsync(Guid workshopId, DateTime startTime, DateTime endTime, SkillType? requiredSkill = null);
        Task<Guid?> FindAvailableBayAsync(Guid workshopId, DateTime startTime, DateTime endTime, BayType? preferredType = null);
        Task ScheduleInterventionAsync(Guid interventionId, DateTime startTime, DateTime estimatedEndTime);
    }

    public interface IInventoryService
    {
        Task<bool> IsPartInStockAsync(Guid partId, int requiredQuantity);
        Task ReservePartsAsync(Guid interventionId, List<(Guid PartId, int Quantity)> parts);
        Task ConsumePartsAsync(Guid interventionId);
        Task CheckLowStockAndReorderAsync();
        Task<List<Guid>> GetLowStockPartsAsync();
    }

    public interface IInterventionNumberService
    {
        Task<string> GenerateNextNumberAsync(Guid workshopId);
    }
}
```

#### Application Service Implementations
```csharp
namespace DriveOps.Garage.Application.Services
{
    public class PricingService : IPricingService
    {
        private readonly IMechanicRepository _mechanicRepository;
        private readonly IPartRepository _partRepository;
        private readonly IInterventionRepository _interventionRepository;
        private readonly ITaxService _taxService;

        public async Task<Money> CalculateLaborCostAsync(Guid mechanicId, decimal hours)
        {
            var mechanic = await _mechanicRepository.GetByIdAsync(mechanicId);
            if (mechanic == null)
                throw new NotFoundException($"Mechanic {mechanicId} not found");

            var totalCost = mechanic.HourlyRate * hours;
            return Money.FromDecimal(totalCost, "EUR");
        }

        public async Task<Money> CalculatePartCostAsync(Guid partId, int quantity, Guid? customerId = null)
        {
            var part = await _partRepository.GetByIdAsync(partId);
            if (part == null)
                throw new NotFoundException($"Part {partId} not found");

            // Apply customer-specific discounts if applicable
            var unitPrice = part.ListPrice.Amount;
            if (customerId.HasValue)
            {
                // Check for customer discounts
                var discount = await GetCustomerDiscountAsync(customerId.Value);
                unitPrice *= (1 - discount);
            }

            var totalCost = unitPrice * quantity;
            return Money.FromDecimal(totalCost, part.ListPrice.Currency);
        }

        public async Task<Money> CalculateInterventionTotalAsync(Guid interventionId)
        {
            var intervention = await _interventionRepository.GetByIdWithItemsAsync(interventionId);
            if (intervention == null)
                throw new NotFoundException($"Intervention {interventionId} not found");

            var subtotal = intervention.TotalCost;
            var taxRate = await GetApplicableTaxRateAsync(intervention.CustomerId);
            var taxAmount = subtotal.Amount * (taxRate / 100);
            
            return Money.FromDecimal(subtotal.Amount + taxAmount, subtotal.Currency);
        }

        public async Task<decimal> GetApplicableTaxRateAsync(Guid customerId)
        {
            // Get tax rate based on customer location and business rules
            return 20.0m; // Default VAT rate for automotive services
        }

        private async Task<decimal> GetCustomerDiscountAsync(Guid customerId)
        {
            // Implement customer loyalty discount logic
            return 0.0m;
        }
    }

    public class SchedulingService : ISchedulingService
    {
        private readonly IMechanicRepository _mechanicRepository;
        private readonly IWorkshopRepository _workshopRepository;
        private readonly IInterventionRepository _interventionRepository;

        public async Task<bool> IsMechanicAvailableAsync(Guid mechanicId, DateTime startTime, DateTime endTime)
        {
            var mechanic = await _mechanicRepository.GetByIdWithSchedulesAsync(mechanicId);
            if (mechanic == null || !mechanic.IsActive)
                return false;

            // Check if mechanic is available during the requested time
            return mechanic.IsAvailable(startTime, endTime);
        }

        public async Task<bool> IsBayAvailableAsync(Guid bayId, DateTime startTime, DateTime endTime)
        {
            var bay = await _workshopRepository.GetBayByIdAsync(bayId);
            if (bay == null || !bay.IsActive || bay.Status != BayStatus.Available)
                return false;

            // Check for overlapping reservations
            var overlappingInterventions = await _interventionRepository
                .GetByBayAndTimeRangeAsync(bayId, startTime, endTime);
            
            return !overlappingInterventions.Any();
        }

        public async Task<List<Guid>> FindAvailableMechanicsAsync(
            Guid workshopId, 
            DateTime startTime, 
            DateTime endTime, 
            SkillType? requiredSkill = null)
        {
            var mechanics = await _mechanicRepository.GetByWorkshopIdAsync(workshopId);
            var availableMechanics = new List<Guid>();

            foreach (var mechanic in mechanics.Where(m => m.IsActive))
            {
                if (requiredSkill.HasValue && !mechanic.HasSkill(requiredSkill.Value))
                    continue;

                if (await IsMechanicAvailableAsync(mechanic.Id, startTime, endTime))
                {
                    availableMechanics.Add(mechanic.Id);
                }
            }

            return availableMechanics;
        }

        public async Task<Guid?> FindAvailableBayAsync(
            Guid workshopId, 
            DateTime startTime, 
            DateTime endTime, 
            BayType? preferredType = null)
        {
            var workshop = await _workshopRepository.GetByIdWithBaysAsync(workshopId);
            if (workshop == null)
                return null;

            var availableBays = workshop.Bays
                .Where(b => b.IsActive && b.Status == BayStatus.Available)
                .Where(b => !preferredType.HasValue || b.Type == preferredType.Value)
                .OrderBy(b => preferredType.HasValue && b.Type == preferredType.Value ? 0 : 1);

            foreach (var bay in availableBays)
            {
                if (await IsBayAvailableAsync(bay.Id, startTime, endTime))
                {
                    return bay.Id;
                }
            }

            return null;
        }

        public async Task ScheduleInterventionAsync(Guid interventionId, DateTime startTime, DateTime estimatedEndTime)
        {
            var intervention = await _interventionRepository.GetByIdAsync(interventionId);
            if (intervention == null)
                throw new NotFoundException($"Intervention {interventionId} not found");

            // Find available mechanic and bay
            var availableMechanics = await FindAvailableMechanicsAsync(
                intervention.WorkshopId, 
                startTime, 
                estimatedEndTime
            );

            if (!availableMechanics.Any())
                throw new InvalidOperationException("No mechanics available for the requested time");

            var availableBay = await FindAvailableBayAsync(
                intervention.WorkshopId, 
                startTime, 
                estimatedEndTime
            );

            if (!availableBay.HasValue)
                throw new InvalidOperationException("No bays available for the requested time");

            // Assign mechanic and bay
            intervention.AssignMechanic(availableMechanics.First());
            intervention.AssignBay(availableBay.Value);
            
            // Reserve the bay
            var bay = await _workshopRepository.GetBayByIdAsync(availableBay.Value);
            bay.Reserve(interventionId, startTime, estimatedEndTime);
        }
    }

    public class InventoryService : IInventoryService
    {
        private readonly IPartRepository _partRepository;
        private readonly IInterventionRepository _interventionRepository;
        private readonly ISupplierService _supplierService;
        private readonly INotificationService _notificationService;

        public async Task<bool> IsPartInStockAsync(Guid partId, int requiredQuantity)
        {
            var part = await _partRepository.GetByIdAsync(partId);
            return part != null && part.CurrentStock >= requiredQuantity;
        }

        public async Task ReservePartsAsync(Guid interventionId, List<(Guid PartId, int Quantity)> parts)
        {
            foreach (var (partId, quantity) in parts)
            {
                if (!await IsPartInStockAsync(partId, quantity))
                {
                    throw new InsufficientStockException($"Insufficient stock for part {partId}");
                }

                var part = await _partRepository.GetByIdAsync(partId);
                var newStock = part.CurrentStock - quantity;
                part.UpdateStock(newStock, StockMovementType.Sale, $"INT-{interventionId}");
            }
        }

        public async Task ConsumePartsAsync(Guid interventionId)
        {
            var intervention = await _interventionRepository.GetByIdWithItemsAsync(interventionId);
            if (intervention == null)
                return;

            var partItems = intervention.Items.Where(i => i.Type == InterventionItemType.Part && i.PartId.HasValue);
            
            foreach (var item in partItems)
            {
                var part = await _partRepository.GetByIdAsync(item.PartId.Value);
                if (part != null)
                {
                    // Parts were already reserved, this confirms consumption
                    await CheckLowStockAsync(part);
                }
            }
        }

        public async Task CheckLowStockAndReorderAsync()
        {
            var lowStockParts = await _partRepository.GetLowStockPartsAsync();
            
            foreach (var part in lowStockParts)
            {
                await TriggerReorderAsync(part);
            }
        }

        public async Task<List<Guid>> GetLowStockPartsAsync()
        {
            var lowStockParts = await _partRepository.GetLowStockPartsAsync();
            return lowStockParts.Select(p => p.Id).ToList();
        }

        private async Task CheckLowStockAsync(Part part)
        {
            if (part.CurrentStock <= part.MinimumStock)
            {
                await TriggerReorderAsync(part);
                await _notificationService.SendLowStockAlertAsync(part.Id, part.CurrentStock, part.MinimumStock);
            }
        }

        private async Task TriggerReorderAsync(Part part)
        {
            var preferredSupplier = part.GetPreferredSupplier();
            if (preferredSupplier != null)
            {
                await _supplierService.CreatePurchaseOrderAsync(
                    preferredSupplier.SupplierId, 
                    part.Id, 
                    part.ReorderQuantity
                );
            }
        }
    }
}
```

---

## 6. API Design

### 6.1 REST API Controllers

#### Workshop Controller
```csharp
namespace DriveOps.Garage.Api.Controllers
{
    [ApiController]
    [Route("api/garage/workshops")]
    [Authorize]
    public class WorkshopsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public WorkshopsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<ActionResult<Guid>> CreateWorkshop([FromBody] CreateWorkshopRequest request)
        {
            var command = new CreateWorkshopCommand(
                User.GetTenantId(),
                User.GetUserId(),
                Guid.NewGuid().ToString(),
                request.Name,
                request.RegistrationNumber,
                request.Address,
                request.ContactInfo
            );

            var result = await _mediator.Send(command);
            
            if (result.IsFailure)
                return BadRequest(result.Error);
                
            return CreatedAtAction(nameof(GetWorkshop), new { id = result.Value }, result.Value);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WorkshopDto>> GetWorkshop(Guid id)
        {
            var query = new GetWorkshopByIdQuery(User.GetTenantId(), User.GetUserId(), id);
            var result = await _mediator.Send(query);
            
            if (result.IsFailure)
                return NotFound(result.Error);
                
            return Ok(result.Value);
        }

        [HttpPost("{id}/bays")]
        public async Task<ActionResult<Guid>> AddBay(Guid id, [FromBody] AddWorkshopBayRequest request)
        {
            var command = new AddWorkshopBayCommand(
                User.GetTenantId(),
                User.GetUserId(),
                Guid.NewGuid().ToString(),
                id,
                request.Name,
                request.Type,
                request.HasLift,
                request.MaxVehicleLength,
                request.MaxVehicleWidth
            );

            var result = await _mediator.Send(command);
            
            if (result.IsFailure)
                return BadRequest(result.Error);
                
            return Ok(result.Value);
        }

        [HttpGet("{id}/bays")]
        public async Task<ActionResult<List<WorkshopBayDto>>> GetBays(Guid id, [FromQuery] BayStatus? status = null)
        {
            var query = new GetWorkshopBaysQuery(User.GetTenantId(), User.GetUserId(), id, status);
            var result = await _mediator.Send(query);
            
            if (result.IsFailure)
                return BadRequest(result.Error);
                
            return Ok(result.Value);
        }
    }
}
```

#### Interventions Controller
```csharp
namespace DriveOps.Garage.Api.Controllers
{
    [ApiController]
    [Route("api/garage/interventions")]
    [Authorize]
    public class InterventionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public InterventionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<ActionResult<Guid>> CreateIntervention([FromBody] CreateInterventionRequest request)
        {
            var command = new CreateInterventionCommand(
                User.GetTenantId(),
                User.GetUserId(),
                Guid.NewGuid().ToString(),
                request.WorkshopId,
                request.VehicleId,
                request.CustomerId,
                request.Type,
                request.Description,
                request.Priority,
                request.CurrentMileage
            );

            var result = await _mediator.Send(command);
            
            if (result.IsFailure)
                return BadRequest(result.Error);
                
            return CreatedAtAction(nameof(GetIntervention), new { id = result.Value }, result.Value);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InterventionDetailDto>> GetIntervention(Guid id)
        {
            var query = new GetInterventionByIdQuery(User.GetTenantId(), User.GetUserId(), id);
            var result = await _mediator.Send(query);
            
            if (result.IsFailure)
                return NotFound(result.Error);
                
            return Ok(result.Value);
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<InterventionSummaryDto>>> GetInterventions(
            [FromQuery] Guid? workshopId = null,
            [FromQuery] InterventionStatus? status = null,
            [FromQuery] Guid? mechanicId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = new GetInterventionsQuery(
                User.GetTenantId(),
                User.GetUserId(),
                workshopId,
                status,
                mechanicId,
                fromDate,
                toDate,
                page,
                pageSize
            );

            var result = await _mediator.Send(query);
            
            if (result.IsFailure)
                return BadRequest(result.Error);
                
            return Ok(result.Value);
        }

        [HttpPut("{id}/assign-mechanic")]
        public async Task<ActionResult> AssignMechanic(Guid id, [FromBody] AssignMechanicRequest request)
        {
            var command = new AssignMechanicToInterventionCommand(
                User.GetTenantId(),
                User.GetUserId(),
                Guid.NewGuid().ToString(),
                id,
                request.MechanicId
            );

            var result = await _mediator.Send(command);
            
            if (result.IsFailure)
                return BadRequest(result.Error);
                
            return NoContent();
        }

        [HttpPut("{id}/start")]
        public async Task<ActionResult> StartWork(Guid id)
        {
            var command = new StartInterventionWorkCommand(
                User.GetTenantId(),
                User.GetUserId(),
                Guid.NewGuid().ToString(),
                id
            );

            var result = await _mediator.Send(command);
            
            if (result.IsFailure)
                return BadRequest(result.Error);
                
            return NoContent();
        }

        [HttpPut("{id}/complete")]
        public async Task<ActionResult> CompleteWork(Guid id, [FromBody] CompleteInterventionRequest request)
        {
            var command = new CompleteInterventionCommand(
                User.GetTenantId(),
                User.GetUserId(),
                Guid.NewGuid().ToString(),
                id,
                request.CompletionNotes
            );

            var result = await _mediator.Send(command);
            
            if (result.IsFailure)
                return BadRequest(result.Error);
                
            return NoContent();
        }

        [HttpPost("{id}/items/labor")]
        public async Task<ActionResult> AddLaborItem(Guid id, [FromBody] AddLaborItemRequest request)
        {
            var command = new AddInterventionLaborCommand(
                User.GetTenantId(),
                User.GetUserId(),
                Guid.NewGuid().ToString(),
                id,
                request.Description,
                request.Hours,
                request.HourlyRate,
                request.MechanicId
            );

            var result = await _mediator.Send(command);
            
            if (result.IsFailure)
                return BadRequest(result.Error);
                
            return NoContent();
        }

        [HttpPost("{id}/items/parts")]
        public async Task<ActionResult> AddPartItem(Guid id, [FromBody] AddPartItemRequest request)
        {
            var command = new AddInterventionPartCommand(
                User.GetTenantId(),
                User.GetUserId(),
                Guid.NewGuid().ToString(),
                id,
                request.PartId,
                request.Quantity
            );

            var result = await _mediator.Send(command);
            
            if (result.IsFailure)
                return BadRequest(result.Error);
                
            return NoContent();
        }
    }
}
```

### 6.2 gRPC Services

#### Garage Service Definition
```protobuf
syntax = "proto3";

package driveops.garage.v1;

import "google/protobuf/timestamp.proto";
import "google/protobuf/empty.proto";

service GarageService {
  // Workshop operations
  rpc GetWorkshop(GetWorkshopRequest) returns (Workshop);
  rpc GetWorkshopBays(GetWorkshopBaysRequest) returns (GetWorkshopBaysResponse);
  
  // Intervention operations
  rpc GetIntervention(GetInterventionRequest) returns (Intervention);
  rpc GetInterventions(GetInterventionsRequest) returns (GetInterventionsResponse);
  rpc GetInterventionsByVehicle(GetInterventionsByVehicleRequest) returns (GetInterventionsResponse);
  
  // Mechanic operations
  rpc GetAvailableMechanics(GetAvailableMechanicsRequest) returns (GetAvailableMechanicsResponse);
  rpc GetMechanicSchedule(GetMechanicScheduleRequest) returns (GetMechanicScheduleResponse);
  
  // Parts operations
  rpc SearchParts(SearchPartsRequest) returns (SearchPartsResponse);
  rpc GetPartAvailability(GetPartAvailabilityRequest) returns (PartAvailability);
  rpc GetLowStockParts(GetLowStockPartsRequest) returns (GetLowStockPartsResponse);
}

message Workshop {
  string id = 1;
  string tenant_id = 2;
  string name = 3;
  string registration_number = 4;
  Address address = 5;
  ContactInfo contact_info = 6;
  WorkshopSettings settings = 7;
  bool is_active = 8;
  google.protobuf.Timestamp created_at = 9;
}

message Address {
  string street = 1;
  string city = 2;
  string postal_code = 3;
  string country = 4;
  string region = 5;
}

message ContactInfo {
  string phone = 1;
  string email = 2;
  string website = 3;
  string contact_person = 4;
}

message WorkshopSettings {
  string opening_time = 1;
  string closing_time = 2;
  repeated int32 working_days = 3;
  double default_hourly_rate = 4;
  string currency = 5;
  int32 default_estimate_validity_days = 6;
  bool auto_assign_mechanics = 7;
  bool require_customer_approval = 8;
}

message Intervention {
  string id = 1;
  string tenant_id = 2;
  string workshop_id = 3;
  string intervention_number = 4;
  string vehicle_id = 5;
  string customer_id = 6;
  InterventionType type = 7;
  InterventionStatus status = 8;
  Priority priority = 9;
  string description = 10;
  string customer_notes = 11;
  int32 current_mileage = 12;
  google.protobuf.Timestamp created_at = 13;
  google.protobuf.Timestamp estimated_completion_date = 14;
  google.protobuf.Timestamp actual_completion_date = 15;
  string created_by = 16;
  string assigned_mechanic_id = 17;
  string assigned_bay_id = 18;
  repeated InterventionItem items = 19;
  Money total_cost = 20;
}

message InterventionItem {
  string id = 1;
  string intervention_id = 2;
  InterventionItemType type = 3;
  string description = 4;
  double quantity = 5;
  Money unit_price = 6;
  Money total_cost = 7;
  string part_id = 8;
  string mechanic_id = 9;
  google.protobuf.Timestamp created_at = 10;
}

message Money {
  double amount = 1;
  string currency = 2;
}

enum InterventionType {
  INTERVENTION_TYPE_UNSPECIFIED = 0;
  INTERVENTION_TYPE_MAINTENANCE = 1;
  INTERVENTION_TYPE_REPAIR = 2;
  INTERVENTION_TYPE_DIAGNOSIS = 3;
  INTERVENTION_TYPE_INSPECTION = 4;
  INTERVENTION_TYPE_WARRANTY = 5;
  INTERVENTION_TYPE_RECALL = 6;
}

enum InterventionStatus {
  INTERVENTION_STATUS_UNSPECIFIED = 0;
  INTERVENTION_STATUS_DRAFT = 1;
  INTERVENTION_STATUS_ESTIMATED = 2;
  INTERVENTION_STATUS_APPROVED = 3;
  INTERVENTION_STATUS_ASSIGNED = 4;
  INTERVENTION_STATUS_IN_PROGRESS = 5;
  INTERVENTION_STATUS_COMPLETED = 6;
  INTERVENTION_STATUS_INVOICED = 7;
  INTERVENTION_STATUS_PAID = 8;
  INTERVENTION_STATUS_CANCELLED = 9;
}

enum Priority {
  PRIORITY_UNSPECIFIED = 0;
  PRIORITY_LOW = 1;
  PRIORITY_MEDIUM = 2;
  PRIORITY_HIGH = 3;
  PRIORITY_URGENT = 4;
}

enum InterventionItemType {
  INTERVENTION_ITEM_TYPE_UNSPECIFIED = 0;
  INTERVENTION_ITEM_TYPE_LABOR = 1;
  INTERVENTION_ITEM_TYPE_PART = 2;
  INTERVENTION_ITEM_TYPE_SUBCONTRACT = 3;
  INTERVENTION_ITEM_TYPE_MISCELLANEOUS = 4;
}

// Request/Response messages
message GetWorkshopRequest {
  string tenant_id = 1;
  string user_id = 2;
  string workshop_id = 3;
}

message GetInterventionRequest {
  string tenant_id = 1;
  string user_id = 2;
  string intervention_id = 3;
}

message GetInterventionsRequest {
  string tenant_id = 1;
  string user_id = 2;
  string workshop_id = 3;
  InterventionStatus status = 4;
  string mechanic_id = 5;
  google.protobuf.Timestamp from_date = 6;
  google.protobuf.Timestamp to_date = 7;
  int32 page = 8;
  int32 page_size = 9;
}

message GetInterventionsResponse {
  repeated Intervention interventions = 1;
  int32 total_count = 2;
  int32 page = 3;
  int32 page_size = 4;
}
```
