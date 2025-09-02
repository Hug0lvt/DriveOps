# DriveOps - Commercial RH/PERSONNEL Module Documentation

This document provides comprehensive technical architecture for the RH/PERSONNEL commercial module - an advanced human resources and personnel management system that extends the Core Users module. This module provides complete HR functionality for automotive businesses including payroll automation, compliance management, training certifications, and multi-site personnel coordination.

## Overview

The RH/PERSONNEL module is a premium commercial offering that transforms the basic Core Users functionality into a comprehensive human resources management system. It follows Domain Driven Design (DDD) principles with clean architecture layers and integrates seamlessly with all DriveOps business modules.

### Key Value Propositions

- **Comprehensive HR Suite**: Complete employee lifecycle management from onboarding to offboarding
- **Automated Payroll**: Multi-country payroll processing with tax compliance and social charges
- **Compliance Management**: GDPR compliance, labor law adherence, and certification tracking
- **Multi-Site Coordination**: Centralized HR for distributed automotive businesses
- **Scalable Pricing**: Per-employee pricing model with tiered functionality

---

## 1. Business Domain Model

The RH/PERSONNEL module extends the Core Users module with comprehensive HR entities and value objects.

### 1.1 Core Domain Entities

#### Employee (extends User)
```csharp
namespace DriveOps.HumanResources.Domain.Entities
{
    public class Employee : AggregateRoot
    {
        public EmployeeId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public UserId UserId { get; private set; } // Reference to Core Users
        public string EmployeeNumber { get; private set; }
        public EmploymentContract Contract { get; private set; }
        public PersonalInfo PersonalInfo { get; private set; }
        public EmergencyContact EmergencyContact { get; private set; }
        public EmploymentStatus Status { get; private set; }
        public DateTime HireDate { get; private set; }
        public DateTime? TerminationDate { get; private set; }
        public DepartmentId DepartmentId { get; private set; }
        public PositionId PositionId { get; private set; }
        public ManagerId? DirectManagerId { get; private set; }
        
        private readonly List<EmployeeDocument> _documents = new();
        public IReadOnlyCollection<EmployeeDocument> Documents => _documents.AsReadOnly();
        
        private readonly List<PayrollEntry> _payrollHistory = new();
        public IReadOnlyCollection<PayrollEntry> PayrollHistory => _payrollHistory.AsReadOnly();
        
        private readonly List<TimeEntry> _timeEntries = new();
        public IReadOnlyCollection<TimeEntry> TimeEntries => _timeEntries.AsReadOnly();
        
        private readonly List<PerformanceReview> _performanceReviews = new();
        public IReadOnlyCollection<PerformanceReview> PerformanceReviews => _performanceReviews.AsReadOnly();

        public void UpdateContract(EmploymentContract newContract, UserId updatedBy)
        {
            Contract = newContract;
            AddDomainEvent(new EmployeeContractUpdatedEvent(TenantId, Id, Contract, updatedBy));
        }

        public void AssignToPosition(PositionId positionId, DepartmentId departmentId, UserId assignedBy)
        {
            var previousPosition = PositionId;
            var previousDepartment = DepartmentId;
            
            PositionId = positionId;
            DepartmentId = departmentId;
            
            AddDomainEvent(new EmployeePositionChangedEvent(
                TenantId, Id, previousPosition, positionId, previousDepartment, departmentId, assignedBy));
        }

        public void TerminateEmployment(DateTime terminationDate, TerminationReason reason, UserId terminatedBy)
        {
            if (Status == EmploymentStatus.Terminated)
                throw new InvalidOperationException("Employee is already terminated");

            TerminationDate = terminationDate;
            Status = EmploymentStatus.Terminated;
            
            AddDomainEvent(new EmployeeTerminatedEvent(TenantId, Id, terminationDate, reason, terminatedBy));
        }
    }
}
```

#### Department
```csharp
public class Department : AggregateRoot
{
    public DepartmentId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public DepartmentId? ParentDepartmentId { get; private set; }
    public EmployeeId? DepartmentHeadId { get; private set; }
    public CostCenter CostCenter { get; private set; }
    public bool IsActive { get; private set; }
    
    private readonly List<Position> _positions = new();
    public IReadOnlyCollection<Position> Positions => _positions.AsReadOnly();
    
    private readonly List<Employee> _employees = new();
    public IReadOnlyCollection<Employee> Employees => _employees.AsReadOnly();

    public void AddPosition(Position position, UserId createdBy)
    {
        _positions.Add(position);
        AddDomainEvent(new DepartmentPositionAddedEvent(TenantId, Id, position.Id, createdBy));
    }

    public void AssignDepartmentHead(EmployeeId employeeId, UserId assignedBy)
    {
        DepartmentHeadId = employeeId;
        AddDomainEvent(new DepartmentHeadAssignedEvent(TenantId, Id, employeeId, assignedBy));
    }
}
```

#### Position
```csharp
public class Position : AggregateRoot
{
    public PositionId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public DepartmentId DepartmentId { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public JobLevel Level { get; private set; }
    public SalaryRange SalaryRange { get; private set; }
    public bool RequiresLicense { get; private set; }
    public bool IsActive { get; private set; }
    
    private readonly List<Skill> _requiredSkills = new();
    public IReadOnlyCollection<Skill> RequiredSkills => _requiredSkills.AsReadOnly();
    
    private readonly List<Certification> _requiredCertifications = new();
    public IReadOnlyCollection<Certification> RequiredCertifications => _requiredCertifications.AsReadOnly();

    public void UpdateSalaryRange(SalaryRange newRange, UserId updatedBy)
    {
        var previousRange = SalaryRange;
        SalaryRange = newRange;
        AddDomainEvent(new PositionSalaryRangeUpdatedEvent(TenantId, Id, previousRange, newRange, updatedBy));
    }
}
```

### 1.2 Payroll Management Entities

#### PayrollPeriod
```csharp
public class PayrollPeriod : AggregateRoot
{
    public PayrollPeriodId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public PayrollFrequency Frequency { get; private set; }
    public PayrollStatus Status { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public UserId? ProcessedBy { get; private set; }
    
    private readonly List<PayrollEntry> _entries = new();
    public IReadOnlyCollection<PayrollEntry> Entries => _entries.AsReadOnly();

    public void ProcessPayroll(UserId processedBy)
    {
        if (Status != PayrollStatus.Draft)
            throw new InvalidOperationException("Only draft payrolls can be processed");

        Status = PayrollStatus.Processing;
        ProcessedAt = DateTime.UtcNow;
        ProcessedBy = processedBy;
        
        AddDomainEvent(new PayrollProcessingStartedEvent(TenantId, Id, processedBy));
    }

    public void CompletePayroll(UserId completedBy)
    {
        if (Status != PayrollStatus.Processing)
            throw new InvalidOperationException("Only processing payrolls can be completed");

        Status = PayrollStatus.Completed;
        AddDomainEvent(new PayrollCompletedEvent(TenantId, Id, completedBy));
    }
}
```

#### PayrollEntry
```csharp
public class PayrollEntry : Entity
{
    public PayrollEntryId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public PayrollPeriodId PayrollPeriodId { get; private set; }
    public EmployeeId EmployeeId { get; private set; }
    public decimal GrossSalary { get; private set; }
    public decimal OvertimePay { get; private set; }
    public decimal BonusPay { get; private set; }
    public decimal GrossTotalPay { get; private set; }
    public TaxCalculation TaxCalculation { get; private set; }
    public SocialCharges SocialCharges { get; private set; }
    public decimal NetPay { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public DateTime? PaidAt { get; private set; }
    
    private readonly List<Deduction> _deductions = new();
    public IReadOnlyCollection<Deduction> Deductions => _deductions.AsReadOnly();
    
    private readonly List<Bonus> _bonuses = new();
    public IReadOnlyCollection<Bonus> Bonuses => _bonuses.AsReadOnly();

    public void CalculateNetPay(ITaxCalculationService taxService, ISocialChargesService socialService)
    {
        GrossTotalPay = GrossSalary + OvertimePay + BonusPay + _bonuses.Sum(b => b.Amount);
        TaxCalculation = taxService.CalculateTaxes(GrossTotalPay, EmployeeId);
        SocialCharges = socialService.CalculateSocialCharges(GrossTotalPay, EmployeeId);
        
        var totalDeductions = _deductions.Sum(d => d.Amount) + TaxCalculation.TotalTax + SocialCharges.TotalCharges;
        NetPay = GrossTotalPay - totalDeductions;
    }

    public void MarkAsPaid(DateTime paidAt, UserId paidBy)
    {
        PaymentStatus = PaymentStatus.Paid;
        PaidAt = paidAt;
        AddDomainEvent(new PayrollEntryPaidEvent(TenantId, Id, EmployeeId, NetPay, paidAt, paidBy));
    }
}
```

### 1.3 Performance Management Entities

#### PerformanceReview
```csharp
public class PerformanceReview : AggregateRoot
{
    public PerformanceReviewId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public EmployeeId EmployeeId { get; private set; }
    public UserId ReviewerId { get; private set; }
    public ReviewPeriod Period { get; private set; }
    public ReviewType Type { get; private set; }
    public ReviewStatus Status { get; private set; }
    public decimal OverallRating { get; private set; }
    public string? ReviewerComments { get; private set; }
    public string? EmployeeComments { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    
    private readonly List<ReviewCriteria> _criteria = new();
    public IReadOnlyCollection<ReviewCriteria> Criteria => _criteria.AsReadOnly();
    
    private readonly List<Goal> _goals = new();
    public IReadOnlyCollection<Goal> Goals => _goals.AsReadOnly();

    public void AddCriteria(string criteriaName, decimal rating, string? comments)
    {
        var criteria = new ReviewCriteria(criteriaName, rating, comments);
        _criteria.Add(criteria);
        CalculateOverallRating();
    }

    public void CompleteReview(string? reviewerComments, UserId completedBy)
    {
        if (Status != ReviewStatus.InProgress)
            throw new InvalidOperationException("Only in-progress reviews can be completed");

        Status = ReviewStatus.Completed;
        ReviewerComments = reviewerComments;
        CompletedAt = DateTime.UtcNow;
        
        AddDomainEvent(new PerformanceReviewCompletedEvent(TenantId, Id, EmployeeId, OverallRating, completedBy));
    }

    private void CalculateOverallRating()
    {
        if (_criteria.Any())
            OverallRating = _criteria.Average(c => c.Rating);
    }
}
```

#### TrainingRecord
```csharp
public class TrainingRecord : AggregateRoot
{
    public TrainingRecordId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public EmployeeId EmployeeId { get; private set; }
    public TrainingProgramId TrainingProgramId { get; private set; }
    public string TrainingName { get; private set; }
    public TrainingType Type { get; private set; }
    public TrainingStatus Status { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime? CompletionDate { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public decimal? Score { get; private set; }
    public bool IsMandatory { get; private set; }
    public bool IsCertification { get; private set; }
    public CertificationId? CertificationId { get; private set; }
    public UserId? ApprovedBy { get; private set; }

    public void CompleteTraining(decimal? score, UserId completedBy)
    {
        if (Status != TrainingStatus.InProgress)
            throw new InvalidOperationException("Only in-progress training can be completed");

        Status = TrainingStatus.Completed;
        CompletionDate = DateTime.UtcNow;
        Score = score;
        
        if (IsCertification && score >= 70) // Passing score for certification
        {
            Status = TrainingStatus.Certified;
            ExpiryDate = DateTime.UtcNow.AddYears(2); // 2-year certification validity
        }
        
        AddDomainEvent(new TrainingCompletedEvent(TenantId, Id, EmployeeId, TrainingProgramId, score, completedBy));
    }

    public void RenewCertification(UserId renewedBy)
    {
        if (!IsCertification || Status != TrainingStatus.Certified)
            throw new InvalidOperationException("Only valid certifications can be renewed");

        ExpiryDate = DateTime.UtcNow.AddYears(2);
        AddDomainEvent(new CertificationRenewedEvent(TenantId, EmployeeId, CertificationId.Value, ExpiryDate.Value, renewedBy));
    }
}
```


---

## 2. Complete C# Architecture

The RH/PERSONNEL module follows Domain Driven Design (DDD) principles with clean architecture layers, extending the Core Users module architecture.

### 2.1 Architecture Overview

```
DriveOps.HumanResources/
├── Domain/
│   ├── Entities/
│   │   ├── Employee.cs
│   │   ├── Department.cs
│   │   ├── Position.cs
│   │   ├── PayrollPeriod.cs
│   │   ├── PayrollEntry.cs
│   │   ├── TimeEntry.cs
│   │   ├── LeaveRequest.cs
│   │   ├── PerformanceReview.cs
│   │   └── TrainingRecord.cs
│   ├── ValueObjects/
│   │   ├── EmploymentContract.cs
│   │   ├── PersonalInfo.cs
│   │   ├── TaxCalculation.cs
│   │   ├── SocialCharges.cs
│   │   └── SalaryRange.cs
│   ├── Aggregates/
│   │   ├── EmployeeAggregate.cs
│   │   ├── PayrollAggregate.cs
│   │   └── DepartmentAggregate.cs
│   ├── DomainEvents/
│   │   ├── EmployeeHiredEvent.cs
│   │   ├── PayrollProcessedEvent.cs
│   │   ├── LeaveApprovedEvent.cs
│   │   └── TrainingCompletedEvent.cs
│   ├── Repositories/
│   │   ├── IEmployeeRepository.cs
│   │   ├── IPayrollRepository.cs
│   │   └── ITimeTrackingRepository.cs
│   └── Services/
│       ├── ITaxCalculationService.cs
│       ├── ISocialChargesService.cs
│       └── IComplianceService.cs
├── Application/
│   ├── Commands/
│   │   ├── Employees/
│   │   ├── Payroll/
│   │   ├── TimeTracking/
│   │   └── Training/
│   ├── Queries/
│   │   ├── Employees/
│   │   ├── Payroll/
│   │   ├── Reports/
│   │   └── Analytics/
│   ├── Handlers/
│   ├── DTOs/
│   └── Services/
│       ├── PayrollService.cs
│       ├── ComplianceService.cs
│       └── AnalyticsService.cs
├── Infrastructure/
│   ├── Persistence/
│   │   ├── HRDbContext.cs
│   │   ├── Configurations/
│   │   └── Repositories/
│   ├── External/
│   │   ├── PayrollProviders/
│   │   ├── TaxServices/
│   │   └── BankingServices/
│   ├── Messaging/
│   │   ├── EventHandlers/
│   │   └── IntegrationEvents/
│   └── Services/
│       ├── TaxCalculationService.cs
│       ├── PayrollProcessor.cs
│       └── ComplianceMonitor.cs
└── Presentation/
    ├── Controllers/
    │   ├── EmployeesController.cs
    │   ├── PayrollController.cs
    │   ├── TimeTrackingController.cs
    │   └── ReportsController.cs
    ├── Pages/
    │   ├── Employees/
    │   ├── Payroll/
    │   ├── TimeTracking/
    │   └── Reports/
    └── Components/
        ├── EmployeeProfile/
        ├── PayrollDashboard/
        └── ComplianceDashboard/
```

