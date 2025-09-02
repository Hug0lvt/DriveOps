# GitHub Copilot Integration Guide for DriveOps

This comprehensive guide shows how to maximize GitHub Copilot's effectiveness when working with the DriveOps automotive SaaS platform. DriveOps is architected with DDD, CQRS, and multi-tenancy patterns that benefit greatly from Copilot's AI assistance.

---

## 🚀 Quick Setup

### 1. Repository Configuration
Configure Copilot to understand DriveOps' specific patterns:

```bash
# Clone and setup the repository
git clone https://github.com/Hug0lvt/DriveOps.git
cd DriveOps

# Open key architecture files to prime Copilot context
code docs/10-CORE-ARCHITECTURE.md
code docs/02-ARCHITECTURE-OVERVIEW.md
code src/DriveOps.Shared/Domain/  # (when available)
```

### 2. IDE Configuration
**Visual Studio Code:**
```json
// .vscode/settings.json
{
  "github.copilot.enable": {
    "*": true,
    "plaintext": false,
    "markdown": true,
    "scminput": false
  },
  "github.copilot.inlineSuggest.enable": true,
  "editor.inlineSuggest.enabled": true
}
```

**Visual Studio:**
- Install GitHub Copilot extension
- Enable Copilot in Tools → Options → GitHub Copilot

### 3. Context Optimization
Before starting development, open these files to provide Copilot with DriveOps context:
- `docs/02-ARCHITECTURE-OVERVIEW.md` - System architecture
- `docs/10-CORE-ARCHITECTURE.md` - Core modules
- `docs/21-GARAGE-MODULE.md` - Sample business module
- Any existing Domain models in `/src` folder

---

## 🏗️ DriveOps Architecture Patterns

### Domain-Driven Design (DDD) Templates

#### Entity Creation
```csharp
// Copilot prompt: "Create a DDD entity for Vehicle with DriveOps multi-tenant pattern"
// Generated suggestion:
namespace DriveOps.Vehicles.Domain.Entities
{
    public class Vehicle : AggregateRoot
    {
        public VehicleId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string VIN { get; private set; }
        public string Make { get; private set; }
        public string Model { get; private set; }
        public int Year { get; private set; }
        public VehicleStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private Vehicle() { } // EF Core

        public Vehicle(TenantId tenantId, string vin, string make, string model, int year)
        {
            Id = VehicleId.New();
            TenantId = tenantId;
            VIN = vin;
            Make = make;
            Model = model;
            Year = year;
            Status = VehicleStatus.Active;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            
            AddDomainEvent(new VehicleCreatedEvent(TenantId, Id, VIN));
        }

        public void UpdateStatus(VehicleStatus newStatus)
        {
            if (Status != newStatus)
            {
                var oldStatus = Status;
                Status = newStatus;
                UpdatedAt = DateTime.UtcNow;
                
                AddDomainEvent(new VehicleStatusChangedEvent(TenantId, Id, oldStatus, newStatus));
            }
        }
    }
}
```

#### Value Object Pattern
```csharp
// Copilot prompt: "Create a value object for Money in DriveOps following DDD patterns"
namespace DriveOps.Shared.Domain.ValueObjects
{
    public class Money : ValueObject
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public Money(decimal amount, string currency = "EUR")
        {
            if (amount < 0)
                throw new ArgumentException("Amount cannot be negative", nameof(amount));
            
            Amount = amount;
            Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        }

        public Money Add(Money other)
        {
            if (Currency != other.Currency)
                throw new InvalidOperationException("Cannot add different currencies");
            
            return new Money(Amount + other.Amount, Currency);
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }

        public override string ToString() => $"{Amount:C} {Currency}";
    }
}
```

### CQRS Implementation Templates

#### Command Handler Pattern
```csharp
// Copilot prompt: "Create a CQRS command handler for creating a garage intervention in DriveOps"
namespace DriveOps.Garage.Application.Interventions.Commands
{
    public class CreateInterventionCommand : IRequest<Result<InterventionId>>
    {
        public TenantId TenantId { get; set; }
        public VehicleId VehicleId { get; set; }
        public CustomerId CustomerId { get; set; }
        public string Description { get; set; }
        public DateTime ScheduledDate { get; set; }
        public List<string> RequiredSkills { get; set; } = new();
    }

    public class CreateInterventionCommandHandler : IRequestHandler<CreateInterventionCommand, Result<InterventionId>>
    {
        private readonly IInterventionRepository _interventionRepository;
        private readonly IVehicleRepository _vehicleRepository;
        private readonly IDomainEventDispatcher _eventDispatcher;

        public CreateInterventionCommandHandler(
            IInterventionRepository interventionRepository,
            IVehicleRepository vehicleRepository,
            IDomainEventDispatcher eventDispatcher)
        {
            _interventionRepository = interventionRepository;
            _vehicleRepository = vehicleRepository;
            _eventDispatcher = eventDispatcher;
        }

        public async Task<Result<InterventionId>> Handle(CreateInterventionCommand request, CancellationToken cancellationToken)
        {
            // Validate vehicle exists and belongs to tenant
            var vehicle = await _vehicleRepository.GetByIdAsync(request.TenantId, request.VehicleId);
            if (vehicle == null)
                return Result.Failure<InterventionId>("Vehicle not found");

            // Create intervention aggregate
            var intervention = new Intervention(
                request.TenantId,
                request.VehicleId,
                request.CustomerId,
                request.Description,
                request.ScheduledDate,
                request.RequiredSkills);

            // Persist
            await _interventionRepository.AddAsync(intervention);

            // Dispatch domain events
            await _eventDispatcher.DispatchAsync(intervention.DomainEvents);

            return Result.Success(intervention.Id);
        }
    }
}
```

