# Development Workflow

This guide outlines the development processes, coding standards, and contribution guidelines for the DriveOps platform.

---

## 🎯 Overview

DriveOps follows industry best practices for enterprise software development with:
- **Git Flow** workflow for structured branching
- **Domain-Driven Design (DDD)** with clean architecture
- **Test-Driven Development (TDD)** principles
- **Continuous Integration/Continuous Deployment (CI/CD)**
- **Code Review** processes for quality assurance

---

## 🌳 Git Workflow

### Branch Strategy

We use a modified Git Flow strategy optimized for continuous deployment:

```
main                    # Production-ready code
├── develop            # Integration branch for features
├── feature/xxx        # Feature development branches
├── hotfix/xxx         # Critical production fixes
└── release/vX.Y.Z     # Release preparation branches
```

#### Branch Types

| Branch Type | Purpose | Naming Convention | Merges To |
|-------------|---------|-------------------|-----------|
| **main** | Production releases | `main` | - |
| **develop** | Integration branch | `develop` | `main` |
| **feature** | New features | `feature/JIRA-123-short-description` | `develop` |
| **hotfix** | Critical fixes | `hotfix/JIRA-456-fix-description` | `main` + `develop` |
| **release** | Release preparation | `release/v1.2.0` | `main` + `develop` |

### Workflow Steps

#### 1. Start New Feature
```bash
# Update develop branch
git checkout develop
git pull origin develop

# Create feature branch
git checkout -b feature/GARAGE-123-intervention-scheduling

# Push branch to remote
git push -u origin feature/GARAGE-123-intervention-scheduling
```

#### 2. Development Process
```bash
# Make changes following our coding standards
# Commit frequently with descriptive messages

# Stage changes
git add .

# Commit with conventional commit format
git commit -m "feat(garage): add intervention scheduling logic

- Implement time slot validation
- Add mechanic availability check
- Include business rule validation

Closes GARAGE-123"

# Push changes
git push origin feature/GARAGE-123-intervention-scheduling
```

#### 3. Create Pull Request
1. **Push your feature branch** to GitHub
2. **Open Pull Request** from feature branch to `develop`
3. **Fill PR template** with detailed description
4. **Assign reviewers** (at least 2 team members)
5. **Link related issues** and documentation

#### 4. Code Review Process
- **Automated checks** must pass (build, tests, linting)
- **Manual review** by at least 2 team members
- **Address feedback** and update branch
- **Approval required** before merge

#### 5. Merge and Cleanup
```bash
# After PR approval, merge via GitHub (squash and merge)
# Delete feature branch
git branch -d feature/GARAGE-123-intervention-scheduling
git push origin --delete feature/GARAGE-123-intervention-scheduling

# Update local develop
git checkout develop
git pull origin develop
```

---

## 📝 Commit Message Standards