### 2.2 Application Layer - CQRS Commands and Queries

#### Employee Management Commands
```csharp
namespace DriveOps.HumanResources.Application.Commands.Employees
{
    public record CreateEmployeeCommand(
        string FirstName,
        string LastName,
        string Email,
        string Phone,
        DepartmentId DepartmentId,
        PositionId PositionId,
        EmploymentContract Contract,
        PersonalInfo PersonalInfo,
        DateTime HireDate
    ) : IRequest<Result<EmployeeDto>>;

    public class CreateEmployeeCommandHandler : IRequestHandler<CreateEmployeeCommand, Result<EmployeeDto>>
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IUserService _userService; // From Core Users module
        private readonly ITenantContext _tenantContext;
        private readonly IMapper _mapper;

        public async Task<Result<EmployeeDto>> Handle(CreateEmployeeCommand request, CancellationToken cancellationToken)
        {
            // First create user in Core Users module
            var userResult = await _userService.CreateUserAsync(new CreateUserRequest(
                request.Email, request.FirstName, request.LastName, request.Phone));
                
            if (userResult.IsFailure)
                return Result.Failure<EmployeeDto>(userResult.Error);

            // Create employee extending the user
            var employee = new Employee(
                TenantId.From(Guid.Parse(_tenantContext.TenantId)),
                userResult.Value.Id,
                request.Contract,
                request.PersonalInfo,
                request.HireDate,
                request.DepartmentId,
                request.PositionId
            );

            await _employeeRepository.AddAsync(employee);
            await _employeeRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

            return Result.Success(_mapper.Map<EmployeeDto>(employee));
        }
    }
}
```

#### Payroll Processing Commands
```csharp
public record ProcessPayrollCommand(
    PayrollPeriodId PayrollPeriodId
) : IRequest<Result<PayrollProcessingResultDto>>;

public class ProcessPayrollCommandHandler : IRequestHandler<ProcessPayrollCommand, Result<PayrollProcessingResultDto>>
{
    private readonly IPayrollRepository _payrollRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly ITaxCalculationService _taxService;
    private readonly ISocialChargesService _socialChargesService;
    private readonly IPayrollProcessor _payrollProcessor;
    private readonly ILogger<ProcessPayrollCommandHandler> _logger;

    public async Task<Result<PayrollProcessingResultDto>> Handle(ProcessPayrollCommand request, CancellationToken cancellationToken)
    {
        var payrollPeriod = await _payrollRepository.GetByIdAsync(request.PayrollPeriodId);
        if (payrollPeriod == null)
            return Result.Failure<PayrollProcessingResultDto>("Payroll period not found");

        var employees = await _employeeRepository.GetActiveEmployeesAsync();
        var entries = new List<PayrollEntry>();

        foreach (var employee in employees)
        {
            var timeEntries = await GetApprovedTimeEntriesAsync(employee.Id, payrollPeriod.StartDate, payrollPeriod.EndDate);
            var payrollEntry = await CreatePayrollEntryAsync(employee, timeEntries, payrollPeriod.Id);
            entries.Add(payrollEntry);
        }

        payrollPeriod.ProcessPayroll(UserId.From(GetCurrentUserId()));
        
        // Process payments through external payroll provider
        var processingResult = await _payrollProcessor.ProcessPaymentsAsync(entries);
        
        await _payrollRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success(new PayrollProcessingResultDto(
            payrollPeriod.Id,
            entries.Count,
            entries.Sum(e => e.NetPay),
            processingResult.SuccessfulPayments,
            processingResult.FailedPayments
        ));
    }

    private async Task<PayrollEntry> CreatePayrollEntryAsync(Employee employee, IEnumerable<TimeEntry> timeEntries, PayrollPeriodId payrollPeriodId)
    {
        var regularHours = timeEntries.Sum(t => t.WorkedHours.TotalHours);
        var overtimeHours = timeEntries.Sum(t => t.OvertimeHours.TotalHours);
        
        var grossSalary = CalculateGrossSalary(employee.Contract.BaseSalary, regularHours);
        var overtimePay = CalculateOvertimePay(employee.Contract.HourlyRate, overtimeHours);

        var payrollEntry = new PayrollEntry(
            employee.TenantId,
            payrollPeriodId,
            employee.Id,
            grossSalary,
            overtimePay
        );

        payrollEntry.CalculateNetPay(_taxService, _socialChargesService);
        return payrollEntry;
    }
}
```

### 2.3 Infrastructure Layer - External Integrations

#### Tax Calculation Service
```csharp
namespace DriveOps.HumanResources.Infrastructure.Services
{
    public class TaxCalculationService : ITaxCalculationService
    {
        private readonly ICountryTaxProviderFactory _taxProviderFactory;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TaxCalculationService> _logger;

        public async Task<TaxCalculation> CalculateTaxes(decimal grossPay, EmployeeId employeeId)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            var taxProvider = _taxProviderFactory.GetProvider(employee.PersonalInfo.Country);
            
            var taxRequest = new TaxCalculationRequest
            {
                GrossPay = grossPay,
                TaxYear = DateTime.UtcNow.Year,
                EmployeeInfo = new TaxEmployeeInfo
                {
                    MaritalStatus = employee.PersonalInfo.MaritalStatus,
                    DependentsCount = employee.PersonalInfo.DependentsCount,
                    TaxResidency = employee.PersonalInfo.TaxResidency,
                    SocialSecurityNumber = employee.PersonalInfo.SocialSecurityNumber
                }
            };

            var taxResult = await taxProvider.CalculateTaxesAsync(taxRequest);
            
            return new TaxCalculation(
                taxResult.IncomeTax,
                taxResult.SocialSecurityTax,
                taxResult.UnemploymentTax,
                taxResult.OtherTaxes,
                employee.PersonalInfo.Country
            );
        }
    }

    // Multi-country tax provider support
    public interface ICountryTaxProviderFactory
    {
        ITaxProvider GetProvider(string countryCode);
    }

    public class CountryTaxProviderFactory : ICountryTaxProviderFactory
    {
        private readonly Dictionary<string, ITaxProvider> _providers;

        public CountryTaxProviderFactory(
            FranceTaxProvider franceTaxProvider,
            GermanyTaxProvider germanyTaxProvider,
            UKTaxProvider ukTaxProvider)
        {
            _providers = new Dictionary<string, ITaxProvider>
            {
                ["FR"] = franceTaxProvider,
                ["DE"] = germanyTaxProvider,
                ["GB"] = ukTaxProvider
            };
        }

        public ITaxProvider GetProvider(string countryCode)
        {
            return _providers.TryGetValue(countryCode, out var provider) 
                ? provider 
                : throw new NotSupportedException($"Tax calculations not supported for country: {countryCode}");
        }
    }
}
```

#### Payroll Processing Service
```csharp
public class PayrollProcessor : IPayrollProcessor
{
    private readonly IPayrollProviderFactory _payrollProviderFactory;
    private readonly IBankingService _bankingService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PayrollProcessor> _logger;

    public async Task<PayrollProcessingResult> ProcessPaymentsAsync(IEnumerable<PayrollEntry> entries)
    {
        var successfulPayments = 0;
        var failedPayments = 0;
        var errors = new List<string>();

        foreach (var entry in entries)
        {
            try
            {
                var employee = await GetEmployeeAsync(entry.EmployeeId);
                var payrollProvider = _payrollProviderFactory.GetProvider(employee.PersonalInfo.Country);
                
                var paymentRequest = new PaymentRequest
                {
                    EmployeeId = entry.EmployeeId.Value,
                    Amount = entry.NetPay,
                    Currency = employee.Contract.Currency,
                    BankAccount = employee.PersonalInfo.BankAccount,
                    PaymentDate = DateTime.UtcNow,
                    Description = $"Salary payment for {entry.PayrollPeriodId}"
                };

                var paymentResult = await payrollProvider.ProcessPaymentAsync(paymentRequest);
                
                if (paymentResult.IsSuccess)
                {
                    entry.MarkAsPaid(DateTime.UtcNow, UserId.System());
                    await SendPayslipNotificationAsync(employee, entry);
                    successfulPayments++;
                }
                else
                {
                    errors.Add($"Payment failed for employee {employee.EmployeeNumber}: {paymentResult.Error}");
                    failedPayments++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment for entry {EntryId}", entry.Id);
                errors.Add($"Payment processing error for entry {entry.Id}: {ex.Message}");
                failedPayments++;
            }
        }

        return new PayrollProcessingResult(successfulPayments, failedPayments, errors);
    }

    private async Task SendPayslipNotificationAsync(Employee employee, PayrollEntry entry)
    {
        var user = await GetUserAsync(employee.UserId);
        var payslipPdf = await GeneratePayslipPdfAsync(employee, entry);
        
        await _notificationService.SendEmailAsync(new EmailNotification
        {
            To = user.Email,
            Subject = "Your payslip is ready",
            Body = "Please find your payslip attached.",
            Attachments = new[] { payslipPdf }
        });
    }
}
```


---

## 3. Database Schema (PostgreSQL)

The RH/PERSONNEL module extends the Core Users schema with comprehensive HR tables, maintaining full tenant isolation and referential integrity.

### 3.1 Employee Management Schema

```sql
-- HR module schema
CREATE SCHEMA IF NOT EXISTS hr;

-- Departments table
CREATE TABLE hr.departments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL, -- Foreign key to admin.tenants(id)
    name VARCHAR(255) NOT NULL,
    description TEXT,
    parent_department_id UUID REFERENCES hr.departments(id),
    department_head_id UUID, -- Will reference hr.employees(id)
    cost_center VARCHAR(50),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, name)
);

-- Positions table
CREATE TABLE hr.positions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    department_id UUID NOT NULL REFERENCES hr.departments(id) ON DELETE CASCADE,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    job_level INTEGER NOT NULL, -- 1=Entry, 2=Junior, 3=Mid, 4=Senior, 5=Lead, 6=Manager, 7=Director
    min_salary DECIMAL(12,2),
    max_salary DECIMAL(12,2),
    currency CHAR(3) NOT NULL DEFAULT 'EUR',
    requires_license BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, department_id, title)
);

-- Employees table (extends Core Users)
CREATE TABLE hr.employees (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    user_id UUID NOT NULL, -- Foreign key to users.users(id)
    employee_number VARCHAR(50) NOT NULL,
    department_id UUID NOT NULL REFERENCES hr.departments(id),
    position_id UUID NOT NULL REFERENCES hr.positions(id),
    direct_manager_id UUID REFERENCES hr.employees(id),
    hire_date DATE NOT NULL,
    termination_date DATE,
    employment_status INTEGER NOT NULL DEFAULT 1, -- 1=Active, 2=OnLeave, 3=Suspended, 4=Terminated
    employment_type INTEGER NOT NULL, -- 1=FullTime, 2=PartTime, 3=Contract, 4=Intern, 5=Freelancer
    
    -- Employment contract details
    base_salary DECIMAL(12,2) NOT NULL,
    hourly_rate DECIMAL(8,2),
    overtime_rate DECIMAL(8,2),
    currency CHAR(3) NOT NULL DEFAULT 'EUR',
    pay_frequency INTEGER NOT NULL, -- 1=Weekly, 2=BiWeekly, 3=Monthly, 4=Quarterly
    
    -- Personal information (GDPR compliant)
    date_of_birth DATE,
    nationality VARCHAR(100),
    marital_status INTEGER, -- 1=Single, 2=Married, 3=Divorced, 4=Widowed
    dependents_count INTEGER DEFAULT 0,
    social_security_number VARCHAR(50), -- Encrypted
    tax_identification VARCHAR(50), -- Encrypted
    
    -- Emergency contact
    emergency_contact_name VARCHAR(255),
    emergency_contact_phone VARCHAR(50),
    emergency_contact_relationship VARCHAR(100),
    
    -- Address information
    street_address VARCHAR(255),
    city VARCHAR(100),
    postal_code VARCHAR(20),
    country CHAR(2) NOT NULL,
    
    -- Banking information (encrypted)
    bank_name VARCHAR(255),
    bank_account_number VARCHAR(100), -- Encrypted
    bank_routing_number VARCHAR(50), -- Encrypted
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, employee_number),
    UNIQUE(tenant_id, user_id)
);

-- Employee documents table
CREATE TABLE hr.employee_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    employee_id UUID NOT NULL REFERENCES hr.employees(id) ON DELETE CASCADE,
    file_id UUID NOT NULL, -- Reference to files.files(id)
    document_type INTEGER NOT NULL, -- 1=Contract, 2=ID, 3=Resume, 4=Certificate, 5=Medical, 6=Other
    document_name VARCHAR(255) NOT NULL,
    expiry_date DATE,
    is_mandatory BOOLEAN NOT NULL DEFAULT FALSE,
    uploaded_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    uploaded_by UUID NOT NULL -- Reference to users.users(id)
);

-- Skills table
CREATE TABLE hr.skills (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    category VARCHAR(100), -- Technical, Soft, Language, Certification
    description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    UNIQUE(tenant_id, name)
);

-- Employee skills mapping
CREATE TABLE hr.employee_skills (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    employee_id UUID NOT NULL REFERENCES hr.employees(id) ON DELETE CASCADE,
    skill_id UUID NOT NULL REFERENCES hr.skills(id) ON DELETE CASCADE,
    proficiency_level INTEGER NOT NULL, -- 1=Beginner, 2=Intermediate, 3=Advanced, 4=Expert
    certified BOOLEAN NOT NULL DEFAULT FALSE,
    certification_date DATE,
    certification_expiry DATE,
    notes TEXT,
    
    UNIQUE(tenant_id, employee_id, skill_id)
);
```