#### Query Handler Pattern
```csharp
// Copilot prompt: "Create a CQRS query handler for getting garage interventions with filtering in DriveOps"
namespace DriveOps.Garage.Application.Interventions.Queries
{
    public class GetInterventionsQuery : IRequest<Result<PagedList<InterventionDto>>>
    {
        public TenantId TenantId { get; set; }
        public InterventionStatus? Status { get; set; }
        public VehicleId? VehicleId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class GetInterventionsQueryHandler : IRequestHandler<GetInterventionsQuery, Result<PagedList<InterventionDto>>>
    {
        private readonly IInterventionReadRepository _readRepository;
        private readonly IMapper _mapper;

        public GetInterventionsQueryHandler(IInterventionReadRepository readRepository, IMapper mapper)
        {
            _readRepository = readRepository;
            _mapper = mapper;
        }

        public async Task<Result<PagedList<InterventionDto>>> Handle(GetInterventionsQuery request, CancellationToken cancellationToken)
        {
            var query = _readRepository.GetInterventionsQuery(request.TenantId);

            // Apply filters
            if (request.Status.HasValue)
                query = query.Where(i => i.Status == request.Status.Value);

            if (request.VehicleId.HasValue)
                query = query.Where(i => i.VehicleId == request.VehicleId.Value);

            if (request.FromDate.HasValue)
                query = query.Where(i => i.ScheduledDate >= request.FromDate.Value);

            if (request.ToDate.HasValue)
                query = query.Where(i => i.ScheduledDate <= request.ToDate.Value);

            // Order and paginate
            query = query.OrderBy(i => i.ScheduledDate);

            var pagedInterventions = await PagedList<Intervention>.CreateAsync(
                query, request.Page, request.PageSize);

            var dtos = _mapper.Map<List<InterventionDto>>(pagedInterventions.Items);

            return Result.Success(new PagedList<InterventionDto>(
                dtos, pagedInterventions.TotalCount, pagedInterventions.CurrentPage, pagedInterventions.PageSize));
        }
    }
}
```

---

## 🗃️ Database Patterns

### Multi-Tenant Schema Generation
```csharp
// Copilot prompt: "Create PostgreSQL migration for multi-tenant garage interventions table in DriveOps"
namespace DriveOps.Garage.Infrastructure.Migrations
{
    public partial class CreateInterventionsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "garage_interventions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mechanic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    scheduled_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    estimated_duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    actual_duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    total_cost = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garage_interventions", x => x.id);
                    table.ForeignKey(
                        name: "FK_garage_interventions_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Multi-tenant indexes
            migrationBuilder.CreateIndex(
                name: "IX_garage_interventions_tenant_id_status_scheduled_date",
                table: "garage_interventions",
                columns: new[] { "tenant_id", "status", "scheduled_date" });

            migrationBuilder.CreateIndex(
                name: "IX_garage_interventions_tenant_id_vehicle_id",
                table: "garage_interventions",
                columns: new[] { "tenant_id", "vehicle_id" });

            migrationBuilder.CreateIndex(
                name: "IX_garage_interventions_tenant_id_mechanic_id",
                table: "garage_interventions",
                columns: new[] { "tenant_id", "mechanic_id" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "garage_interventions");
        }
    }
}
```

### Repository Pattern with Multi-Tenancy
```csharp
// Copilot prompt: "Create a multi-tenant repository for DriveOps garage interventions with EF Core"
namespace DriveOps.Garage.Infrastructure.Repositories
{
    public class InterventionRepository : IInterventionRepository
    {
        private readonly GarageDbContext _context;

        public InterventionRepository(GarageDbContext context)
        {
            _context = context;
        }

        public async Task<Intervention?> GetByIdAsync(TenantId tenantId, InterventionId id)
        {
            return await _context.Interventions
                .Where(i => i.TenantId == tenantId && i.Id == id)
                .Include(i => i.Items)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Intervention>> GetByVehicleAsync(TenantId tenantId, VehicleId vehicleId)
        {
            return await _context.Interventions
                .Where(i => i.TenantId == tenantId && i.VehicleId == vehicleId)
                .OrderByDescending(i => i.ScheduledDate)
                .ToListAsync();
        }

        public async Task<List<Intervention>> GetScheduledForDateAsync(TenantId tenantId, DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            return await _context.Interventions
                .Where(i => i.TenantId == tenantId 
                    && i.ScheduledDate >= startOfDay 
                    && i.ScheduledDate < endOfDay
                    && i.Status != InterventionStatus.Completed
                    && i.Status != InterventionStatus.Cancelled)
                .OrderBy(i => i.ScheduledDate)
                .ToListAsync();
        }

        public async Task AddAsync(Intervention intervention)
        {
            _context.Interventions.Add(intervention);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Intervention intervention)
        {
            _context.Interventions.Update(intervention);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(TenantId tenantId, InterventionId id)
        {
            var intervention = await GetByIdAsync(tenantId, id);
            if (intervention != null)
            {
                _context.Interventions.Remove(intervention);
                await _context.SaveChangesAsync();
            }
        }
    }
}
```