We follow [Conventional Commits](https://www.conventionalcommits.org/) specification:

### Format
```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Types
| Type | Description | Example |
|------|-------------|---------|
| **feat** | New feature | `feat(garage): add intervention scheduling` |
| **fix** | Bug fix | `fix(auth): resolve token expiration issue` |
| **docs** | Documentation | `docs: update API documentation` |
| **style** | Code formatting | `style: apply consistent indentation` |
| **refactor** | Code refactoring | `refactor(vehicles): simplify search logic` |
| **test** | Add/update tests | `test(garage): add intervention validation tests` |
| **chore** | Maintenance | `chore: update dependencies` |
| **perf** | Performance improvement | `perf(db): optimize vehicle search query` |

### Scopes
Common scopes for DriveOps:
- `garage`, `breakdown`, `rental`, `fleet` - Business modules
- `core`, `shared`, `auth` - Core system components
- `api`, `web`, `mobile` - Application layers
- `db`, `cache`, `queue` - Infrastructure
- `ci`, `deploy`, `config` - DevOps

### Examples
```bash
# Feature with scope
git commit -m "feat(garage): implement intervention scheduling

Add ability to schedule interventions with mechanic assignment.
Includes time slot validation and conflict detection.

Closes GARAGE-123"

# Bug fix
git commit -m "fix(auth): resolve JWT token refresh issue

Fixed issue where tokens were not properly refreshed
leading to unexpected logouts.

Fixes AUTH-456"

# Breaking change
git commit -m "feat(api): update vehicle API response format

BREAKING CHANGE: Vehicle API now returns nested owner object
instead of flat structure.

Migration guide available in docs/migrations/v2.0.md"
```

---

## 🏗️ Architecture Standards

### Domain-Driven Design (DDD)

DriveOps follows DDD principles with clear architectural boundaries:

#### Project Structure
```
DriveOps.{Module}/
├── Domain/                    # Pure business logic
│   ├── Entities/             # Business entities
│   ├── ValueObjects/         # Value objects
│   ├── Aggregates/           # Aggregate roots
│   ├── DomainEvents/         # Domain events
│   ├── Repositories/         # Repository interfaces
│   └── Services/             # Domain services
├── Application/              # Use cases and orchestration
│   ├── Commands/             # CQRS commands
│   ├── Queries/              # CQRS queries
│   ├── Handlers/             # Command/query handlers
│   ├── DTOs/                 # Data transfer objects
│   └── Services/             # Application services
├── Infrastructure/           # External concerns
│   ├── Persistence/          # Database implementations
│   ├── External/             # External service clients
│   └── Messaging/            # Message handling
└── Presentation/             # API and UI
    ├── Controllers/          # REST API controllers
    ├── GraphQL/              # GraphQL resolvers
    └── Components/           # Blazor components
```

#### Entity Design
```csharp
// Follow this pattern for all entities
public class Intervention : AggregateRoot
{
    public InterventionId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    // ... other properties

    private Intervention() { } // EF Core constructor

    public Intervention(TenantId tenantId, VehicleId vehicleId, /*...*/)
    {
        Id = InterventionId.New();
        TenantId = tenantId;
        // ... initialization
        
        AddDomainEvent(new InterventionCreatedEvent(TenantId, Id));
    }

    public void UpdateStatus(InterventionStatus newStatus)
    {
        // Business logic validation
        if (!CanUpdateStatus(newStatus))
            throw new InvalidOperationException("Cannot update status");

        var oldStatus = Status;
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new InterventionStatusChangedEvent(TenantId, Id, oldStatus, newStatus));
    }
}
```

### CQRS Implementation

#### Command Pattern
```csharp
// Commands modify state
public class CreateInterventionCommand : IRequest<Result<InterventionId>>
{
    public TenantId TenantId { get; set; }
    public VehicleId VehicleId { get; set; }
    // ... properties
}

public class CreateInterventionCommandHandler : IRequestHandler<CreateInterventionCommand, Result<InterventionId>>
{
    public async Task<Result<InterventionId>> Handle(CreateInterventionCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate request
        // 2. Create domain entity
        // 3. Persist changes
        // 4. Dispatch events
        // 5. Return result
    }
}
```

#### Query Pattern
```csharp
// Queries return data without side effects
public class GetInterventionsQuery : IRequest<Result<PagedList<InterventionDto>>>
{
    public TenantId TenantId { get; set; }
    public InterventionStatus? Status { get; set; }
    // ... filters
}

public class GetInterventionsQueryHandler : IRequestHandler<GetInterventionsQuery, Result<PagedList<InterventionDto>>>
{
    public async Task<Result<PagedList<InterventionDto>>> Handle(GetInterventionsQuery request, CancellationToken cancellationToken)
    {
        // 1. Build query with filters
        // 2. Execute query
        // 3. Map to DTOs
        // 4. Return paged results
    }
}
```

### Multi-Tenancy Requirements

**Every entity must include TenantId:**
```csharp
public abstract class AggregateRoot
{
    public TenantId TenantId { get; protected set; }
    // ... base properties
}
```

**Every query must filter by tenant:**
```csharp
// Correct
var interventions = await _context.Interventions
    .Where(i => i.TenantId == tenantId)
    .ToListAsync();

// WRONG - missing tenant filter
var interventions = await _context.Interventions.ToListAsync();
```

---

## 🧪 Testing Standards

### Testing Strategy

We maintain comprehensive testing at multiple levels:

#### 1. Unit Tests (70% coverage target)
```csharp
// Test naming: MethodName_Scenario_ExpectedResult
[Fact]
public void CreateIntervention_WithValidData_ShouldCreateInterventionAndRaiseDomainEvent()
{
    // Arrange
    var tenantId = TenantId.New();
    var vehicleId = VehicleId.New();
    var customerId = CustomerId.New();
    var description = "Engine maintenance";
    var scheduledDate = DateTime.UtcNow.AddDays(1);

    // Act
    var intervention = new Intervention(tenantId, vehicleId, customerId, description, scheduledDate);

    // Assert
    intervention.Should().NotBeNull();
    intervention.TenantId.Should().Be(tenantId);
    intervention.Description.Should().Be(description);
    intervention.Status.Should().Be(InterventionStatus.Scheduled);
    intervention.DomainEvents.Should().ContainSingle(e => e is InterventionCreatedEvent);
}
```

#### 2. Integration Tests
```csharp
[Collection("Integration Tests")]
public class InterventionsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task CreateIntervention_WithValidRequest_ShouldReturnCreatedIntervention()
    {
        // Arrange
        var request = new CreateInterventionRequest { /* ... */ };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/interventions", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<InterventionDto>();
        result.Should().NotBeNull();
    }
}
```

#### 3. Test Organization
```
tests/
├── DriveOps.Domain.Tests/           # Domain logic tests
├── DriveOps.Application.Tests/      # Application service tests
├── DriveOps.Infrastructure.Tests/   # Infrastructure tests
├── DriveOps.Api.Tests/             # API integration tests
└── DriveOps.EndToEnd.Tests/        # E2E tests
```

### Test Requirements
- **Minimum 80% code coverage** for new code
- **All public methods** must have unit tests
- **Critical business logic** must have comprehensive test scenarios
- **API endpoints** must have integration tests
- **Database operations** must have integration tests

### Running Tests
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific category
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration

# Run tests for specific module
dotnet test tests/DriveOps.Garage.Tests/
```