### 3.2 Payroll Management Schema

```sql
-- Payroll periods table
CREATE TABLE hr.payroll_periods (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL, -- "January 2024 Payroll"
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    pay_date DATE NOT NULL,
    frequency INTEGER NOT NULL, -- 1=Weekly, 2=BiWeekly, 3=Monthly, 4=Quarterly
    status INTEGER NOT NULL DEFAULT 0, -- 0=Draft, 1=Processing, 2=Completed, 3=Cancelled
    total_gross_amount DECIMAL(15,2) DEFAULT 0,
    total_net_amount DECIMAL(15,2) DEFAULT 0,
    total_tax_amount DECIMAL(15,2) DEFAULT 0,
    processed_at TIMESTAMP WITH TIME ZONE,
    processed_by UUID, -- Reference to users.users(id)
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, name)
);

-- Payroll entries table
CREATE TABLE hr.payroll_entries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    payroll_period_id UUID NOT NULL REFERENCES hr.payroll_periods(id) ON DELETE CASCADE,
    employee_id UUID NOT NULL REFERENCES hr.employees(id) ON DELETE CASCADE,
    
    -- Salary components
    base_salary DECIMAL(12,2) NOT NULL DEFAULT 0,
    overtime_pay DECIMAL(12,2) NOT NULL DEFAULT 0,
    bonus_pay DECIMAL(12,2) NOT NULL DEFAULT 0,
    commission_pay DECIMAL(12,2) NOT NULL DEFAULT 0,
    allowances DECIMAL(12,2) NOT NULL DEFAULT 0,
    gross_pay DECIMAL(12,2) NOT NULL DEFAULT 0,
    
    -- Tax calculations
    income_tax DECIMAL(12,2) NOT NULL DEFAULT 0,
    social_security_tax DECIMAL(12,2) NOT NULL DEFAULT 0,
    unemployment_tax DECIMAL(12,2) NOT NULL DEFAULT 0,
    other_taxes DECIMAL(12,2) NOT NULL DEFAULT 0,
    total_taxes DECIMAL(12,2) NOT NULL DEFAULT 0,
    
    -- Deductions
    health_insurance DECIMAL(12,2) NOT NULL DEFAULT 0,
    dental_insurance DECIMAL(12,2) NOT NULL DEFAULT 0,
    retirement_contribution DECIMAL(12,2) NOT NULL DEFAULT 0,
    other_deductions DECIMAL(12,2) NOT NULL DEFAULT 0,
    total_deductions DECIMAL(12,2) NOT NULL DEFAULT 0,
    
    -- Final calculation
    net_pay DECIMAL(12,2) NOT NULL DEFAULT 0,
    
    -- Payment tracking
    payment_status INTEGER NOT NULL DEFAULT 0, -- 0=Pending, 1=Paid, 2=Failed, 3=Cancelled
    payment_method INTEGER, -- 1=DirectDeposit, 2=Check, 3=Cash, 4=Card
    payment_reference VARCHAR(255),
    paid_at TIMESTAMP WITH TIME ZONE,
    
    -- Metadata
    worked_hours DECIMAL(8,2) DEFAULT 0,
    overtime_hours DECIMAL(8,2) DEFAULT 0,
    tax_jurisdiction VARCHAR(10) NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'EUR',
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, payroll_period_id, employee_id)
);

-- Payroll adjustments table
CREATE TABLE hr.payroll_adjustments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    payroll_entry_id UUID NOT NULL REFERENCES hr.payroll_entries(id) ON DELETE CASCADE,
    adjustment_type INTEGER NOT NULL, -- 1=Bonus, 2=Deduction, 3=Correction, 4=Reimbursement
    description VARCHAR(255) NOT NULL,
    amount DECIMAL(12,2) NOT NULL,
    is_taxable BOOLEAN NOT NULL DEFAULT TRUE,
    is_recurring BOOLEAN NOT NULL DEFAULT FALSE,
    applied_by UUID NOT NULL, -- Reference to users.users(id)
    applied_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
```

### 3.3 Time & Attendance Schema

```sql
-- Time entries table
CREATE TABLE hr.time_entries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    employee_id UUID NOT NULL REFERENCES hr.employees(id) ON DELETE CASCADE,
    entry_date DATE NOT NULL,
    clock_in_time TIMESTAMP WITH TIME ZONE NOT NULL,
    clock_out_time TIMESTAMP WITH TIME ZONE,
    break_duration_minutes INTEGER DEFAULT 0,
    worked_hours DECIMAL(8,2) DEFAULT 0,
    overtime_hours DECIMAL(8,2) DEFAULT 0,
    
    -- Location tracking
    clock_in_location_lat DECIMAL(10,8),
    clock_in_location_lng DECIMAL(11,8),
    clock_out_location_lat DECIMAL(10,8),
    clock_out_location_lng DECIMAL(11,8),
    work_location VARCHAR(255),
    
    -- Approval workflow
    status INTEGER NOT NULL DEFAULT 0, -- 0=Draft, 1=PendingApproval, 2=Approved, 3=Rejected
    notes TEXT,
    approved_by UUID, -- Reference to users.users(id)
    approved_at TIMESTAMP WITH TIME ZONE,
    rejection_reason TEXT,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, employee_id, entry_date)
);

-- Leave types table
CREATE TABLE hr.leave_types (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL, -- Annual Leave, Sick Leave, Maternity, etc.
    description TEXT,
    max_days_per_year INTEGER,
    is_paid BOOLEAN NOT NULL DEFAULT TRUE,
    requires_approval BOOLEAN NOT NULL DEFAULT TRUE,
    requires_documentation BOOLEAN NOT NULL DEFAULT FALSE,
    accrual_rate DECIMAL(5,2), -- Days accrued per month
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    UNIQUE(tenant_id, name)
);

-- Leave balances table
CREATE TABLE hr.leave_balances (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    employee_id UUID NOT NULL REFERENCES hr.employees(id) ON DELETE CASCADE,
    leave_type_id UUID NOT NULL REFERENCES hr.leave_types(id) ON DELETE CASCADE,
    leave_year INTEGER NOT NULL,
    accrued_days DECIMAL(8,2) NOT NULL DEFAULT 0,
    used_days DECIMAL(8,2) NOT NULL DEFAULT 0,
    pending_days DECIMAL(8,2) NOT NULL DEFAULT 0,
    available_days DECIMAL(8,2) NOT NULL DEFAULT 0,
    carried_forward_days DECIMAL(8,2) NOT NULL DEFAULT 0,
    
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, employee_id, leave_type_id, leave_year)
);

-- Leave requests table
CREATE TABLE hr.leave_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    employee_id UUID NOT NULL REFERENCES hr.employees(id) ON DELETE CASCADE,
    leave_type_id UUID NOT NULL REFERENCES hr.leave_types(id),
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    days_requested DECIMAL(8,2) NOT NULL,
    reason TEXT,
    
    -- Approval workflow
    status INTEGER NOT NULL DEFAULT 0, -- 0=Pending, 1=Approved, 2=Rejected, 3=Cancelled
    approved_by UUID, -- Reference to users.users(id)
    approved_at TIMESTAMP WITH TIME ZONE,
    approval_notes TEXT,
    
    -- Documentation
    supporting_document_id UUID, -- Reference to files.files(id)
    
    requested_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
```

### 3.4 Performance & Training Schema

```sql
-- Performance review templates
CREATE TABLE hr.review_templates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    review_type INTEGER NOT NULL, -- 1=Annual, 2=Quarterly, 3=Probation, 4=360Degree, 5=SelfAssessment
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Review criteria (template items)
CREATE TABLE hr.review_criteria (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    template_id UUID NOT NULL REFERENCES hr.review_templates(id) ON DELETE CASCADE,
    criteria_name VARCHAR(255) NOT NULL,
    description TEXT,
    weight_percentage DECIMAL(5,2) DEFAULT 100, -- For weighted scoring
    max_score DECIMAL(5,2) DEFAULT 5,
    display_order INTEGER NOT NULL DEFAULT 1
);

-- Performance reviews
CREATE TABLE hr.performance_reviews (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    employee_id UUID NOT NULL REFERENCES hr.employees(id) ON DELETE CASCADE,
    reviewer_id UUID NOT NULL REFERENCES hr.employees(id),
    template_id UUID NOT NULL REFERENCES hr.review_templates(id),
    review_period_start DATE NOT NULL,
    review_period_end DATE NOT NULL,
    due_date DATE,
    
    -- Status and scores
    status INTEGER NOT NULL DEFAULT 0, -- 0=Draft, 1=InProgress, 2=Completed, 3=Acknowledged
    overall_rating DECIMAL(5,2),
    reviewer_comments TEXT,
    employee_comments TEXT,
    
    -- Timestamps
    started_at TIMESTAMP WITH TIME ZONE,
    completed_at TIMESTAMP WITH TIME ZONE,
    acknowledged_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Review scores (individual criteria ratings)
CREATE TABLE hr.review_scores (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    review_id UUID NOT NULL REFERENCES hr.performance_reviews(id) ON DELETE CASCADE,
    criteria_id UUID NOT NULL REFERENCES hr.review_criteria(id),
    score DECIMAL(5,2) NOT NULL,
    comments TEXT,
    
    UNIQUE(tenant_id, review_id, criteria_id)
);

-- Training programs
CREATE TABLE hr.training_programs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    category VARCHAR(100), -- Safety, Technical, Compliance, Soft Skills
    training_type INTEGER NOT NULL, -- 1=Online, 2=InPerson, 3=Workshop, 4=Certification, 5=OnTheJob
    duration_hours DECIMAL(8,2),
    is_mandatory BOOLEAN NOT NULL DEFAULT FALSE,
    is_certification BOOLEAN NOT NULL DEFAULT FALSE,
    certification_validity_years INTEGER,
    provider VARCHAR(255),
    cost DECIMAL(10,2),
    currency CHAR(3) DEFAULT 'EUR',
    max_participants INTEGER,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Training sessions (scheduled instances)
CREATE TABLE hr.training_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    program_id UUID NOT NULL REFERENCES hr.training_programs(id) ON DELETE CASCADE,
    session_name VARCHAR(255) NOT NULL,
    instructor_name VARCHAR(255),
    start_date TIMESTAMP WITH TIME ZONE NOT NULL,
    end_date TIMESTAMP WITH TIME ZONE NOT NULL,
    location VARCHAR(255),
    max_participants INTEGER,
    current_participants INTEGER DEFAULT 0,
    status INTEGER NOT NULL DEFAULT 0, -- 0=Scheduled, 1=InProgress, 2=Completed, 3=Cancelled
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Training records (employee participation)
CREATE TABLE hr.training_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    employee_id UUID NOT NULL REFERENCES hr.employees(id) ON DELETE CASCADE,
    program_id UUID NOT NULL REFERENCES hr.training_programs(id),
    session_id UUID REFERENCES hr.training_sessions(id),
    
    -- Progress tracking
    status INTEGER NOT NULL DEFAULT 0, -- 0=Enrolled, 1=InProgress, 2=Completed, 3=Failed, 4=Cancelled
    start_date DATE NOT NULL,
    completion_date DATE,
    expiry_date DATE,
    score DECIMAL(5,2),
    passing_score DECIMAL(5,2),
    
    -- Certification details
    certificate_number VARCHAR(100),
    issued_by VARCHAR(255),
    
    -- Administrative
    assigned_by UUID NOT NULL, -- Reference to users.users(id)
    notes TEXT,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(tenant_id, employee_id, program_id, session_id)
);
```

### 3.5 Compliance & Audit Schema

```sql
-- Compliance requirements
CREATE TABLE hr.compliance_requirements (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    requirement_name VARCHAR(255) NOT NULL,
    description TEXT,
    category VARCHAR(100), -- GDPR, Labor Law, Safety, Industry Specific
    jurisdiction VARCHAR(100), -- Country/State where applicable
    mandatory BOOLEAN NOT NULL DEFAULT TRUE,
    frequency INTEGER, -- 1=OneTime, 2=Annual, 3=Quarterly, 4=Monthly, 5=Ongoing
    next_review_date DATE,
    responsible_person_id UUID, -- Reference to hr.employees(id)
    
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Compliance tracking
CREATE TABLE hr.compliance_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    requirement_id UUID NOT NULL REFERENCES hr.compliance_requirements(id) ON DELETE CASCADE,
    employee_id UUID REFERENCES hr.employees(id), -- NULL for organization-wide compliance
    
    status INTEGER NOT NULL DEFAULT 0, -- 0=Pending, 1=Compliant, 2=NonCompliant, 3=Expired
    compliance_date DATE,
    expiry_date DATE,
    notes TEXT,
    supporting_document_id UUID, -- Reference to files.files(id)
    
    verified_by UUID, -- Reference to users.users(id)
    verified_at TIMESTAMP WITH TIME ZONE,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- HR audit logs (extends general audit but HR-specific)
CREATE TABLE hr.audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    user_id UUID NOT NULL, -- Reference to users.users(id)
    action VARCHAR(50) NOT NULL, -- CREATE, UPDATE, DELETE, VIEW
    resource_type VARCHAR(100) NOT NULL, -- Employee, Payroll, TimeEntry, etc.
    resource_id UUID NOT NULL,
    old_values JSONB,
    new_values JSONB,
    ip_address INET,
    user_agent TEXT,
    session_id UUID,
    
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
```

### 3.6 Indexes for Performance