---

## 🌐 API Development

### REST API Controller
```csharp
// Copilot prompt: "Create a REST API controller for garage interventions in DriveOps with proper validation"
namespace DriveOps.Garage.Api.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId}/garage/interventions")]
    [Authorize]
    public class InterventionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public InterventionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Get paginated list of interventions for a tenant
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedList<InterventionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetInterventions(
            [FromRoute] Guid tenantId,
            [FromQuery] InterventionStatus? status = null,
            [FromQuery] Guid? vehicleId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = new GetInterventionsQuery
            {
                TenantId = new TenantId(tenantId),
                Status = status,
                VehicleId = vehicleId.HasValue ? new VehicleId(vehicleId.Value) : null,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);

            return result.IsSuccess 
                ? Ok(result.Value) 
                : BadRequest(result.Error);
        }

        /// <summary>
        /// Create a new intervention
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(InterventionDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateIntervention(
            [FromRoute] Guid tenantId,
            [FromBody] CreateInterventionRequest request)
        {
            var command = new CreateInterventionCommand
            {
                TenantId = new TenantId(tenantId),
                VehicleId = new VehicleId(request.VehicleId),
                CustomerId = new CustomerId(request.CustomerId),
                Description = request.Description,
                ScheduledDate = request.ScheduledDate,
                RequiredSkills = request.RequiredSkills ?? new List<string>()
            };

            var result = await _mediator.Send(command);

            if (result.IsFailure)
                return BadRequest(result.Error);

            // Get the created intervention
            var getQuery = new GetInterventionByIdQuery 
            { 
                TenantId = new TenantId(tenantId), 
                InterventionId = result.Value 
            };
            
            var interventionResult = await _mediator.Send(getQuery);

            return CreatedAtAction(
                nameof(GetIntervention), 
                new { tenantId, id = result.Value.Value }, 
                interventionResult.Value);
        }

        /// <summary>
        /// Get a specific intervention by ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(InterventionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetIntervention(
            [FromRoute] Guid tenantId,
            [FromRoute] Guid id)
        {
            var query = new GetInterventionByIdQuery
            {
                TenantId = new TenantId(tenantId),
                InterventionId = new InterventionId(id)
            };

            var result = await _mediator.Send(query);

            return result.IsSuccess 
                ? Ok(result.Value) 
                : NotFound();
        }
    }

    public class CreateInterventionRequest
    {
        [Required]
        public Guid VehicleId { get; set; }

        [Required]
        public Guid CustomerId { get; set; }

        [Required]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime ScheduledDate { get; set; }

        public List<string>? RequiredSkills { get; set; }
    }
}
```

### GraphQL Integration
```csharp
// Copilot prompt: "Create GraphQL schema for DriveOps garage interventions with HotChocolate"
namespace DriveOps.Garage.Api.GraphQL
{
    [ExtendObjectType<Query>]
    public class InterventionQueries
    {
        /// <summary>
        /// Get interventions for a tenant with filtering
        /// </summary>
        public async Task<Connection<InterventionDto>> GetInterventions(
            [Service] IMediator mediator,
            Guid tenantId,
            InterventionStatus? status = null,
            Guid? vehicleId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            PagingArguments paging = default)
        {
            var query = new GetInterventionsQuery
            {
                TenantId = new TenantId(tenantId),
                Status = status,
                VehicleId = vehicleId.HasValue ? new VehicleId(vehicleId.Value) : null,
                FromDate = fromDate,
                ToDate = toDate,
                Page = paging.First.HasValue ? (paging.After?.GetHashCode() ?? 0) / (paging.First.Value) + 1 : 1,
                PageSize = paging.First ?? 20
            };

            var result = await mediator.Send(query);

            if (result.IsFailure)
                throw new GraphQLException(result.Error);

            return new Connection<InterventionDto>(
                result.Value.Items,
                result.Value.HasNext,
                result.Value.HasPrevious);
        }
    }

    [ExtendObjectType<Mutation>]
    public class InterventionMutations
    {
        /// <summary>
        /// Create a new intervention
        /// </summary>
        public async Task<InterventionDto> CreateIntervention(
            [Service] IMediator mediator,
            CreateInterventionInput input)
        {
            var command = new CreateInterventionCommand
            {
                TenantId = new TenantId(input.TenantId),
                VehicleId = new VehicleId(input.VehicleId),
                CustomerId = new CustomerId(input.CustomerId),
                Description = input.Description,
                ScheduledDate = input.ScheduledDate,
                RequiredSkills = input.RequiredSkills ?? new List<string>()
            };

            var result = await mediator.Send(command);

            if (result.IsFailure)
                throw new GraphQLException(result.Error);

            // Return the created intervention
            var getQuery = new GetInterventionByIdQuery 
            { 
                TenantId = command.TenantId, 
                InterventionId = result.Value 
            };
            
            var interventionResult = await mediator.Send(getQuery);
            return interventionResult.Value;
        }
    }

    public record CreateInterventionInput(
        Guid TenantId,
        Guid VehicleId,
        Guid CustomerId,
        string Description,
        DateTime ScheduledDate,
        List<string>? RequiredSkills);
}
```

---

## 🎨 Blazor UI Components