---

## 🎨 Code Standards

### C# Conventions

Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions):

#### Naming Conventions
```csharp
// Classes: PascalCase
public class InterventionService { }

// Methods: PascalCase
public async Task<Result> CreateInterventionAsync() { }

// Properties: PascalCase
public string Description { get; set; }

// Private fields: _camelCase
private readonly IRepository _repository;

// Local variables: camelCase
var interventionId = InterventionId.New();

// Constants: PascalCase
public const int MaxInterventionsPerDay = 10;
```

#### Code Organization
```csharp
// Order: fields, constructors, properties, methods
public class InterventionService
{
    // 1. Private fields
    private readonly IInterventionRepository _repository;
    private readonly ILogger<InterventionService> _logger;

    // 2. Constructor
    public InterventionService(IInterventionRepository repository, ILogger<InterventionService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // 3. Public properties
    public bool IsInitialized { get; private set; }

    // 4. Public methods
    public async Task<Result> CreateInterventionAsync(CreateInterventionRequest request)
    {
        // Implementation
    }

    // 5. Private methods
    private bool ValidateRequest(CreateInterventionRequest request)
    {
        // Implementation
    }
}
```

### Database Conventions

#### Entity Configuration
```csharp
public class InterventionConfiguration : IEntityTypeConfiguration<Intervention>
{
    public void Configure(EntityTypeBuilder<Intervention> builder)
    {
        builder.ToTable("garage_interventions");
        
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasConversion(id => id.Value, value => new InterventionId(value));

        builder.Property(i => i.TenantId).IsRequired();
        builder.Property(i => i.Description).IsRequired().HasMaxLength(1000);

        // Multi-tenant index
        builder.HasIndex(i => new { i.TenantId, i.Status, i.ScheduledDate });
    }
}
```

#### Migration Naming
```bash
# Format: {DateTime}_{DescriptiveAction}
# Examples:
20241219_CreateInterventionsTable
20241219_AddMechanicToInterventions
20241219_UpdateInterventionStatusEnum
```

---

## 📋 Pull Request Guidelines

### PR Template
Every PR must include:

```markdown
## Description
Brief description of changes and motivation.

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed

## Checklist
- [ ] Code follows the style guidelines
- [ ] Self-review completed
- [ ] Code is commented, particularly in hard-to-understand areas
- [ ] Documentation updated
- [ ] No new warnings introduced
- [ ] Tests pass locally
```

### Review Criteria

**Automated Checks:**
- [ ] Build passes
- [ ] All tests pass
- [ ] Code coverage meets minimum 80%
- [ ] No linting errors
- [ ] Security scan passes

**Manual Review:**
- [ ] Code follows DDD principles
- [ ] Multi-tenancy properly implemented
- [ ] Business logic is in domain layer
- [ ] Error handling is appropriate
- [ ] Performance considerations addressed
- [ ] Security implications reviewed

### Approval Process
1. **At least 2 approvals** required
2. **All checks must pass** before merge
3. **Address all feedback** before requesting re-review
4. **Use "Squash and Merge"** to maintain clean history

---

## 🔄 GitHub Copilot Integration in Workflow

### Development Cycle with Copilot

#### 1. Planning Phase
```markdown
# Use Copilot Chat for planning
"I need to implement intervention scheduling in the garage module.
What are the key components I should consider for DriveOps architecture?"
```

#### 2. Implementation Phase
```csharp
// Use Copilot for code generation
// Type comment and let Copilot suggest implementation
// Create a CQRS command handler for scheduling garage interventions
public class ScheduleInterventionCommandHandler : IRequestHandler<ScheduleInterventionCommand, Result<InterventionId>>
{
    // Copilot will suggest the implementation
}
```