```sql
-- Employee indexes
CREATE INDEX idx_hr_employees_tenant_status ON hr.employees(tenant_id, employment_status) WHERE employment_status = 1;
CREATE INDEX idx_hr_employees_department ON hr.employees(tenant_id, department_id);
CREATE INDEX idx_hr_employees_manager ON hr.employees(tenant_id, direct_manager_id) WHERE direct_manager_id IS NOT NULL;
CREATE INDEX idx_hr_employees_hire_date ON hr.employees(tenant_id, hire_date);

-- Payroll indexes
CREATE INDEX idx_hr_payroll_periods_tenant_status ON hr.payroll_periods(tenant_id, status);
CREATE INDEX idx_hr_payroll_entries_period ON hr.payroll_entries(tenant_id, payroll_period_id);
CREATE INDEX idx_hr_payroll_entries_employee ON hr.payroll_entries(tenant_id, employee_id);
CREATE INDEX idx_hr_payroll_entries_payment_status ON hr.payroll_entries(tenant_id, payment_status);

-- Time tracking indexes
CREATE INDEX idx_hr_time_entries_employee_date ON hr.time_entries(tenant_id, employee_id, entry_date);
CREATE INDEX idx_hr_time_entries_status ON hr.time_entries(tenant_id, status);
CREATE INDEX idx_hr_leave_requests_employee ON hr.leave_requests(tenant_id, employee_id, status);
CREATE INDEX idx_hr_leave_requests_dates ON hr.leave_requests(tenant_id, start_date, end_date);

-- Training indexes
CREATE INDEX idx_hr_training_records_employee ON hr.training_records(tenant_id, employee_id, status);
CREATE INDEX idx_hr_training_records_expiry ON hr.training_records(tenant_id, expiry_date) WHERE expiry_date IS NOT NULL;

-- Compliance indexes
CREATE INDEX idx_hr_compliance_records_requirement ON hr.compliance_records(tenant_id, requirement_id, status);
CREATE INDEX idx_hr_compliance_records_employee ON hr.compliance_records(tenant_id, employee_id, status) WHERE employee_id IS NOT NULL;
CREATE INDEX idx_hr_compliance_records_expiry ON hr.compliance_records(tenant_id, expiry_date) WHERE expiry_date IS NOT NULL;

-- Audit indexes
CREATE INDEX idx_hr_audit_logs_tenant_resource ON hr.audit_logs(tenant_id, resource_type, resource_id);
CREATE INDEX idx_hr_audit_logs_user_date ON hr.audit_logs(tenant_id, user_id, created_at);
CREATE INDEX idx_hr_audit_logs_date ON hr.audit_logs(created_at);
```


---

## 4. Key Features Implementation

### 4.1 Automated Payroll Engine

#### Multi-Country Tax Calculation
```csharp
namespace DriveOps.HumanResources.Infrastructure.Payroll
{
    public class MultiCountryPayrollEngine : IPayrollEngine
    {
        private readonly Dictionary<string, ICountryPayrollProvider> _providers;
        private readonly ITaxCalculationService _taxService;
        private readonly ISocialChargesService _socialChargesService;
        private readonly ILogger<MultiCountryPayrollEngine> _logger;

        public async Task<PayrollCalculationResult> CalculatePayrollAsync(
            Employee employee, 
            PayrollPeriod period, 
            IEnumerable<TimeEntry> timeEntries)
        {
            var countryProvider = _providers[employee.PersonalInfo.Country];
            
            // Calculate base pay
            var basePay = await countryProvider.CalculateBasePayAsync(employee, period, timeEntries);
            
            // Calculate overtime using country-specific rules
            var overtimePay = await countryProvider.CalculateOvertimeAsync(employee, timeEntries);
            
            // Apply country-specific bonuses and allowances
            var bonuses = await countryProvider.CalculateBonusesAsync(employee, period);
            
            var grossPay = basePay + overtimePay + bonuses.Sum(b => b.Amount);
            
            // Calculate taxes and social charges
            var taxes = await _taxService.CalculateTaxesAsync(grossPay, employee);
            var socialCharges = await _socialChargesService.CalculateChargesAsync(grossPay, employee);
            
            // Apply deductions
            var deductions = await countryProvider.CalculateDeductionsAsync(employee, grossPay);
            
            var netPay = grossPay - taxes.TotalTax - socialCharges.TotalCharges - deductions.Sum(d => d.Amount);
            
            return new PayrollCalculationResult
            {
                EmployeeId = employee.Id,
                GrossPay = grossPay,
                Taxes = taxes,
                SocialCharges = socialCharges,
                Deductions = deductions,
                NetPay = netPay,
                PaymentDetails = await countryProvider.GeneratePaymentDetailsAsync(employee, netPay)
            };
        }
    }

    // France-specific payroll implementation
    public class FrancePayrollProvider : ICountryPayrollProvider
    {
        public async Task<decimal> CalculateOvertimeAsync(Employee employee, IEnumerable<TimeEntry> timeEntries)
        {
            // French labor law: 35h/week standard, 25% for first 8h overtime, 50% after
            var overtimeHours = timeEntries.Sum(t => t.OvertimeHours.TotalHours);
            var firstTierOvertime = Math.Min(overtimeHours, 8);
            var secondTierOvertime = Math.Max(0, overtimeHours - 8);
            
            var hourlyRate = employee.Contract.HourlyRate;
            return (decimal)(firstTierOvertime * hourlyRate * 1.25m + secondTierOvertime * hourlyRate * 1.5m);
        }

        public async Task<SocialCharges> CalculateSocialChargesAsync(decimal grossPay, Employee employee)
        {
            // French social charges (simplified example)
            var employeeCharges = new Dictionary<string, decimal>
            {
                ["Sécurité Sociale"] = grossPay * 0.2395m, // Social Security
                ["Assurance Chômage"] = grossPay * 0.024m, // Unemployment insurance
                ["Retraite Complémentaire"] = grossPay * 0.0347m, // Complementary retirement
                ["CSG/CRDS"] = grossPay * 0.092m // General social contribution
            };

            var employerCharges = new Dictionary<string, decimal>
            {
                ["Charges Patronales"] = grossPay * 0.42m, // Employer social charges
                ["Formation Professionnelle"] = grossPay * 0.0055m, // Professional training
                ["Taxe d'Apprentissage"] = grossPay * 0.0068m // Apprenticeship tax
            };

            return new SocialCharges(employeeCharges, employerCharges);
        }
    }
}
```

### 4.2 Compliance Management Framework

#### GDPR Compliance Implementation
```csharp
namespace DriveOps.HumanResources.Application.Compliance
{
    public class GDPRComplianceService : IGDPRComplianceService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDataEncryptionService _encryptionService;
        private readonly IAuditService _auditService;
        private readonly INotificationService _notificationService;

        public async Task<GDPRComplianceReport> GenerateComplianceReportAsync(TenantId tenantId)
        {
            var employees = await _employeeRepository.GetAllByTenantAsync(tenantId);
            var report = new GDPRComplianceReport();

            foreach (var employee in employees)
            {
                // Check data minimization
                await CheckDataMinimizationAsync(employee, report);
                
                // Check consent records
                await CheckConsentRecordsAsync(employee, report);
                
                // Check data retention policies
                await CheckDataRetentionAsync(employee, report);
                
                // Check access logs
                await CheckDataAccessLogsAsync(employee, report);
            }

            return report;
        }

        public async Task<Result> ProcessDataSubjectRequestAsync(DataSubjectRequest request)
        {
            var employee = await _employeeRepository.GetByEmailAsync(request.Email);
            if (employee == null)
                return Result.Failure("Employee not found");

            switch (request.RequestType)
            {
                case DataSubjectRequestType.Access:
                    return await ProcessAccessRequestAsync(employee, request);
                    
                case DataSubjectRequestType.Rectification:
                    return await ProcessRectificationRequestAsync(employee, request);
                    
                case DataSubjectRequestType.Erasure:
                    return await ProcessErasureRequestAsync(employee, request);
                    
                case DataSubjectRequestType.Portability:
                    return await ProcessPortabilityRequestAsync(employee, request);
                    
                default:
                    return Result.Failure("Invalid request type");
            }
        }

        private async Task<Result> ProcessErasureRequestAsync(Employee employee, DataSubjectRequest request)
        {
            // Check if employee can be deleted (legal obligations, ongoing contracts, etc.)
            var canDelete = await CanDeleteEmployeeDataAsync(employee);
            if (!canDelete.IsSuccess)
                return canDelete;

            // Anonymize instead of delete if required for legal/business purposes
            if (await RequiresAnonymizationAsync(employee))
            {
                await AnonymizeEmployeeDataAsync(employee);
                await _auditService.LogAsync(new AuditEntry
                {
                    Action = "DATA_ANONYMIZED",
                    ResourceType = "Employee",
                    ResourceId = employee.Id.Value.ToString(),
                    Reason = "GDPR Right to Erasure Request"
                });
            }
            else
            {
                await DeleteEmployeeDataAsync(employee);
                await _auditService.LogAsync(new AuditEntry
                {
                    Action = "DATA_DELETED",
                    ResourceType = "Employee", 
                    ResourceId = employee.Id.Value.ToString(),
                    Reason = "GDPR Right to Erasure Request"
                });
            }

            return Result.Success();
        }
    }
}
```

### 4.3 Employee Self-Service Portal

#### Employee Dashboard Component
```csharp
@page "/employee/dashboard"
@using DriveOps.HumanResources.Application.DTOs
@inject IEmployeeDashboardService DashboardService
@inject ICurrentUserService CurrentUserService

<PageTitle>Employee Dashboard</PageTitle>

<RadzenStack Gap="1rem">
    <RadzenRow>
        <RadzenColumn Size="8">
            <RadzenCard>
                <RadzenText TextStyle="TextStyle.H5">Welcome back, @currentEmployee?.FirstName!</RadzenText>
                <RadzenText TextStyle="TextStyle.Subtitle1">@currentEmployee?.Position?.Title</RadzenText>
            </RadzenCard>
        </RadzenColumn>
        <RadzenColumn Size="4">
            <RadzenCard>
                <RadzenText TextStyle="TextStyle.H6">Time Off Balance</RadzenText>
                <RadzenText>@(dashboard?.LeaveBalance?.AvailableDays ?? 0) days available</RadzenText>
            </RadzenCard>
        </RadzenColumn>
    </RadzenRow>

    <RadzenRow>
        <RadzenColumn Size="6">
            <RadzenCard>
                <RadzenText TextStyle="TextStyle.H6">Recent Payslips</RadzenText>
                <RadzenDataList Data="@dashboard?.RecentPayslips" TItem="PayslipSummaryDto">
                    <Template>
                        <RadzenRow Style="align-items: center;">
                            <RadzenColumn Size="8">
                                <RadzenText TextStyle="TextStyle.Body1">@context.PeriodName</RadzenText>
                                <RadzenText TextStyle="TextStyle.Caption">@context.PayDate.ToShortDateString()</RadzenText>
                            </RadzenColumn>
                            <RadzenColumn Size="4">
                                <RadzenButton Text="Download" Icon="download" Size="ButtonSize.Small" 
                                            Click="() => DownloadPayslip(context.Id)" />
                            </RadzenColumn>
                        </RadzenRow>
                    </Template>
                </RadzenDataList>
            </RadzenCard>
        </RadzenColumn>
        
        <RadzenColumn Size="6">
            <RadzenCard>
                <RadzenText TextStyle="TextStyle.H6">Upcoming Training</RadzenText>
                <RadzenDataList Data="@dashboard?.UpcomingTraining" TItem="TrainingSessionDto">
                    <Template>
                        <RadzenRow>
                            <RadzenColumn Size="8">
                                <RadzenText TextStyle="TextStyle.Body1">@context.Name</RadzenText>
                                <RadzenText TextStyle="TextStyle.Caption">@context.StartDate.ToShortDateString()</RadzenText>
                            </RadzenColumn>
                            <RadzenColumn Size="4">
                                <RadzenBadge BadgeStyle="@GetTrainingStatusStyle(context.Status)" 
                                           Text="@context.Status.ToString()" />
                            </RadzenColumn>
                        </RadzenRow>
                    </Template>
                </RadzenDataList>
            </RadzenCard>
        </RadzenColumn>
    </RadzenRow>

    <RadzenRow>
        <RadzenColumn Size="12">
            <RadzenCard>
                <RadzenText TextStyle="TextStyle.H6">Quick Actions</RadzenText>
                <RadzenStack Orientation="Orientation.Horizontal" Gap="1rem">
                    <RadzenButton Text="Request Time Off" Icon="event" Click="RequestTimeOff" />
                    <RadzenButton Text="Clock In/Out" Icon="schedule" Click="ClockInOut" />
                    <RadzenButton Text="Update Profile" Icon="person" Click="UpdateProfile" />
                    <RadzenButton Text="View Documents" Icon="folder" Click="ViewDocuments" />
                </RadzenStack>
            </RadzenCard>
        </RadzenColumn>
    </RadzenRow>
</RadzenStack>

@code {
    private EmployeeDashboardDto? dashboard;
    private EmployeeDto? currentEmployee;

    protected override async Task OnInitializedAsync()
    {
        var currentUser = await CurrentUserService.GetCurrentUserAsync();
        dashboard = await DashboardService.GetEmployeeDashboardAsync(currentUser.Id);
        currentEmployee = dashboard?.Employee;
    }

    private async Task DownloadPayslip(Guid payslipId)
    {
        // Implementation for payslip download
        var payslipPdf = await DashboardService.GeneratePayslipPdfAsync(payslipId);
        // Trigger download
    }

    private BadgeStyle GetTrainingStatusStyle(TrainingStatus status)
    {
        return status switch
        {
            TrainingStatus.Enrolled => BadgeStyle.Info,
            TrainingStatus.InProgress => BadgeStyle.Warning,
            TrainingStatus.Completed => BadgeStyle.Success,
            TrainingStatus.Overdue => BadgeStyle.Danger,
            _ => BadgeStyle.Secondary
        };
    }
}
```

### 4.4 Mobile Time Tracking Application