### Component with Radzen Integration
```razor
@* Copilot prompt: "Create a Blazor component for displaying garage interventions list with Radzen DataGrid in DriveOps" *@
@page "/garage/interventions"
@using DriveOps.Garage.Application.Interventions.Queries
@using DriveOps.Garage.Application.Interventions.DTOs
@inject IMediator Mediator
@inject NotificationService NotificationService

<PageTitle>Garage Interventions</PageTitle>

<RadzenStack>
    <RadzenRow>
        <RadzenColumn Size="12">
            <RadzenText TextStyle="TextStyle.H3" TagName="TagName.H1">
                <RadzenIcon Icon="build" /> Garage Interventions
            </RadzenText>
        </RadzenColumn>
    </RadzenRow>

    <RadzenRow>
        <RadzenColumn Size="12">
            <RadzenCard>
                <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap="1rem">
                    <RadzenLabel Text="Status Filter:" />
                    <RadzenDropDown @bind-Value="selectedStatus" 
                                    Data="@statusOptions" 
                                    TextProperty="Text" 
                                    ValueProperty="Value"
                                    Placeholder="All Statuses"
                                    AllowClear="true"
                                    Change="OnFilterChanged" />

                    <RadzenLabel Text="Vehicle:" />
                    <RadzenDropDown @bind-Value="selectedVehicleId" 
                                    Data="@vehicleOptions" 
                                    TextProperty="Text" 
                                    ValueProperty="Value"
                                    Placeholder="All Vehicles"
                                    AllowClear="true"
                                    Change="OnFilterChanged" />

                    <RadzenButton Icon="add" Text="New Intervention" 
                                  ButtonStyle="ButtonStyle.Primary" 
                                  Click="CreateIntervention" />
                </RadzenStack>
            </RadzenCard>
        </RadzenColumn>
    </RadzenRow>

    <RadzenRow>
        <RadzenColumn Size="12">
            <RadzenDataGrid @ref="interventionsGrid" 
                            Data="@interventions" 
                            Count="@totalCount"
                            LoadData="@LoadInterventions"
                            AllowPaging="true" 
                            AllowSorting="true"
                            AllowFiltering="true"
                            PageSize="20"
                            PagerHorizontalAlign="HorizontalAlign.Left"
                            ShowPagingSummary="true"
                            TItem="InterventionDto">
                <Columns>
                    <RadzenDataGridColumn TItem="InterventionDto" Property="Id" Title="ID" Width="80px">
                        <Template Context="intervention">
                            <RadzenLink Path="@($"/garage/interventions/{intervention.Id}")" 
                                        Text="@intervention.Id.ToString("N")[..8]" />
                        </Template>
                    </RadzenDataGridColumn>

                    <RadzenDataGridColumn TItem="InterventionDto" Property="VehicleInfo" Title="Vehicle" Width="200px">
                        <Template Context="intervention">
                            <RadzenStack Orientation="Orientation.Vertical" Gap="0.2rem">
                                <RadzenText TextStyle="TextStyle.Body2">
                                    @intervention.VehicleInfo.Make @intervention.VehicleInfo.Model
                                </RadzenText>
                                <RadzenText TextStyle="TextStyle.Caption">
                                    @intervention.VehicleInfo.LicensePlate
                                </RadzenText>
                            </RadzenStack>
                        </Template>
                    </RadzenDataGridColumn>

                    <RadzenDataGridColumn TItem="InterventionDto" Property="Description" Title="Description" />

                    <RadzenDataGridColumn TItem="InterventionDto" Property="Status" Title="Status" Width="120px">
                        <Template Context="intervention">
                            <RadzenBadge BadgeStyle="@GetStatusBadgeStyle(intervention.Status)" 
                                         Text="@intervention.Status.ToString()" />
                        </Template>
                    </RadzenDataGridColumn>

                    <RadzenDataGridColumn TItem="InterventionDto" Property="ScheduledDate" Title="Scheduled" Width="150px">
                        <Template Context="intervention">
                            <RadzenText>@intervention.ScheduledDate.ToString("dd/MM/yyyy HH:mm")</RadzenText>
                        </Template>
                    </RadzenDataGridColumn>

                    <RadzenDataGridColumn TItem="InterventionDto" Property="MechanicName" Title="Mechanic" Width="150px">
                        <Template Context="intervention">
                            @if (!string.IsNullOrEmpty(intervention.MechanicName))
                            {
                                <RadzenText>@intervention.MechanicName</RadzenText>
                            }
                            else
                            {
                                <RadzenText TextStyle="TextStyle.Caption">Not assigned</RadzenText>
                            }
                        </Template>
                    </RadzenDataGridColumn>

                    <RadzenDataGridColumn TItem="InterventionDto" Title="Actions" Width="120px" Sortable="false" Filterable="false">
                        <Template Context="intervention">
                            <RadzenButton Icon="edit" ButtonStyle="ButtonStyle.Light" Size="ButtonSize.Small"
                                          Click="@(() => EditIntervention(intervention.Id))" />
                            <RadzenButton Icon="delete" ButtonStyle="ButtonStyle.Danger" Size="ButtonSize.Small" 
                                          Click="@(() => DeleteIntervention(intervention.Id))" />
                        </Template>
                    </RadzenDataGridColumn>
                </Columns>
            </RadzenDataGrid>
        </RadzenColumn>
    </RadzenRow>
</RadzenStack>

@code {
    RadzenDataGrid<InterventionDto> interventionsGrid = null!;
    IEnumerable<InterventionDto> interventions = new List<InterventionDto>();
    int totalCount;

    InterventionStatus? selectedStatus;
    Guid? selectedVehicleId;

    List<DropDownOption> statusOptions = new()
    {
        new("Scheduled", InterventionStatus.Scheduled),
        new("In Progress", InterventionStatus.InProgress),
        new("Completed", InterventionStatus.Completed),
        new("Cancelled", InterventionStatus.Cancelled)
    };

    List<DropDownOption> vehicleOptions = new();

    [CascadingParameter]
    public TenantId TenantId { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await LoadVehicleOptions();
    }

    async Task LoadInterventions(LoadDataArgs args)
    {
        var query = new GetInterventionsQuery
        {
            TenantId = TenantId,
            Status = selectedStatus,
            VehicleId = selectedVehicleId.HasValue ? new VehicleId(selectedVehicleId.Value) : null,
            Page = (args.Skip / args.Top) + 1,
            PageSize = args.Top ?? 20
        };

        var result = await Mediator.Send(query);

        if (result.IsSuccess)
        {
            interventions = result.Value.Items;
            totalCount = result.Value.TotalCount;
        }
        else
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", result.Error);
        }
    }

    async Task LoadVehicleOptions()
    {
        // Implementation for loading vehicle options
        vehicleOptions = new List<DropDownOption>
        {
            new("BMW 318i - AB123CD", Guid.NewGuid()),
            new("Mercedes C200 - EF456GH", Guid.NewGuid())
        };
    }

    async Task OnFilterChanged()
    {
        await interventionsGrid.Reload();
    }

    void CreateIntervention()
    {
        // Navigate to create intervention page
    }

    void EditIntervention(Guid interventionId)
    {
        // Navigate to edit intervention page
    }

    async Task DeleteIntervention(Guid interventionId)
    {
        var confirmed = await DialogService.Confirm("Are you sure you want to delete this intervention?", "Delete Intervention", 
            new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" });

        if (confirmed == true)
        {
            // Implement delete logic
            await interventionsGrid.Reload();
        }
    }

    BadgeStyle GetStatusBadgeStyle(InterventionStatus status) => status switch
    {
        InterventionStatus.Scheduled => BadgeStyle.Info,
        InterventionStatus.InProgress => BadgeStyle.Warning,
        InterventionStatus.Completed => BadgeStyle.Success,
        InterventionStatus.Cancelled => BadgeStyle.Danger,
        _ => BadgeStyle.Secondary
    };

    public class DropDownOption
    {
        public string Text { get; set; }
        public object Value { get; set; }

        public DropDownOption(string text, object value)
        {
            Text = text;
            Value = value;
        }
    }
}
```