#### 3. Testing Phase
```csharp
// Use Copilot for test generation
// Generate unit tests for intervention scheduling with edge cases
[Fact]
public void ScheduleIntervention_WithConflictingTime_ShouldReturnError()
{
    // Copilot suggests test implementation
}
```

#### 4. Documentation Phase
```markdown
<!-- Use Copilot for documentation -->
<!-- Generate API documentation for intervention scheduling endpoint -->
```

### Copilot Code Review Checklist
- [ ] **Architecture Compliance**: Copilot suggestions follow DDD patterns
- [ ] **Multi-tenancy**: Generated code includes proper tenant filtering
- [ ] **Error Handling**: Appropriate exception handling and Result patterns
- [ ] **Performance**: Efficient database queries and async patterns
- [ ] **Security**: No security vulnerabilities in generated code

---

## 🚀 Continuous Integration/Deployment

### GitHub Actions Workflow

```yaml
name: CI/CD Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
          
      - name: Restore dependencies
        run: dotnet restore
        
      - name: Build
        run: dotnet build --no-restore
        
      - name: Test
        run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
        
      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v3
```

### Deployment Strategy

#### Development Environment
- **Automatic deployment** on merge to `develop`
- **Runs integration tests** in staging environment
- **Accessible to development team** for testing

#### Production Environment
- **Manual deployment** from `main` branch
- **Requires approval** from tech lead
- **Blue-green deployment** for zero downtime
- **Automatic rollback** on health check failure

---

## 📖 Documentation Standards

### Code Documentation
```csharp
/// <summary>
/// Schedules an intervention for a vehicle with the specified mechanic.
/// </summary>
/// <param name="tenantId">The tenant identifier for multi-tenant isolation</param>
/// <param name="request">The intervention scheduling request containing vehicle, mechanic, and time details</param>
/// <returns>A Result containing the InterventionId if successful, or error details if failed</returns>
/// <exception cref="ArgumentNullException">Thrown when tenantId or request is null</exception>
/// <exception cref="ValidationException">Thrown when request validation fails</exception>
public async Task<Result<InterventionId>> ScheduleInterventionAsync(TenantId tenantId, ScheduleInterventionRequest request)
{
    // Implementation
}
```

### API Documentation
- **OpenAPI/Swagger** documentation for all endpoints
- **Example requests/responses** for complex operations
- **Error code documentation** with troubleshooting guide
- **Authentication requirements** clearly specified

### Architecture Documentation
- **Keep documentation current** with code changes
- **Update diagrams** when architecture changes
- **Document breaking changes** with migration guides
- **Include performance characteristics** for critical operations

---

## ✅ Definition of Done

A feature is considered complete when:

### Code Quality
- [ ] Code follows established patterns and conventions
- [ ] All automated tests pass
- [ ] Code coverage meets minimum threshold (80%)
- [ ] No critical security vulnerabilities
- [ ] Performance requirements met

### Documentation
- [ ] Code is properly documented with XML comments
- [ ] API documentation updated (if applicable)
- [ ] Architecture documentation updated (if applicable)
- [ ] Migration guide provided (for breaking changes)

### Testing
- [ ] Unit tests written and passing
- [ ] Integration tests written and passing
- [ ] Manual testing completed
- [ ] Edge cases identified and tested

### Review
- [ ] Code review completed with at least 2 approvals
- [ ] All feedback addressed
- [ ] Technical debt assessed and documented
- [ ] Security implications reviewed

### Deployment
- [ ] Feature deployed to development environment
- [ ] Integration tests pass in staging
- [ ] Ready for production deployment
- [ ] Monitoring and alerting configured

---

## 🛠️ Tools and Resources

### Development Tools
- **IDE**: Visual Studio 2022, VS Code with C# extension
- **Database**: Azure Data Studio, pgAdmin, MongoDB Compass
- **API Testing**: Postman, Insomnia, Thunder Client
- **Code Quality**: SonarQube, CodeClimate
- **Performance**: dotMemory, PerfView, Application Insights

### Recommended Extensions
- **Visual Studio Code**:
  - C# for Visual Studio Code
  - GitHub Copilot
  - Thunder Client
  - GitLens
  - PostgreSQL extension

- **Visual Studio 2022**:
  - GitHub Copilot
  - Productivity Power Tools
  - SonarLint
  - Web Essentials

### Learning Resources
- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Domain-Driven Design Reference](https://domainlanguage.com/ddd/reference/)
- [Clean Architecture Guide](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [DriveOps Architecture Overview](02-ARCHITECTURE-OVERVIEW.md)

---

**Ready to contribute to DriveOps? Follow this workflow to ensure high-quality, maintainable code! 🚀**

---

*Last updated: 2024-12-19*  
*Version: 1.0.0*