#### Mobile Clock-In/Out Component
```typescript
// Mobile app component for time tracking
import { Component, OnInit } from '@angular/core';
import { Geolocation } from '@capacitor/geolocation';
import { Camera, CameraResultType } from '@capacitor/camera';

@Component({
  selector: 'app-time-tracking',
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-title>Time Tracking</ion-title>
      </ion-toolbar>
    </ion-header>

    <ion-content>
      <ion-card>
        <ion-card-header>
          <ion-card-title>{{ isClocked ? 'Clock Out' : 'Clock In' }}</ion-card-title>
          <ion-card-subtitle>{{ currentLocation }}</ion-card-subtitle>
        </ion-card-header>
        
        <ion-card-content>
          <div class="current-time">
            {{ currentTime | date:'HH:mm:ss' }}
          </div>
          
          <ion-button 
            expand="block" 
            [color]="isClocked ? 'danger' : 'success'"
            (click)="toggleClock()"
            [disabled]="isProcessing">
            <ion-icon [name]="isClocked ? 'log-out-outline' : 'log-in-outline'"></ion-icon>
            {{ isClocked ? 'Clock Out' : 'Clock In' }}
          </ion-button>

          <ion-item *ngIf="isClocked">
            <ion-label>Working since: {{ clockedInTime | date:'HH:mm' }}</ion-label>
          </ion-item>

          <ion-item>
            <ion-label>Take photo</ion-label>
            <ion-button fill="clear" slot="end" (click)="takePhoto()">
              <ion-icon name="camera"></ion-icon>
            </ion-button>
          </ion-item>

          <ion-textarea 
            placeholder="Notes (optional)"
            [(ngModel)]="notes"
            rows="3">
          </ion-textarea>
        </ion-card-content>
      </ion-card>

      <ion-card>
        <ion-card-header>
          <ion-card-title>Today's Summary</ion-card-title>
        </ion-card-header>
        <ion-card-content>
          <ion-item>
            <ion-label>Hours Worked</ion-label>
            <ion-note slot="end">{{ todayHours | number:'1.2-2' }}h</ion-note>
          </ion-item>
          <ion-item>
            <ion-label>Overtime</ion-label>
            <ion-note slot="end">{{ overtimeHours | number:'1.2-2' }}h</ion-note>
          </ion-item>
          <ion-item>
            <ion-label>Break Time</ion-label>
            <ion-note slot="end">{{ breakHours | number:'1.2-2' }}h</ion-note>
          </ion-item>
        </ion-card-content>
      </ion-card>
    </ion-content>
  `
})
export class TimeTrackingComponent implements OnInit {
  isClocked = false;
  isProcessing = false;
  currentTime = new Date();
  currentLocation = '';
  clockedInTime?: Date;
  notes = '';
  todayHours = 0;
  overtimeHours = 0;
  breakHours = 0;

  constructor(
    private timeTrackingService: TimeTrackingService,
    private toastController: ToastController
  ) {}

  async ngOnInit() {
    setInterval(() => {
      this.currentTime = new Date();
    }, 1000);

    await this.loadCurrentStatus();
    await this.getCurrentLocation();
  }

  async toggleClock() {
    this.isProcessing = true;
    
    try {
      const location = await this.getCurrentPosition();
      const photo = await this.takePhoto();
      
      if (this.isClocked) {
        await this.clockOut(location, photo);
      } else {
        await this.clockIn(location, photo);
      }
      
      await this.loadCurrentStatus();
    } catch (error) {
      await this.showError('Error processing clock in/out');
    } finally {
      this.isProcessing = false;
    }
  }

  private async clockIn(location: Position, photo?: string) {
    const request = {
      clockInTime: new Date(),
      location: {
        latitude: location.coords.latitude,
        longitude: location.coords.longitude,
        accuracy: location.coords.accuracy
      },
      photo,
      notes: this.notes
    };

    await this.timeTrackingService.clockIn(request);
    this.isClocked = true;
    this.clockedInTime = new Date();
    await this.showSuccess('Clocked in successfully');
  }

  private async clockOut(location: Position, photo?: string) {
    const request = {
      clockOutTime: new Date(),
      location: {
        latitude: location.coords.latitude,
        longitude: location.coords.longitude,
        accuracy: location.coords.accuracy
      },
      photo,
      notes: this.notes
    };

    await this.timeTrackingService.clockOut(request);
    this.isClocked = false;
    this.clockedInTime = undefined;
    await this.showSuccess('Clocked out successfully');
  }

  private async getCurrentPosition(): Promise<Position> {
    const position = await Geolocation.getCurrentPosition({
      enableHighAccuracy: true,
      timeout: 10000
    });
    return position;
  }

  async takePhoto(): Promise<string | undefined> {
    try {
      const image = await Camera.getPhoto({
        quality: 90,
        allowEditing: false,
        resultType: CameraResultType.DataUrl
      });
      return image.dataUrl;
    } catch (error) {
      // Photo is optional, don't fail the entire operation
      return undefined;
    }
  }
}
```


---

## 5. Integration Points with DriveOps Ecosystem

### 5.1 Core Users Module Extension

The RH/PERSONNEL module seamlessly extends the Core Users module while maintaining backward compatibility:

```csharp
namespace DriveOps.HumanResources.Integration
{
    public class UserToEmployeeMapper : IUserToEmployeeMapper
    {
        private readonly IUserService _userService;
        private readonly IEmployeeRepository _employeeRepository;

        public async Task<Employee?> GetEmployeeFromUserAsync(UserId userId)
        {
            return await _employeeRepository.GetByUserIdAsync(userId);
        }

        public async Task<User> GetUserFromEmployeeAsync(EmployeeId employeeId)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            return await _userService.GetByIdAsync(employee.UserId);
        }

        // Automatically create employee profile when user is created
        public async Task HandleUserCreatedEvent(UserCreatedEvent userEvent)
        {
            if (userEvent.UserType == UserType.Employee)
            {
                var employee = new Employee(
                    userEvent.TenantId,
                    userEvent.UserId,
                    EmploymentContract.Default(),
                    PersonalInfo.FromUser(userEvent.User),
                    DateTime.UtcNow,
                    DepartmentId.Default(),
                    PositionId.Default()
                );

                await _employeeRepository.AddAsync(employee);
            }
        }
    }
}
```

### 5.2 Garage Module Integration

#### Mechanic Certification Tracking
```csharp
namespace DriveOps.HumanResources.Integration.Garage
{
    public class GarageHRIntegrationService : IGarageHRIntegrationService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ITrainingRepository _trainingRepository;
        private readonly ICertificationService _certificationService;

        public async Task<List<EmployeeSkillDto>> GetCertifiedMechanicsAsync(TenantId tenantId, string skillType)
        {
            var mechanics = await _employeeRepository.GetByDepartmentAsync(tenantId, "Mechanics");
            var certifiedMechanics = new List<EmployeeSkillDto>();

            foreach (var mechanic in mechanics)
            {
                var certifications = await _certificationService.GetActiveCertificationsAsync(mechanic.Id);
                var relevantCerts = certifications.Where(c => c.SkillCategory == skillType).ToList();
                
                if (relevantCerts.Any())
                {
                    certifiedMechanics.Add(new EmployeeSkillDto
                    {
                        EmployeeId = mechanic.Id,
                        EmployeeName = $"{mechanic.PersonalInfo.FirstName} {mechanic.PersonalInfo.LastName}",
                        Certifications = relevantCerts,
                        IsAvailable = await IsEmployeeAvailableAsync(mechanic.Id)
                    });
                }
            }

            return certifiedMechanics;
        }

        // Integrate work order assignment with employee skills
        public async Task<Result<EmployeeId>> AssignWorkOrderToQualifiedMechanicAsync(
            WorkOrderId workOrderId, 
            string requiredSkills)
        {
            var qualifiedMechanics = await GetCertifiedMechanicsAsync(
                await GetTenantIdFromWorkOrderAsync(workOrderId), 
                requiredSkills);

            var availableMechanic = qualifiedMechanics
                .Where(m => m.IsAvailable)
                .OrderByDescending(m => m.SkillLevel)
                .FirstOrDefault();

            if (availableMechanic == null)
                return Result.Failure<EmployeeId>("No qualified mechanics available");

            // Update work order assignment in Garage module
            await UpdateWorkOrderAssignmentAsync(workOrderId, availableMechanic.EmployeeId);

            return Result.Success(availableMechanic.EmployeeId);
        }

        // Track safety training completion for garage workers
        public async Task EnsureSafetyComplianceAsync(EmployeeId employeeId)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            var safetyTraining = await _trainingRepository.GetMandatoryTrainingAsync(
                employee.TenantId, "Safety");

            foreach (var training in safetyTraining)
            {
                var record = await _trainingRepository.GetEmployeeTrainingRecordAsync(
                    employeeId, training.Id);

                if (record == null || record.IsExpired)
                {
                    await _trainingRepository.AssignTrainingAsync(employeeId, training.Id);
                    // Send notification about mandatory training
                    await SendMandatoryTrainingNotificationAsync(employee, training);
                }
            }
        }
    }
}
```

### 5.3 VTC/Transport Module Integration

#### Driver License and Certification Management
```csharp
namespace DriveOps.HumanResources.Integration.VTC
{
    public class VTCDriverHRService : IVTCDriverHRService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IDocumentService _documentService;
        private readonly IComplianceService _complianceService;

        public async Task<DriverEligibilityResult> CheckDriverEligibilityAsync(EmployeeId employeeId)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            var eligibilityChecks = new List<ComplianceCheck>();

            // Check driving license validity
            var drivingLicense = await _documentService.GetLatestDocumentAsync(
                employeeId, DocumentType.DrivingLicense);
            
            eligibilityChecks.Add(new ComplianceCheck
            {
                Name = "Driving License",
                IsCompliant = drivingLicense?.IsValid == true,
                ExpiryDate = drivingLicense?.ExpiryDate,
                Details = drivingLicense?.IsValid == true ? "Valid" : "Invalid or expired"
            });

            // Check VTC professional license
            var vtcLicense = await _documentService.GetLatestDocumentAsync(
                employeeId, DocumentType.VTCLicense);
            
            eligibilityChecks.Add(new ComplianceCheck
            {
                Name = "VTC License",
                IsCompliant = vtcLicense?.IsValid == true,
                ExpiryDate = vtcLicense?.ExpiryDate,
                Details = vtcLicense?.IsValid == true ? "Valid" : "Required for VTC operations"
            });

            // Check medical certificate
            var medicalCert = await _documentService.GetLatestDocumentAsync(
                employeeId, DocumentType.MedicalCertificate);
            
            eligibilityChecks.Add(new ComplianceCheck
            {
                Name = "Medical Certificate",
                IsCompliant = medicalCert?.IsValid == true,
                ExpiryDate = medicalCert?.ExpiryDate,
                Details = medicalCert?.IsValid == true ? "Valid" : "Medical clearance required"
            });

            // Check background check
            var backgroundCheck = await _complianceService.GetLatestBackgroundCheckAsync(employeeId);
            
            eligibilityChecks.Add(new ComplianceCheck
            {
                Name = "Background Check",
                IsCompliant = backgroundCheck?.IsClean == true && backgroundCheck?.IsValid == true,
                ExpiryDate = backgroundCheck?.ExpiryDate,
                Details = backgroundCheck?.IsClean == true ? "Clean record" : "Issues found"
            });

            var isEligible = eligibilityChecks.All(c => c.IsCompliant);
            
            return new DriverEligibilityResult
            {
                IsEligible = isEligible,
                Checks = eligibilityChecks,
                ExpiringDocuments = eligibilityChecks
                    .Where(c => c.ExpiryDate.HasValue && c.ExpiryDate.Value <= DateTime.UtcNow.AddDays(30))
                    .ToList()
            };
        }

        public async Task<List<EmployeeDto>> GetAvailableDriversAsync(
            TenantId tenantId, 
            DateTime startTime, 
            DateTime endTime,
            string vehicleCategory = null)
        {
            var drivers = await _employeeRepository.GetByPositionAsync(tenantId, "Driver");
            var availableDrivers = new List<EmployeeDto>();

            foreach (var driver in drivers)
            {
                var eligibility = await CheckDriverEligibilityAsync(driver.Id);
                if (!eligibility.IsEligible)
                    continue;

                var isAvailable = await CheckDriverAvailabilityAsync(driver.Id, startTime, endTime);
                if (!isAvailable)
                    continue;

                if (!string.IsNullOrEmpty(vehicleCategory))
                {
                    var hasVehicleCertification = await HasVehicleCertificationAsync(
                        driver.Id, vehicleCategory);
                    if (!hasVehicleCertification)
                        continue;
                }

                availableDrivers.Add(MapToDto(driver));
            }

            return availableDrivers;
        }

        private async Task<bool> CheckDriverAvailabilityAsync(
            EmployeeId driverId, 
            DateTime startTime, 
            DateTime endTime)
        {
            // Check work schedule
            var schedule = await GetEmployeeScheduleAsync(driverId, startTime.Date);
            if (schedule == null || !schedule.IsWorkingTime(startTime, endTime))
                return false;

            // Check for existing assignments
            var existingAssignments = await GetDriverAssignmentsAsync(driverId, startTime, endTime);
            if (existingAssignments.Any())
                return false;

            // Check for leave requests
            var leaveRequests = await GetApprovedLeaveRequestsAsync(driverId, startTime, endTime);
            if (leaveRequests.Any())
                return false;

            return true;
        }
    }
}
```

### 5.4 Security Services Module Integration

#### Security Clearance and Training Management
```csharp
namespace DriveOps.HumanResources.Integration.Security
{
    public class SecurityPersonnelHRService : ISecurityPersonnelHRService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ISecurityClearanceService _clearanceService;
        private readonly ITrainingRepository _trainingRepository;
        private readonly IIncidentReportingService _incidentService;

        public async Task<SecurityClearanceStatus> GetSecurityClearanceStatusAsync(EmployeeId employeeId)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            var clearances = await _clearanceService.GetEmployeeClearancesAsync(employeeId);
            