---

## 🧪 Testing Patterns

### Unit Testing with Copilot
```csharp
// Copilot prompt: "Create comprehensive unit tests for DriveOps garage intervention entity using xUnit and FluentAssertions"
namespace DriveOps.Garage.Domain.Tests.Entities
{
    public class InterventionTests
    {
        private readonly TenantId _tenantId = TenantId.New();
        private readonly VehicleId _vehicleId = VehicleId.New();
        private readonly CustomerId _customerId = CustomerId.New();

        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateIntervention()
        {
            // Arrange
            const string description = "Engine maintenance";
            var scheduledDate = DateTime.UtcNow.AddDays(1);
            var requiredSkills = new List<string> { "Engine", "Diagnostics" };

            // Act
            var intervention = new Intervention(_tenantId, _vehicleId, _customerId, description, scheduledDate, requiredSkills);

            // Assert
            intervention.Id.Should().NotBe(InterventionId.Empty);
            intervention.TenantId.Should().Be(_tenantId);
            intervention.VehicleId.Should().Be(_vehicleId);
            intervention.CustomerId.Should().Be(_customerId);
            intervention.Description.Should().Be(description);
            intervention.ScheduledDate.Should().Be(scheduledDate);
            intervention.Status.Should().Be(InterventionStatus.Scheduled);
            intervention.RequiredSkills.Should().BeEquivalentTo(requiredSkills);
            intervention.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            intervention.DomainEvents.Should().ContainSingle(e => e is InterventionCreatedEvent);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void Constructor_WithInvalidDescription_ShouldThrowArgumentException(string description)
        {
            // Arrange
            var scheduledDate = DateTime.UtcNow.AddDays(1);
            var requiredSkills = new List<string>();

            // Act & Assert
            var action = () => new Intervention(_tenantId, _vehicleId, _customerId, description, scheduledDate, requiredSkills);
            action.Should().Throw<ArgumentException>()
                .WithMessage("Description cannot be null or empty*");
        }

        [Fact]
        public void Constructor_WithPastScheduledDate_ShouldThrowArgumentException()
        {
            // Arrange
            const string description = "Engine maintenance";
            var scheduledDate = DateTime.UtcNow.AddDays(-1);
            var requiredSkills = new List<string>();

            // Act & Assert
            var action = () => new Intervention(_tenantId, _vehicleId, _customerId, description, scheduledDate, requiredSkills);
            action.Should().Throw<ArgumentException>()
                .WithMessage("Scheduled date cannot be in the past*");
        }

        [Fact]
        public void StartIntervention_WithValidMechanic_ShouldUpdateStatusAndSetStartTime()
        {
            // Arrange
            var intervention = CreateValidIntervention();
            var mechanicId = MechanicId.New();

            // Act
            intervention.StartIntervention(mechanicId);

            // Assert
            intervention.Status.Should().Be(InterventionStatus.InProgress);
            intervention.MechanicId.Should().Be(mechanicId);
            intervention.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            intervention.DomainEvents.Should().Contain(e => e is InterventionStartedEvent);
        }

        [Fact]
        public void StartIntervention_WhenAlreadyStarted_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var intervention = CreateValidIntervention();
            var mechanicId = MechanicId.New();
            intervention.StartIntervention(mechanicId);

            // Act & Assert
            var action = () => intervention.StartIntervention(mechanicId);
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("Intervention is already started*");
        }

        [Fact]
        public void CompleteIntervention_WithValidParameters_ShouldUpdateStatusAndSetCompletionTime()
        {
            // Arrange
            var intervention = CreateValidIntervention();
            var mechanicId = MechanicId.New();
            intervention.StartIntervention(mechanicId);
            var totalCost = new Money(150.50m, "EUR");
            const string notes = "Completed successfully";

            // Act
            intervention.CompleteIntervention(totalCost, notes);

            // Assert
            intervention.Status.Should().Be(InterventionStatus.Completed);
            intervention.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            intervention.TotalCost.Should().Be(totalCost);
            intervention.Notes.Should().Be(notes);
            intervention.ActualDuration.Should().NotBeNull();
            intervention.DomainEvents.Should().Contain(e => e is InterventionCompletedEvent);
        }

        [Fact]
        public void CompleteIntervention_WhenNotStarted_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var intervention = CreateValidIntervention();
            var totalCost = new Money(150.50m, "EUR");

            // Act & Assert
            var action = () => intervention.CompleteIntervention(totalCost, "notes");
            action.Should().Throw<InvalidOperationException>()
                .WithMessage("Cannot complete intervention that has not been started*");
        }

        [Fact]
        public void AddItem_WithValidItem_ShouldAddToItems()
        {
            // Arrange
            var intervention = CreateValidIntervention();
            const string itemDescription = "Oil change";
            var itemCost = new Money(45.00m, "EUR");
            const int quantity = 1;

            // Act
            intervention.AddItem(itemDescription, itemCost, quantity);

            // Assert
            intervention.Items.Should().ContainSingle();
            var item = intervention.Items.First();
            item.Description.Should().Be(itemDescription);
            item.UnitCost.Should().Be(itemCost);
            item.Quantity.Should().Be(quantity);
            item.TotalCost.Should().Be(itemCost);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void AddItem_WithInvalidQuantity_ShouldThrowArgumentException(int quantity)
        {
            // Arrange
            var intervention = CreateValidIntervention();
            const string itemDescription = "Oil change";
            var itemCost = new Money(45.00m, "EUR");

            // Act & Assert
            var action = () => intervention.AddItem(itemDescription, itemCost, quantity);
            action.Should().Throw<ArgumentException>()
                .WithMessage("Quantity must be greater than zero*");
        }

        private Intervention CreateValidIntervention()
        {
            return new Intervention(
                _tenantId,
                _vehicleId,
                _customerId,
                "Test intervention",
                DateTime.UtcNow.AddDays(1),
                new List<string> { "General" });
        }
    }
}
```