            return new SecurityClearanceStatus
            {
                EmployeeId = employeeId,
                ActiveClearances = clearances.Where(c => c.IsActive).ToList(),
                ExpiringSoon = clearances.Where(c => c.ExpiryDate <= DateTime.UtcNow.AddDays(90)).ToList(),
                RequiredTraining = await GetRequiredSecurityTrainingAsync(employeeId),
                IncidentHistory = await _incidentService.GetEmployeeIncidentsAsync(employeeId)
            };
        }

        public async Task<List<EmployeeDto>> GetQualifiedSecurityPersonnelAsync(
            TenantId tenantId,
            SecurityClearanceLevel requiredLevel,
            string[] requiredCertifications)
        {
            var securityPersonnel = await _employeeRepository.GetByDepartmentAsync(tenantId, "Security");
            var qualifiedPersonnel = new List<EmployeeDto>();

            foreach (var employee in securityPersonnel)
            {
                var clearanceStatus = await GetSecurityClearanceStatusAsync(employee.Id);
                
                // Check clearance level
                var hasRequiredClearance = clearanceStatus.ActiveClearances
                    .Any(c => c.Level >= requiredLevel);
                
                if (!hasRequiredClearance)
                    continue;

                // Check required certifications
                var employeeCertifications = await GetEmployeeCertificationsAsync(employee.Id);
                var hasAllCertifications = requiredCertifications.All(req => 
                    employeeCertifications.Any(cert => cert.Name == req && cert.IsValid));

                if (!hasAllCertifications)
                    continue;

                // Check for disqualifying incidents
                var hasRecentIncidents = clearanceStatus.IncidentHistory
                    .Any(i => i.Severity >= IncidentSeverity.Major && 
                             i.OccurredAt >= DateTime.UtcNow.AddYears(-2));

                if (hasRecentIncidents)
                    continue;

                qualifiedPersonnel.Add(MapToDto(employee));
            }

            return qualifiedPersonnel;
        }

        public async Task HandleSecurityIncidentAsync(SecurityIncident incident)
        {
            // Update employee record with incident
            await _incidentService.RecordIncidentAsync(incident);

            // Check if incident affects employee's security clearance
            if (incident.Severity >= IncidentSeverity.Major)
            {
                await _clearanceService.ReviewClearanceAsync(
                    incident.EmployeeId, 
                    ClearanceReviewReason.SecurityIncident);

                // Assign mandatory retraining if required
                var retrainingPrograms = await GetIncidentRetrainingProgramsAsync(incident.Type);
                foreach (var program in retrainingPrograms)
                {
                    await _trainingRepository.AssignMandatoryTrainingAsync(
                        incident.EmployeeId, program.Id);
                }
            }

            // Notify HR and security management
            await NotifySecurityIncidentAsync(incident);
        }

        private async Task<List<TrainingProgram>> GetRequiredSecurityTrainingAsync(EmployeeId employeeId)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            var allSecurityTraining = await _trainingRepository.GetByCategory(
                employee.TenantId, "Security");

            var requiredTraining = new List<TrainingProgram>();

            foreach (var training in allSecurityTraining)
            {
                if (training.IsMandatory)
                {
                    var record = await _trainingRepository.GetEmployeeTrainingRecordAsync(
                        employeeId, training.Id);

                    if (record == null || record.IsExpired || record.Status != TrainingStatus.Completed)
                    {
                        requiredTraining.Add(training);
                    }
                }
            }

            return requiredTraining;
        }
    }
}
```

---

## 6. Pricing and Revenue Model

### 6.1 Subscription Tiers

The RH/PERSONNEL module offers three subscription tiers designed to scale with business needs:

#### RH Basic - 19€/month per employee (up to 10 employees)
**Target Market**: Small automotive businesses, independent garages

**Included Features**:
- Basic employee profiles and contact management
- Simple time tracking with manual clock-in/out
- Basic payroll calculation (gross to net)
- Essential leave management
- Document storage (contracts, ID copies)
- Basic reporting (payroll summaries, time reports)
- Email notifications
- Single country tax compliance
- Basic mobile app access

**Limitations**:
- Maximum 10 employees
- Manual payroll processing
- Limited integrations
- Standard support (email only)
- Single location only

#### RH Pro - 29€/month per employee
**Target Market**: Medium-sized automotive businesses, multi-location operations

**Enhanced Features** (includes all Basic features plus):
- Advanced time tracking with GPS verification
- Automated payroll processing with direct deposit
- Performance management system
- Training and certification tracking
- Advanced leave management with approval workflows
- Compliance monitoring and alerts
- Advanced reporting and analytics
- Multi-country support
- Department and position management
- Advanced mobile app with offline capabilities
- API access for integrations
- Priority support (email + phone)

#### RH Enterprise - 39€/month per employee
**Target Market**: Large automotive enterprises, franchise operations

**Premium Features** (includes all Pro features plus):
- Multi-site centralized management
- Advanced compliance framework (GDPR, industry-specific)
- 360-degree performance reviews
- Succession planning and career development
- Advanced analytics and predictive modeling
- Custom integrations and API access
- White-label mobile applications
- Advanced security features
- Dedicated customer success manager
- Custom training and onboarding
- Service Level Agreement (SLA)
- Multi-language support

### 6.2 Additional Revenue Streams

#### Implementation and Professional Services
```csharp
namespace DriveOps.HumanResources.Billing
{
    public class ProfessionalServicesCalculator
    {
        public decimal CalculateImplementationCost(ImplementationRequest request)
        {
            var baseCost = request.EmployeeCount switch
            {
                <= 10 => 1000m,
                <= 50 => 2500m,
                <= 100 => 5000m,
                <= 500 => 10000m,
                _ => 15000m
            };

            // Add complexity factors
            var complexityMultiplier = 1.0m;
            
            if (request.RequiresDataMigration)
                complexityMultiplier += 0.3m;
                
            if (request.RequiresCustomIntegrations)
                complexityMultiplier += 0.5m;
                
            if (request.MultipleCountries)
                complexityMultiplier += 0.4m;
                
            if (request.ComplexPayrollRules)
                complexityMultiplier += 0.6m;

            return baseCost * complexityMultiplier;
        }
    }
}
```

**Service Pricing**:
- **Basic Setup** (up to 10 employees): €1,000
- **Standard Implementation** (11-50 employees): €2,500
- **Advanced Implementation** (51-100 employees): €5,000
- **Enterprise Implementation** (100+ employees): €10,000+
- **Data Migration**: €500-2,000 per system
- **Custom Integration**: €2,000-10,000 per integration
- **Training Sessions**: €200 per user (minimum 10 users)

#### Ongoing Professional Services
- **Compliance Consulting**: €150/hour
- **Custom Report Development**: €500-2,000 per report
- **Additional Training**: €200 per user
- **Priority Support Upgrade**: €50/employee/month
- **Custom Mobile App**: €5,000-15,000 one-time

### 6.3 Revenue Model Innovation

#### Per-Employee Pricing Advantages
```csharp
public class RevenueProjectionCalculator
{
    public RevenueProjection CalculateProjection(TenantGrowthModel growthModel)
    {
        var projection = new RevenueProjection();
        
        for (int month = 1; month <= 36; month++)
        {
            var employeeCount = CalculateEmployeeCount(growthModel, month);
            var tier = DetermineTier(employeeCount, growthModel);
            var monthlyRevenue = CalculateMonthlyRevenue(employeeCount, tier);
            
            projection.AddMonth(month, employeeCount, monthlyRevenue);
        }
        
        return projection;
    }

    private decimal CalculateMonthlyRevenue(int employeeCount, SubscriptionTier tier)
    {
        var pricePerEmployee = tier switch
        {
            SubscriptionTier.Basic => 19m,
            SubscriptionTier.Pro => 29m,
            SubscriptionTier.Enterprise => 39m,
            _ => throw new ArgumentException("Invalid tier")
        };

        // Volume discounts for large accounts
        var volumeDiscount = employeeCount switch
        {
            >= 1000 => 0.15m, // 15% discount
            >= 500 => 0.10m,  // 10% discount
            >= 100 => 0.05m,  // 5% discount
            _ => 0m
        };

        var grossRevenue = employeeCount * pricePerEmployee;
        return grossRevenue * (1 - volumeDiscount);
    }
}
```

**Key Revenue Advantages**:
1. **Scalable Growth**: Revenue grows directly with client success
2. **High Customer Lifetime Value**: Long-term contracts with growing businesses
3. **Predictable Recurring Revenue**: Monthly subscription model
4. **Expansion Revenue**: Natural upselling as businesses grow
5. **Sticky Revenue**: High switching costs due to HR data sensitivity

### 6.4 Market Penetration Strategy

#### Automotive Industry Focus
- **Target Market Size**: 150,000+ automotive businesses in Europe
- **Initial Focus**: France, Germany, UK (largest markets)
- **Market Segments**:
  - Independent garages: 80,000+ businesses
  - VTC/rideshare companies: 15,000+ businesses
  - Security services: 25,000+ businesses
  - Fleet management: 10,000+ businesses
  - Auto dealerships: 20,000+ businesses

#### Competitive Positioning
```markdown
| Feature | DriveOps HR | Generic HR Solutions | Industry Competitors |
|---------|-------------|---------------------|---------------------|
| Automotive Focus | ✅ | ❌ | Partial |
| Multi-Business Integration | ✅ | ❌ | ❌ |
| Compliance Automation | ✅ | Partial | ❌ |
| Mobile-First Design | ✅ | Partial | Partial |
| Per-Employee Pricing | ✅ | ❌ | ❌ |
| Multi-Country Support | ✅ | Partial | ❌ |
```

#### Revenue Projections (3-Year)
- **Year 1**: €2.5M revenue (2,000 employees across 150 clients)
- **Year 2**: €8.5M revenue (7,500 employees across 400 clients)
- **Year 3**: €18M revenue (15,000 employees across 750 clients)

**Assumptions**:
- 35% annual client growth
- 40% annual employee growth per client
- 25% tier upgrade rate per year
- 85% retention rate

### 6.5 Billing Integration

#### Automated Billing System
```csharp
namespace DriveOps.HumanResources.Billing
{
    public class HRBillingService : IBillingService
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IBillingRepository _billingRepository;
        private readonly ISubscriptionService _subscriptionService;

        public async Task<Invoice> GenerateMonthlyInvoiceAsync(TenantId tenantId, DateTime billingMonth)
        {
            var subscription = await _subscriptionService.GetActiveSubscriptionAsync(tenantId);
            var activeEmployees = await _employeeRepository.GetActiveEmployeeCountAsync(
                tenantId, billingMonth);

            var baseAmount = CalculateBaseAmount(activeEmployees, subscription.Tier);
            var addOns = await CalculateAddOnChargesAsync(tenantId, billingMonth);
            var discounts = await CalculateDiscountsAsync(tenantId, activeEmployees);

            var invoice = new Invoice
            {
                TenantId = tenantId,
                BillingPeriod = billingMonth,
                EmployeeCount = activeEmployees,
                BaseAmount = baseAmount,
                AddOnCharges = addOns,
                Discounts = discounts,
                TotalAmount = baseAmount + addOns.Sum(a => a.Amount) - discounts.Sum(d => d.Amount)
            };

            await _billingRepository.SaveInvoiceAsync(invoice);
            return invoice;
        }

        private async Task<List<AddOnCharge>> CalculateAddOnChargesAsync(TenantId tenantId, DateTime month)
        {
            var charges = new List<AddOnCharge>();

            // API usage charges
            var apiUsage = await GetAPIUsageAsync(tenantId, month);
            if (apiUsage.RequestCount > 10000) // Free tier limit
            {
                var overageRequests = apiUsage.RequestCount - 10000;
                charges.Add(new AddOnCharge
                {
                    Description = "API Overage",
                    Quantity = overageRequests,
                    UnitPrice = 0.01m,
                    Amount = overageRequests * 0.01m
                });
            }

            // Storage overage charges
            var storageUsage = await GetStorageUsageAsync(tenantId, month);
            if (storageUsage.GigabytesUsed > 100) // Free tier limit
            {
                var overageGB = storageUsage.GigabytesUsed - 100;
                charges.Add(new AddOnCharge
                {
                    Description = "Storage Overage",
                    Quantity = overageGB,
                    UnitPrice = 2m,
                    Amount = overageGB * 2m
                });
            }

            return charges;
        }
    }
}
```

This comprehensive pricing and revenue model positions the RH/PERSONNEL module as a premium, industry-specific solution that grows with client success while maintaining high profitability and market competitiveness.


---

## 7. Multi-Country Compliance Framework

### 7.1 Localization Architecture

```csharp
namespace DriveOps.HumanResources.Compliance
{
    public interface ICountryComplianceProvider
    {
        string CountryCode { get; }
        Task<TaxCalculation> CalculateTaxesAsync(decimal grossPay, EmployeeProfile profile);
        Task<SocialCharges> CalculateSocialChargesAsync(decimal grossPay, EmployeeProfile profile);
        Task<List<ComplianceRequirement>> GetLaborLawRequirementsAsync();
        Task<List<MandatoryDocument>> GetRequiredDocumentsAsync(EmploymentType type);
        Task<WorkingTimeRules> GetWorkingTimeRulesAsync();
        Task<LeaveEntitlements> GetLeaveEntitlementsAsync(EmploymentType type, int yearsOfService);
    }

    public class FranceComplianceProvider : ICountryComplianceProvider
    {
        public string CountryCode => "FR";

        public async Task<TaxCalculation> CalculateTaxesAsync(decimal grossPay, EmployeeProfile profile)
        {
            // French tax calculation with progressive rates
            var taxableIncome = grossPay - CalculateDeductions(grossPay);
            var incomeTax = CalculateProgressiveTax(taxableIncome, profile.TaxBracket);
            
            return new TaxCalculation
            {
                IncomeTax = incomeTax,
                SocialSecurityTax = grossPay * 0.2395m, // Employee social charges
                UnemploymentTax = grossPay * 0.024m,
                CSG_CRDS = grossPay * 0.092m, // French specific taxes
                TotalTax = incomeTax + (grossPay * 0.3555m),
                TaxJurisdiction = "France"
            };
        }

        public async Task<WorkingTimeRules> GetWorkingTimeRulesAsync()
        {
            return new WorkingTimeRules
            {
                MaxDailyHours = 10,
                MaxWeeklyHours = 48,
                StandardWeeklyHours = 35,
                MaxConsecutiveDays = 6,
                MinRestBetweenShifts = TimeSpan.FromHours(11),
                OvertimeRules = new List<OvertimeRule>
                {
                    new() { FromHour = 35, ToHour = 43, Rate = 1.25m },
                    new() { FromHour = 43, ToHour = null, Rate = 1.5m }
                },
                NightWorkDefinition = new NightWorkRule
                {
                    StartTime = TimeSpan.FromHours(21),
                    EndTime = TimeSpan.FromHours(6),
                    Premium = 0.1m
                }
            };
        }

        public async Task<LeaveEntitlements> GetLeaveEntitlementsAsync(EmploymentType type, int yearsOfService)
        {
            return new LeaveEntitlements
            {
                AnnualLeaveDays = 25, // 5 weeks is standard in France
                SickLeaveDays = 365, // No statutory limit with proper medical certificates
                MaternityLeaveDays = 112, // 16 weeks
                PaternityLeaveDays = 28, // 4 weeks since 2021
                ParentalLeaveDays = 1095, // 3 years unpaid
                PublicHolidays = GetFrenchPublicHolidays()
            };
        }

        private List<PublicHoliday> GetFrenchPublicHolidays()
        {
            return new List<PublicHoliday>
            {
                new("Jour de l'An", new DateTime(DateTime.Now.Year, 1, 1)),
                new("Lundi de Pâques", CalculateEasterMonday()),
                new("Fête du Travail", new DateTime(DateTime.Now.Year, 5, 1)),
                new("Victoire 1945", new DateTime(DateTime.Now.Year, 5, 8)),
                new("Ascension", CalculateAscension()),
                new("Lundi de Pentecôte", CalculatePentecostMonday()),
                new("Fête Nationale", new DateTime(DateTime.Now.Year, 7, 14)),
                new("Assomption", new DateTime(DateTime.Now.Year, 8, 15)),
                new("Toussaint", new DateTime(DateTime.Now.Year, 11, 1)),
                new("Armistice 1918", new DateTime(DateTime.Now.Year, 11, 11)),
                new("Noël", new DateTime(DateTime.Now.Year, 12, 25))
            };
        }
    }

    public class GermanyComplianceProvider : ICountryComplianceProvider
    {
        public string CountryCode => "DE";

        public async Task<TaxCalculation> CalculateTaxesAsync(decimal grossPay, EmployeeProfile profile)
        {
            // German tax calculation with church tax consideration
            var incomeTax = CalculateGermanIncomeTax(grossPay, profile.TaxClass);
            var churchTax = profile.ChurchTaxLiable ? incomeTax * 0.08m : 0m; // 8% church tax
            var solidarityTax = incomeTax * 0.055m; // 5.5% solidarity surcharge

            return new TaxCalculation
            {
                IncomeTax = incomeTax,
                ChurchTax = churchTax,
                SolidarityTax = solidarityTax,
                SocialSecurityTax = Math.Min(grossPay, 84600m) * 0.186m, // Pension insurance
                UnemploymentTax = Math.Min(grossPay, 84600m) * 0.024m,
                HealthInsurance = Math.Min(grossPay, 58050m) * 0.146m,
                TotalTax = incomeTax + churchTax + solidarityTax + 
                          Math.Min(grossPay, 84600m) * 0.21m + 
                          Math.Min(grossPay, 58050m) * 0.146m,
                TaxJurisdiction = "Germany"
            };
        }

        public async Task<WorkingTimeRules> GetWorkingTimeRulesAsync()
        {
            return new WorkingTimeRules
            {
                MaxDailyHours = 8, // Can be extended to 10 with compensation
                MaxWeeklyHours = 48,
                StandardWeeklyHours = 40,
                MaxConsecutiveDays = 6,
                MinRestBetweenShifts = TimeSpan.FromHours(11),
                MinDailyRest = TimeSpan.FromHours(11),
                MaxShiftLength = TimeSpan.FromHours(10),
                OvertimeRules = new List<OvertimeRule>
                {
                    new() { FromHour = 40, ToHour = null, Rate = 1.25m }
                }
            };
        }
    }
}
```

### 7.2 GDPR Compliance Implementation

```csharp
namespace DriveOps.HumanResources.Compliance.GDPR
{
    public class GDPRDataProcessor : IGDPRDataProcessor
    {
        private readonly IDataClassificationService _dataClassification;
        private readonly IEncryptionService _encryption;
        private readonly IAuditService _audit;

        public async Task<GDPRComplianceReport> ProcessDataSubjectRightsAsync(DataSubjectRequest request)
        {
            var report = new GDPRComplianceReport();

            switch (request.Type)
            {
                case DataSubjectRightType.Access:
                    report = await ProcessAccessRequestAsync(request);
                    break;

                case DataSubjectRightType.Rectification:
                    report = await ProcessRectificationAsync(request);
                    break;

                case DataSubjectRightType.Erasure:
                    report = await ProcessErasureAsync(request);
                    break;

                case DataSubjectRightType.Portability:
                    report = await ProcessPortabilityAsync(request);
                    break;

                case DataSubjectRightType.Restriction:
                    report = await ProcessRestrictionAsync(request);
                    break;
            }

            await _audit.LogGDPRActivityAsync(request, report);
            return report;
        }

        private async Task<GDPRComplianceReport> ProcessAccessRequestAsync(DataSubjectRequest request)
        {
            var employee = await GetEmployeeByEmailAsync(request.Email);
            if (employee == null)
                return GDPRComplianceReport.NotFound();

            var personalData = await CollectAllPersonalDataAsync(employee.Id);
            var dataPackage = await CreateDataPackageAsync(personalData);

            return new GDPRComplianceReport
            {
                RequestType = DataSubjectRightType.Access,
                Status = ProcessingStatus.Completed,
                DataPackage = dataPackage,
                ProcessingTime = DateTime.UtcNow
            };
        }

        private async Task<PersonalDataPackage> CollectAllPersonalDataAsync(EmployeeId employeeId)
        {
            return new PersonalDataPackage
            {
                BasicInfo = await GetBasicEmployeeInfoAsync(employeeId),
                ContactInfo = await GetContactInfoAsync(employeeId),
                EmergencyContacts = await GetEmergencyContactsAsync(employeeId),
                BankingInfo = await GetBankingInfoAsync(employeeId), // Anonymized
                PayrollHistory = await GetPayrollHistoryAsync(employeeId),
                TimeRecords = await GetTimeRecordsAsync(employeeId),
                PerformanceReviews = await GetPerformanceReviewsAsync(employeeId),
                TrainingRecords = await GetTrainingRecordsAsync(employeeId),
                Documents = await GetDocumentMetadataAsync(employeeId), // No actual documents
                AuditLogs = await GetPersonalAuditLogsAsync(employeeId)
            };
        }

        private async Task<GDPRComplianceReport> ProcessErasureAsync(DataSubjectRequest request)
        {
            var employee = await GetEmployeeByEmailAsync(request.Email);
            if (employee == null)
                return GDPRComplianceReport.NotFound();

            // Check legal bases for retention
            var retentionCheck = await CheckRetentionRequirementsAsync(employee.Id);
            if (retentionCheck.HasLegalObligations)
            {
                // Anonymize instead of delete
                await AnonymizeEmployeeDataAsync(employee.Id);
                return new GDPRComplianceReport
                {
                    RequestType = DataSubjectRightType.Erasure,
                    Status = ProcessingStatus.PartiallyCompleted,
                    Notes = "Data anonymized due to legal retention requirements",
                    RetentionReasons = retentionCheck.Obligations
                };
            }

            // Full deletion possible
            await DeleteEmployeeDataAsync(employee.Id);
            return new GDPRComplianceReport
            {
                RequestType = DataSubjectRightType.Erasure,
                Status = ProcessingStatus.Completed,
                Notes = "All personal data has been deleted"
            };
        }
    }

    public class DataRetentionService : IDataRetentionService
    {
        private readonly Dictionary<string, RetentionPolicy> _retentionPolicies;

        public DataRetentionService()
        {
            _retentionPolicies = new Dictionary<string, RetentionPolicy>
            {
                ["PayrollRecords"] = new RetentionPolicy
                {
                    RetentionPeriod = TimeSpan.FromDays(365 * 7), // 7 years for tax purposes
                    LegalBasis = "Tax law compliance",
                    AutoDelete = true
                },
                ["TimeRecords"] = new RetentionPolicy
                {
                    RetentionPeriod = TimeSpan.FromDays(365 * 5), // 5 years for labor law
                    LegalBasis = "Labor law compliance",
                    AutoDelete = true
                },
                ["MedicalRecords"] = new RetentionPolicy
                {
                    RetentionPeriod = TimeSpan.FromDays(365 * 40), // 40 years for occupational health
                    LegalBasis = "Occupational health regulations",
                    AutoDelete = false, // Manual review required
                    RequiresManualReview = true
                },
                ["PerformanceReviews"] = new RetentionPolicy
                {
                    RetentionPeriod = TimeSpan.FromDays(365 * 3), // 3 years after employment ends
                    LegalBasis = "Employment law",
                    AutoDelete = true
                }
            };
        }

        public async Task ProcessScheduledRetentionAsync()
        {
            foreach (var policy in _retentionPolicies.Values)
            {
                if (policy.AutoDelete)
                {
                    await ProcessAutomaticDeletionAsync(policy);
                }
                else if (policy.RequiresManualReview)
                {
                    await ScheduleManualReviewAsync(policy);
                }
            }
        }
    }
}
```

---

## 8. Employee Experience Features

### 8.1 Employee Self-Service Portal

#### Personal Dashboard
```typescript
// Angular component for employee dashboard
@Component({
  selector: 'app-employee-dashboard',
  template: `
    <div class="dashboard-container">
      <mat-card class="welcome-card">
        <mat-card-header>
          <mat-card-title>Welcome back, {{ employee?.firstName }}!</mat-card-title>
          <mat-card-subtitle>{{ employee?.position }} - {{ employee?.department }}</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <div class="quick-stats">
            <div class="stat-item">
              <span class="stat-value">{{ timeOffBalance }}</span>
              <span class="stat-label">Days Available</span>
            </div>
            <div class="stat-item">
              <span class="stat-value">{{ hoursThisWeek }}</span>
              <span class="stat-label">Hours This Week</span>
            </div>
            <div class="stat-item">
              <span class="stat-value">{{ pendingTasks }}</span>
              <span class="stat-label">Pending Tasks</span>
            </div>
          </div>
        </mat-card-content>
      </mat-card>

      <div class="action-cards">
        <mat-card class="action-card" (click)="requestTimeOff()">
          <mat-icon>event</mat-icon>
          <span>Request Time Off</span>
        </mat-card>
        
        <mat-card class="action-card" (click)="clockInOut()">
          <mat-icon>schedule</mat-icon>
          <span>Clock In/Out</span>
        </mat-card>
        
        <mat-card class="action-card" (click)="viewPayslips()">
          <mat-icon>receipt</mat-icon>
          <span>View Payslips</span>
        </mat-card>
        
        <mat-card class="action-card" (click)="updateProfile()">
          <mat-icon>person</mat-icon>
          <span>Update Profile</span>
        </mat-card>
      </div>

      <mat-card class="recent-activity">
        <mat-card-header>
          <mat-card-title>Recent Activity</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <mat-list>
            <mat-list-item *ngFor="let activity of recentActivities">
              <mat-icon matListIcon>{{ activity.icon }}</mat-icon>
              <h4 matLine>{{ activity.title }}</h4>
              <p matLine>{{ activity.description }}</p>
              <span class="activity-date">{{ activity.date | date:'short' }}</span>
            </mat-list-item>
          </mat-list>
        </mat-card-content>
      </mat-card>
    </div>
  `
})
export class EmployeeDashboardComponent implements OnInit {
  employee: EmployeeDto;
  timeOffBalance: number;
  hoursThisWeek: number;
  pendingTasks: number;
  recentActivities: ActivityDto[];

  constructor(
    private employeeService: EmployeeService,
    private timeTrackingService: TimeTrackingService,
    private router: Router,
    private dialog: MatDialog
  ) {}

  async ngOnInit() {
    await this.loadDashboardData();
  }

  private async loadDashboardData() {
    const dashboard = await this.employeeService.getDashboard();
    this.employee = dashboard.employee;
    this.timeOffBalance = dashboard.timeOffBalance;
    this.hoursThisWeek = dashboard.hoursThisWeek;
    this.pendingTasks = dashboard.pendingTasks;
    this.recentActivities = dashboard.recentActivities;
  }

  requestTimeOff() {
    this.dialog.open(TimeOffRequestDialogComponent, {
      width: '600px',
      data: { employeeId: this.employee.id }
    });
  }

  async clockInOut() {
    const currentStatus = await this.timeTrackingService.getCurrentStatus();
    
    if (currentStatus.isClockedIn) {
      await this.timeTrackingService.clockOut();
    } else {
      await this.timeTrackingService.clockIn();
    }
    
    await this.loadDashboardData();
  }
}
```

### 8.2 Mobile Application Features

#### React Native Time Tracking App
```typescript
// React Native component for mobile time tracking
import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, Alert } from 'react-native';
import { useLocation } from '@react-native-community/geolocation';
import { launchCamera } from 'react-native-image-picker';