### Integration Testing
```csharp
// Copilot prompt: "Create integration tests for DriveOps garage intervention API using ASP.NET Core TestHost"
namespace DriveOps.Garage.Api.Tests.Integration
{
    [Collection("Integration Tests")]
    public class InterventionsControllerTests : IClassFixture<GarageWebApplicationFactory>
    {
        private readonly GarageWebApplicationFactory _factory;
        private readonly HttpClient _client;
        private readonly TenantId _tenantId = TenantId.New();

        public InterventionsControllerTests(GarageWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetInterventions_WithValidTenant_ShouldReturnPagedResults()
        {
            // Arrange
            await SeedTestData();

            // Act
            var response = await _client.GetAsync($"/api/tenants/{_tenantId.Value}/garage/interventions");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<PagedList<InterventionDto>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            result.Should().NotBeNull();
            result.Items.Should().NotBeEmpty();
            result.TotalCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task CreateIntervention_WithValidData_ShouldReturnCreatedIntervention()
        {
            // Arrange
            await SeedTestData();
            var vehicleId = await GetTestVehicleId();
            var customerId = await GetTestCustomerId();

            var request = new CreateInterventionRequest
            {
                VehicleId = vehicleId,
                CustomerId = customerId,
                Description = "Test intervention",
                ScheduledDate = DateTime.UtcNow.AddDays(1),
                RequiredSkills = new List<string> { "General" }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"/api/tenants/{_tenantId.Value}/garage/interventions", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<InterventionDto>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            result.Should().NotBeNull();
            result.Description.Should().Be(request.Description);
            result.VehicleId.Should().Be(request.VehicleId);
            result.CustomerId.Should().Be(request.CustomerId);
            result.Status.Should().Be(InterventionStatus.Scheduled);
        }

        [Fact]
        public async Task CreateIntervention_WithInvalidData_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new CreateInterventionRequest
            {
                VehicleId = Guid.Empty, // Invalid
                CustomerId = Guid.NewGuid(),
                Description = "", // Invalid
                ScheduledDate = DateTime.UtcNow.AddDays(-1) // Invalid
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Act
            var response = await _client.PostAsync($"/api/tenants/{_tenantId.Value}/garage/interventions", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetIntervention_WithValidId_ShouldReturnIntervention()
        {
            // Arrange
            await SeedTestData();
            var interventionId = await CreateTestIntervention();

            // Act
            var response = await _client.GetAsync($"/api/tenants/{_tenantId.Value}/garage/interventions/{interventionId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<InterventionDto>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            result.Should().NotBeNull();
            result.Id.Should().Be(interventionId);
        }

        [Fact]
        public async Task GetIntervention_WithInvalidId_ShouldReturnNotFound()
        {
            // Arrange
            var invalidId = Guid.NewGuid();

            // Act
            var response = await _client.GetAsync($"/api/tenants/{_tenantId.Value}/garage/interventions/{invalidId}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        private async Task SeedTestData()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GarageDbContext>();
            
            // Seed test data
            // Implementation depends on your seeding strategy
        }

        private async Task<Guid> GetTestVehicleId()
        {
            // Return a test vehicle ID
            return Guid.NewGuid();
        }

        private async Task<Guid> GetTestCustomerId()
        {
            // Return a test customer ID
            return Guid.NewGuid();
        }

        private async Task<Guid> CreateTestIntervention()
        {
            // Create and return a test intervention ID
            return Guid.NewGuid();
        }
    }

    public class GarageWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Replace database with in-memory for testing
                services.RemoveAll(typeof(DbContextOptions<GarageDbContext>));
                services.AddDbContext<GarageDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDatabase");
                });

                // Configure test authentication
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });
            });

            builder.UseEnvironment("Testing");
        }
    }
}
```