const TimeTrackingScreen: React.FC = () => {
  const [isClockedIn, setIsClockedIn] = useState(false);
  const [currentLocation, setCurrentLocation] = useState<Location | null>(null);
  const [clockedInTime, setClockedInTime] = useState<Date | null>(null);

  useEffect(() => {
    checkCurrentStatus();
    getCurrentLocation();
  }, []);

  const checkCurrentStatus = async () => {
    try {
      const status = await timeTrackingService.getCurrentStatus();
      setIsClockedIn(status.isClockedIn);
      setClockedInTime(status.clockedInTime);
    } catch (error) {
      Alert.alert('Error', 'Failed to check current status');
    }
  };

  const getCurrentLocation = async () => {
    try {
      const location = await new Promise<Location>((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(
          position => resolve(position),
          error => reject(error),
          { enableHighAccuracy: true, timeout: 15000, maximumAge: 10000 }
        );
      });
      setCurrentLocation(location);
    } catch (error) {
      Alert.alert('Warning', 'Location access is required for time tracking');
    }
  };

  const takePhoto = async (): Promise<string | null> => {
    return new Promise((resolve) => {
      launchCamera({
        mediaType: 'photo',
        quality: 0.8,
        includeBase64: true
      }, (response) => {
        if (response.assets && response.assets[0]) {
          resolve(response.assets[0].base64 || null);
        } else {
          resolve(null);
        }
      });
    });
  };

  const handleClockInOut = async () => {
    try {
      if (!currentLocation) {
        Alert.alert('Error', 'Location is required for time tracking');
        return;
      }

      const photo = await takePhoto();
      
      if (isClockedIn) {
        await timeTrackingService.clockOut({
          location: currentLocation,
          photo,
          timestamp: new Date()
        });
        
        setIsClockedIn(false);
        setClockedInTime(null);
        Alert.alert('Success', 'Clocked out successfully');
      } else {
        await timeTrackingService.clockIn({
          location: currentLocation,
          photo,
          timestamp: new Date()
        });
        
        setIsClockedIn(true);
        setClockedInTime(new Date());
        Alert.alert('Success', 'Clocked in successfully');
      }
    } catch (error) {
      Alert.alert('Error', 'Failed to process clock in/out');
    }
  };

  return (
    <View style={styles.container}>
      <View style={styles.statusCard}>
        <Text style={styles.statusTitle}>
          {isClockedIn ? 'Currently Clocked In' : 'Currently Clocked Out'}
        </Text>
        {clockedInTime && (
          <Text style={styles.statusTime}>
            Since: {clockedInTime.toLocaleTimeString()}
          </Text>
        )}
      </View>

      <TouchableOpacity
        style={[styles.actionButton, isClockedIn ? styles.clockOutButton : styles.clockInButton]}
        onPress={handleClockInOut}
      >
        <Text style={styles.buttonText}>
          {isClockedIn ? 'Clock Out' : 'Clock In'}
        </Text>
      </TouchableOpacity>

      <View style={styles.summaryCard}>
        <Text style={styles.summaryTitle}>Today's Summary</Text>
        <View style={styles.summaryItem}>
          <Text>Hours Worked:</Text>
          <Text style={styles.summaryValue}>7.5h</Text>
        </View>
        <View style={styles.summaryItem}>
          <Text>Break Time:</Text>
          <Text style={styles.summaryValue}>1.0h</Text>
        </View>
        <View style={styles.summaryItem}>
          <Text>Overtime:</Text>
          <Text style={styles.summaryValue}>0.5h</Text>
        </View>
      </View>
    </View>
  );
};
```

### 8.3 Manager Dashboard

#### Team Management Interface
```vue
<!-- Vue.js component for manager dashboard -->
<template>
  <div class="manager-dashboard">
    <div class="dashboard-header">
      <h1>Team Dashboard</h1>
      <div class="team-stats">
        <stat-card 
          title="Team Members" 
          :value="teamStats.totalMembers" 
          icon="people"
        />
        <stat-card 
          title="Present Today" 
          :value="teamStats.presentToday" 
          icon="check-circle"
        />
        <stat-card 
          title="On Leave" 
          :value="teamStats.onLeave" 
          icon="calendar-x"
        />
        <stat-card 
          title="Pending Approvals" 
          :value="pendingApprovals.length" 
          icon="clock"
        />
      </div>
    </div>

    <div class="dashboard-content">
      <div class="pending-approvals">
        <h2>Pending Approvals</h2>
        <div class="approval-list">
          <approval-card
            v-for="approval in pendingApprovals"
            :key="approval.id"
            :approval="approval"
            @approve="handleApproval"
            @reject="handleRejection"
          />
        </div>
      </div>

      <div class="team-attendance">
        <h2>Team Attendance</h2>
        <attendance-grid 
          :employees="teamMembers"
          :date="selectedDate"
          @date-change="handleDateChange"
        />
      </div>

      <div class="performance-overview">
        <h2>Performance Overview</h2>
        <performance-chart 
          :data="performanceData"
          :period="selectedPeriod"
        />
      </div>

      <div class="training-status">
        <h2>Training Status</h2>
        <training-progress-table 
          :employees="teamMembers"
          :mandatory-training="mandatoryTraining"
        />
      </div>
    </div>
  </div>
</template>

<script lang="ts">
import { defineComponent, ref, onMounted, computed } from 'vue';
import { useManagerService } from '@/services/managerService';

export default defineComponent({
  name: 'ManagerDashboard',
  setup() {
    const managerService = useManagerService();
    
    const teamStats = ref({
      totalMembers: 0,
      presentToday: 0,
      onLeave: 0
    });

    const teamMembers = ref<Employee[]>([]);
    const pendingApprovals = ref<Approval[]>([]);
    const performanceData = ref<PerformanceData[]>([]);
    const mandatoryTraining = ref<TrainingProgram[]>([]);
    const selectedDate = ref(new Date());
    const selectedPeriod = ref('month');

    onMounted(async () => {
      await loadDashboardData();
    });

    const loadDashboardData = async () => {
      try {
        const dashboard = await managerService.getManagerDashboard();
        teamStats.value = dashboard.teamStats;
        teamMembers.value = dashboard.teamMembers;
        pendingApprovals.value = dashboard.pendingApprovals;
        performanceData.value = dashboard.performanceData;
        mandatoryTraining.value = dashboard.mandatoryTraining;
      } catch (error) {
        console.error('Failed to load dashboard data:', error);
      }
    };

    const handleApproval = async (approvalId: string, decision: 'approve' | 'reject', notes?: string) => {
      try {
        await managerService.processApproval(approvalId, decision, notes);
        await loadDashboardData(); // Refresh data
      } catch (error) {
        console.error('Failed to process approval:', error);
      }
    };

    const handleDateChange = (newDate: Date) => {
      selectedDate.value = newDate;
      // Reload attendance data for new date
    };

    return {
      teamStats,
      teamMembers,
      pendingApprovals,
      performanceData,
      mandatoryTraining,
      selectedDate,
      selectedPeriod,
      handleApproval,
      handleDateChange
    };
  }
});
</script>
```

---

## 9. Advanced Analytics and Reporting

### 9.1 HR Analytics Engine

```csharp
namespace DriveOps.HumanResources.Analytics
{
    public class HRAnalyticsEngine : IHRAnalyticsEngine
    {
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IPayrollRepository _payrollRepository;
        private readonly ITimeTrackingRepository _timeTrackingRepository;
        private readonly IPerformanceRepository _performanceRepository;

        public async Task<WorkforceAnalytics> GenerateWorkforceAnalyticsAsync(
            TenantId tenantId, 
            AnalyticsPeriod period)
        {
            var analytics = new WorkforceAnalytics();

            // Employee demographics and distribution
            analytics.Demographics = await CalculateDemographicsAsync(tenantId, period);
            
            // Turnover analysis
            analytics.TurnoverAnalysis = await CalculateTurnoverAsync(tenantId, period);
            
            // Performance metrics
            analytics.PerformanceMetrics = await CalculatePerformanceMetricsAsync(tenantId, period);
            
            // Cost analysis
            analytics.CostAnalysis = await CalculateCostAnalysisAsync(tenantId, period);
            
            // Productivity metrics
            analytics.ProductivityMetrics = await CalculateProductivityAsync(tenantId, period);
            
            // Predictive insights
            analytics.PredictiveInsights = await GeneratePredictiveInsightsAsync(tenantId, analytics);

            return analytics;
        }

        private async Task<TurnoverAnalysis> CalculateTurnoverAsync(TenantId tenantId, AnalyticsPeriod period)
        {
            var startDate = period.StartDate;
            var endDate = period.EndDate;
            
            var employees = await _employeeRepository.GetEmployeesByPeriodAsync(tenantId, startDate, endDate);
            var terminatedEmployees = employees.Where(e => e.TerminationDate.HasValue && 
                e.TerminationDate >= startDate && e.TerminationDate <= endDate).ToList();
            
            var avgEmployeeCount = await CalculateAverageEmployeeCountAsync(tenantId, period);
            var turnoverRate = (decimal)terminatedEmployees.Count / avgEmployeeCount * 100;

            // Calculate turnover by department
            var departmentTurnover = terminatedEmployees
                .GroupBy(e => e.DepartmentId)
                .Select(g => new DepartmentTurnover
                {
                    DepartmentId = g.Key,
                    TurnoverCount = g.Count(),
                    TurnoverRate = CalculateDepartmentTurnoverRate(g.Key, g.Count(), period)
                }).ToList();

            // Analyze turnover reasons
            var turnoverReasons = terminatedEmployees
                .GroupBy(e => e.TerminationReason)
                .Select(g => new TurnoverReason
                {
                    Reason = g.Key,
                    Count = g.Count(),
                    Percentage = (decimal)g.Count() / terminatedEmployees.Count * 100
                }).ToList();

            return new TurnoverAnalysis
            {
                OverallTurnoverRate = turnoverRate,
                DepartmentTurnover = departmentTurnover,
                TurnoverReasons = turnoverReasons,
                TurnoverTrend = await CalculateTurnoverTrendAsync(tenantId, period),
                PredictedRisk = await PredictTurnoverRiskAsync(tenantId)
            };
        }

        private async Task<List<TurnoverRiskEmployee>> PredictTurnoverRiskAsync(TenantId tenantId)
        {
            var employees = await _employeeRepository.GetActiveEmployeesAsync(tenantId);
            var riskEmployees = new List<TurnoverRiskEmployee>();

            foreach (var employee in employees)
            {
                var riskScore = await CalculateIndividualTurnoverRiskAsync(employee);
                
                if (riskScore >= 0.7m) // High risk threshold
                {
                    riskEmployees.Add(new TurnoverRiskEmployee
                    {
                        EmployeeId = employee.Id,
                        EmployeeName = $"{employee.PersonalInfo.FirstName} {employee.PersonalInfo.LastName}",
                        RiskScore = riskScore,
                        RiskFactors = await IdentifyRiskFactorsAsync(employee),
                        RecommendedActions = await GetRetentionRecommendationsAsync(employee, riskScore)
                    });
                }
            }

            return riskEmployees.OrderByDescending(r => r.RiskScore).ToList();
        }

        private async Task<decimal> CalculateIndividualTurnoverRiskAsync(Employee employee)
        {
            var riskFactors = new List<RiskFactor>();

            // Tenure factor
            var tenure = DateTime.UtcNow - employee.HireDate;
            if (tenure.TotalDays < 90) // New employees higher risk
                riskFactors.Add(new RiskFactor("NewEmployee", 0.3m));

            // Performance factor
            var latestReview = await _performanceRepository.GetLatestReviewAsync(employee.Id);
            if (latestReview?.OverallRating < 3.0m)
                riskFactors.Add(new RiskFactor("LowPerformance", 0.4m));

            // Compensation factor
            var marketRate = await GetMarketRateAsync(employee.PositionId, employee.PersonalInfo.Country);
            var compensationRatio = employee.Contract.BaseSalary / marketRate;
            if (compensationRatio < 0.9m)
                riskFactors.Add(new RiskFactor("BelowMarketCompensation", 0.3m));

            // Manager relationship
            var managerTurnover = await GetManagerTurnoverAsync(employee.DirectManagerId);
            if (managerTurnover > 2) // Manager changed more than twice
                riskFactors.Add(new RiskFactor("ManagerInstability", 0.2m));

            // Career development
            var promotions = await GetPromotionHistoryAsync(employee.Id);
            if (tenure.TotalDays > 730 && !promotions.Any()) // No promotion in 2+ years
                riskFactors.Add(new RiskFactor("LimitedCareerGrowth", 0.25m));

            // Calculate weighted risk score
            return Math.Min(1.0m, riskFactors.Sum(rf => rf.Weight));
        }

        public async Task<PayrollAnalytics> GeneratePayrollAnalyticsAsync(TenantId tenantId, AnalyticsPeriod period)
        {
            var payrollEntries = await _payrollRepository.GetEntriesByPeriodAsync(tenantId, period);
            
            return new PayrollAnalytics
            {
                TotalPayrollCost = payrollEntries.Sum(p => p.GrossPay),
                AverageGrossPay = payrollEntries.Average(p => p.GrossPay),
                TotalTaxes = payrollEntries.Sum(p => p.TaxCalculation.TotalTax),
                PayrollCostTrend = await CalculatePayrollTrendAsync(tenantId, period),
                CostByDepartment = await CalculateCostByDepartmentAsync(payrollEntries),
                OvertimeCosts = payrollEntries.Sum(p => p.OvertimePay),
                BonusDistribution = await AnalyzeBonusDistributionAsync(payrollEntries),
                CompensationBenchmark = await CompareWithMarketRatesAsync(tenantId, payrollEntries)
            };
        }
    }
}
```

---

## 10. Conclusion

The DriveOps RH/PERSONNEL module represents a comprehensive, industry-specific human resources management solution that seamlessly extends the Core Users module while providing enterprise-grade functionality tailored to the automotive industry.

### 10.1 Key Architectural Strengths

- **Domain-Driven Design**: Clean architecture with well-defined bounded contexts
- **Multi-Tenant Architecture**: Complete tenant isolation with scalable data management
- **CQRS Implementation**: Efficient command and query segregation for complex HR operations
- **Event Sourcing**: Reliable audit trails for critical payroll and compliance operations
- **Microservices Integration**: Seamless integration with all DriveOps business modules

### 10.2 Business Value Proposition

- **Industry Specialization**: Purpose-built for automotive businesses with specific compliance needs
- **Scalable Revenue Model**: Per-employee pricing that grows with client success
- **Compliance Automation**: Reduces legal risks and administrative burden
- **Operational Efficiency**: Automates time-consuming HR processes
- **Data-Driven Insights**: Advanced analytics for strategic workforce decisions

### 10.3 Technical Innovation

- **Multi-Country Compliance**: Automated tax calculations and labor law compliance across jurisdictions
- **Mobile-First Design**: Native mobile applications for field workers and remote employees
- **AI-Powered Analytics**: Predictive modeling for turnover risk and performance insights
- **GDPR Compliance**: Built-in privacy protection and data subject rights management
- **Real-Time Processing**: Live payroll calculations and instant compliance monitoring

### 10.4 Market Positioning

The RH/PERSONNEL module positions DriveOps as the leading HR solution for the automotive industry, offering:

- **Competitive Differentiation**: Industry-specific features unavailable in generic HR solutions
- **High Customer Lifetime Value**: Sticky revenue with high switching costs
- **Expansion Opportunities**: Natural upselling path as businesses grow
- **Market Leadership**: First-mover advantage in automotive HR technology

### 10.5 Future Roadmap

Planned enhancements include:
- **AI-Powered Recruitment**: Automated candidate screening and matching
- **Advanced Workforce Planning**: Predictive staffing models and capacity planning
- **IoT Integration**: Wearable device integration for safety and productivity monitoring
- **Blockchain Verification**: Immutable credential and certification verification
- **Global Expansion**: Support for additional countries and regulatory frameworks

### 10.6 Implementation Success Factors

For successful deployment, organizations should focus on:
- **Change Management**: Proper training and adoption strategies
- **Data Migration**: Careful planning for existing HR data transfer
- **Compliance Validation**: Thorough testing of local regulatory requirements
- **Integration Testing**: Validation of connections with existing business systems
- **Performance Optimization**: Ensuring system responsiveness under production loads

The RH/PERSONNEL module establishes DriveOps as a comprehensive business platform capable of managing the complete employee lifecycle while maintaining the highest standards of security, compliance, and operational efficiency.

---

*Document created: 2024-12-19*  
*Last updated: 2024-12-19*  
*Version: 1.0*