---

## 🚀 Workflow Integration

### Development Cycle with Copilot

#### 1. Feature Development Workflow
```markdown
1. **Context Setup** (Before starting development)
   - Open relevant documentation files
   - Review existing similar features
   - Load architecture patterns into Copilot context

2. **Domain Design** (With Copilot assistance)
   - Use prompts: "Create DDD entity for [feature] following DriveOps patterns"
   - Generate value objects, aggregates, and domain events
   - Create domain services and specifications

3. **Application Layer** (CQRS implementation)
   - Generate command/query handlers
   - Create DTOs and mapping profiles
   - Implement validation rules

4. **Infrastructure** (Database and external services)
   - Generate EF Core configurations
   - Create repository implementations
   - Set up external service integrations

5. **API Layer** (REST and GraphQL)
   - Generate controller actions
   - Create GraphQL resolvers
   - Implement proper error handling

6. **UI Components** (Blazor with Radzen)
   - Generate Blazor pages and components
   - Implement Radzen data grids and forms
   - Add responsive design elements

7. **Testing** (Comprehensive test coverage)
   - Generate unit tests for domain logic
   - Create integration tests for APIs
   - Implement UI component tests
```

#### 2. Code Review with Copilot
```markdown
**Copilot-Assisted Code Review Checklist:**

- [ ] **Architecture Compliance**
  - Prompt: "Review this code for DDD and CQRS best practices"
  - Check multi-tenancy implementation
  - Verify proper domain event usage

- [ ] **Security Review**
  - Prompt: "Check this code for security vulnerabilities"
  - Verify tenant isolation
  - Check authorization implementations

- [ ] **Performance Analysis**
  - Prompt: "Analyze this code for performance issues"
  - Review database queries
  - Check async/await patterns

- [ ] **Code Quality**
  - Prompt: "Improve code readability and maintainability"
  - Verify naming conventions
  - Check error handling
```

#### 3. Documentation Generation
```markdown
**Auto-Documentation with Copilot:**

1. **API Documentation**
   - Prompt: "Generate OpenAPI documentation for this controller"
   - Create comprehensive XML comments
   - Generate usage examples

2. **Architecture Documentation**
   - Prompt: "Create architecture diagram for this module"
   - Generate sequence diagrams
   - Document component interactions

3. **Database Documentation**
   - Prompt: "Generate database schema documentation"
   - Create ERD diagrams
   - Document indexes and constraints
```

---

## 🔧 Advanced Copilot Features

### 1. Copilot Chat Integration

#### Architecture Discussions
```
You: How should I implement a new business module in DriveOps following the existing patterns?

Copilot: Based on the DriveOps architecture, here's how to implement a new business module:

1. **Domain Layer Structure**:
   - Create aggregates following DDD patterns
   - Implement domain events for cross-module communication
   - Use value objects for complex types

2. **Multi-tenant Considerations**:
   - Always include TenantId in entities
   - Implement tenant-aware repositories
   - Use tenant-scoped queries

3. **Integration Points**:
   - Use MediatR for CQRS implementation
   - Integrate with core modules (Users, Notifications, Files)
   - Follow the established gRPC communication patterns

Would you like me to generate the boilerplate code for a specific module?
```

#### Code Explanations
```
You: Explain this complex business logic in the garage intervention

Copilot: This code implements the intervention scheduling logic with these key features:

1. **Multi-tenant Validation**: Ensures the vehicle belongs to the same tenant
2. **Conflict Detection**: Checks for overlapping interventions
3. **Skill Matching**: Verifies mechanic has required skills
4. **Resource Availability**: Confirms workshop bay availability
5. **Business Rules**: Enforces minimum notice period and working hours

The method uses domain events to notify other modules about the scheduled intervention.
```

### 2. Code Refactoring with Copilot

#### Performance Optimization
```csharp
// Copilot prompt: "Optimize this DriveOps query for better performance with multi-tenancy"

// Before (Copilot identifies performance issues)
public async Task<List<Intervention>> GetInterventionsByStatusAsync(TenantId tenantId, InterventionStatus status)
{
    return await _context.Interventions
        .Where(i => i.TenantId == tenantId)
        .Where(i => i.Status == status)
        .Include(i => i.Vehicle)
        .Include(i => i.Customer)
        .Include(i => i.Mechanic)
        .ToListAsync();
}

// After (Copilot optimization)
public async Task<List<Intervention>> GetInterventionsByStatusAsync(TenantId tenantId, InterventionStatus status)
{
    return await _context.Interventions
        .Where(i => i.TenantId == tenantId && i.Status == status) // Combined filter
        .Select(i => new InterventionDto // Projection to reduce data transfer
        {
            Id = i.Id,
            Description = i.Description,
            Status = i.Status,
            ScheduledDate = i.ScheduledDate,
            VehicleInfo = new VehicleInfoDto
            {
                Make = i.Vehicle.Make,
                Model = i.Vehicle.Model,
                LicensePlate = i.Vehicle.LicensePlate
            },
            CustomerName = i.Customer.FirstName + " " + i.Customer.LastName,
            MechanicName = i.Mechanic != null ? i.Mechanic.FirstName + " " + i.Mechanic.LastName : null
        })
        .ToListAsync();
}
```

#### Modernization Suggestions
```csharp
// Copilot prompt: "Modernize this DriveOps code to use latest C# features"

// Before
public class InterventionService
{
    public async Task<Result<Intervention>> CreateInterventionAsync(CreateInterventionRequest request)
    {
        if (request == null)
            return Result.Failure<Intervention>("Request cannot be null");
            
        if (string.IsNullOrEmpty(request.Description))
            return Result.Failure<Intervention>("Description is required");
            
        // ... more validation
    }
}

// After (using records, pattern matching, and required properties)
public class InterventionService
{
    public async Task<Result<Intervention>> CreateInterventionAsync(CreateInterventionRequest request)
    {
        var validationResult = ValidateRequest(request);
        if (validationResult.IsFailure)
            return Result.Failure<Intervention>(validationResult.Error);
            
        // ... implementation
    }
    
    private static Result ValidateRequest(CreateInterventionRequest request) => request switch
    {
        null => Result.Failure("Request cannot be null"),
        { Description: null or "" } => Result.Failure("Description is required"),
        { ScheduledDate: var date } when date <= DateTime.Now => Result.Failure("Scheduled date must be in the future"),
        _ => Result.Success()
    };
}

public record CreateInterventionRequest(
    Guid VehicleId,
    Guid CustomerId,
    string Description,
    DateTime ScheduledDate,
    List<string>? RequiredSkills = null);
```

---

## 📈 Metrics and Analytics

### Copilot Usage Tracking
```csharp
// Copilot prompt: "Create analytics for tracking Copilot usage effectiveness in DriveOps development"

namespace DriveOps.DevTools.Analytics
{
    public class CopilotUsageTracker
    {
        private readonly ILogger<CopilotUsageTracker> _logger;
        
        public void TrackSuggestionAccepted(string feature, string codeType, int linesSaved)
        {
            _logger.LogInformation("Copilot suggestion accepted for {Feature}, {CodeType}, saved {LinesSaved} lines", 
                feature, codeType, linesSaved);
        }
        
        public void TrackDevelopmentMetrics(TimeSpan developmentTime, int featuresCompleted, int bugsIntroduced)
        {
            var metrics = new
            {
                DevelopmentTime = developmentTime,
                FeaturesCompleted = featuresCompleted,
                BugsIntroduced = bugsIntroduced,
                Efficiency = featuresCompleted / developmentTime.TotalHours
            };
            
            _logger.LogInformation("Development metrics: {@Metrics}", metrics);
        }
    }
}
```

---

## ✅ Best Practices Summary

### 1. Context Management
- **Always load architecture files** before starting development
- **Keep relevant documentation open** during coding sessions
- **Use descriptive prompts** that reference DriveOps patterns

### 2. Code Generation
- **Start with domain models** and work outwards to infrastructure
- **Use established patterns** from existing modules
- **Validate generated code** against architectural principles

### 3. Testing Strategy
- **Generate tests alongside** production code
- **Use Copilot for test data creation** and scenario generation
- **Leverage AI for edge case identification**

### 4. Documentation
- **Auto-generate API documentation** with proper examples
- **Use Copilot for code comments** and explanations
- **Keep documentation in sync** with code changes

### 5. Performance Optimization
- **Use Copilot for query optimization** suggestions
- **Generate performance tests** for critical paths
- **Implement monitoring** for Copilot-generated code

---

## 🎯 Conclusion

This comprehensive guide provides everything needed to maximize GitHub Copilot's effectiveness with the DriveOps platform. By following these patterns, templates, and workflows, developers can:

- **Accelerate Development**: Reduce boilerplate code and focus on business logic
- **Maintain Quality**: Follow established architectural patterns consistently
- **Improve Testing**: Generate comprehensive test coverage automatically
- **Enhance Documentation**: Keep documentation current with minimal effort
- **Optimize Performance**: Leverage AI for performance improvements

The combination of DriveOps' well-structured architecture and Copilot's AI assistance creates a powerful development environment that significantly improves productivity while maintaining high code quality standards.

---

*Last updated: 2024-12-19*  
*Version: 1.0.0*