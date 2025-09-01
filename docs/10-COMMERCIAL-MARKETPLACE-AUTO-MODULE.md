# MARKETPLACE AUTO - Commercial Module Documentation

## 🎯 Module Overview

The MARKETPLACE AUTO module is a comprehensive vehicle sales and consignment management system that operates on a dual-tier business model. This module enables individual dealers to create their own branded websites while participating in a centralized marketplace, generating revenue through both subscription fees and transaction commissions.

### Business Model Innovation

- **Network Effect Strategy**: More dealers = More inventory = More buyers = More dealers wanting to join
- **Multiple Revenue Streams**: Monthly subscriptions + Transaction commissions + Premium features  
- **Viral Growth Mechanism**: Buyers discover new dealers through marketplace, dealers see competitor success

### Revenue Tiers

- **Individual Sales**: 29€/month for standalone dealer websites with basic features
- **Marketplace Premium**: 59€/month for centralized marketplace visibility plus enhanced features
- **Transaction Commissions**: 2-3% per completed sale (configurable by deal size and dealer tier)

---

## 🏗️ Technical Architecture

### Module Structure (DDD Pattern)

```
DriveOps.Marketplace/
├── Domain/
│   ├── Entities/
│   │   ├── Marketplace/
│   │   ├── Dealers/
│   │   ├── Vehicles/
│   │   ├── Sales/
│   │   └── Analytics/
│   ├── ValueObjects/
│   ├── Aggregates/
│   ├── DomainEvents/
│   └── Repositories/
├── Application/
│   ├── Commands/
│   ├── Queries/
│   ├── Handlers/
│   ├── DTOs/
│   └── Services/
├── Infrastructure/
│   ├── Persistence/
│   ├── External/
│   ├── Messaging/
│   ├── Payment/
│   └── Search/
└── Presentation/
    ├── API/
    ├── GraphQL/
    ├── WebApp/
    └── Mobile/
## 🔧 Domain Layer - Business Entities

### Marketplace Entities

```csharp
namespace DriveOps.Marketplace.Domain.Entities
{
    public class MarketplaceDealer : AggregateRoot
    {
        public MarketplaceDealerId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string DealerName { get; private set; }
        public string BusinessLicense { get; private set; }
        public Address BusinessAddress { get; private set; }
        public ContactInfo ContactInfo { get; private set; }
        public DealerSubscriptionTier SubscriptionTier { get; private set; }
        public DealerStatus Status { get; private set; }
        public decimal CommissionRate { get; private set; } // 2-3%
        public DateTime JoinedAt { get; private set; }
        public DealerPerformanceMetrics Performance { get; private set; }
        
        private readonly List<DealerStore> _stores = new();
        public IReadOnlyCollection<DealerStore> Stores => _stores.AsReadOnly();
        
        private readonly List<VehicleListing> _listings = new();
        public IReadOnlyCollection<VehicleListing> Listings => _listings.AsReadOnly();

        public void CreateStore(string storeName, StoreConfiguration configuration)
        {
            var store = new DealerStore(TenantId, Id, storeName, configuration);
            _stores.Add(store);
            AddDomainEvent(new DealerStoreCreatedEvent(TenantId, Id, store.Id));
        }

        public void ListVehicle(Vehicle vehicle, VehicleListingDetails details)
        {
            if (Status != DealerStatus.Active)
                throw new InvalidOperationException("Dealer must be active to list vehicles");

            var listing = new VehicleListing(TenantId, Id, vehicle.Id, details);
            _listings.Add(listing);
            AddDomainEvent(new VehicleListedEvent(TenantId, Id, listing.Id));
        }

        public void UpdateCommissionRate(decimal newRate, UserId updatedBy)
        {
            if (newRate < 0.01m || newRate > 0.05m)
                throw new ArgumentException("Commission rate must be between 1% and 5%");

            CommissionRate = newRate;
            AddDomainEvent(new DealerCommissionRateUpdatedEvent(TenantId, Id, newRate, updatedBy));
        }
    }

    public class DealerStore : Entity
    {
        public DealerStoreId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public MarketplaceDealerId DealerId { get; private set; }
        public string StoreName { get; private set; }
        public string? CustomDomain { get; private set; }
        public StoreConfiguration Configuration { get; private set; }
        public StoreTheme Theme { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        
        public void UpdateTheme(StoreTheme newTheme)
        {
            Theme = newTheme;
            AddDomainEvent(new StoreThemeUpdatedEvent(TenantId, DealerId, Id, newTheme));
        }

        public void SetCustomDomain(string domain)
        {
            CustomDomain = domain;
            AddDomainEvent(new StoreCustomDomainSetEvent(TenantId, DealerId, Id, domain));
        }
    }

    public class VehicleListing : Entity
    {
        public VehicleListingId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public MarketplaceDealerId DealerId { get; private set; }
        public VehicleId VehicleId { get; private set; }
        public decimal Price { get; private set; }
        public string Description { get; private set; }
        public ListingStatus Status { get; private set; }
        public ListingType Type { get; private set; } // Sale, Consignment
        public DateTime ListedAt { get; private set; }
        public DateTime? SoldAt { get; private set; }
        public bool IsFeatured { get; private set; }
        public int ViewCount { get; private set; }
        public int InquiryCount { get; private set; }
        
        private readonly List<VehiclePhoto> _photos = new();
        public IReadOnlyCollection<VehiclePhoto> Photos => _photos.AsReadOnly();
        
        private readonly List<SalesLead> _leads = new();
        public IReadOnlyCollection<SalesLead> Leads => _leads.AsReadOnly();

        public void UpdatePrice(decimal newPrice, PriceChangeReason reason)
        {
            if (newPrice <= 0)
                throw new ArgumentException("Price must be positive");

            var oldPrice = Price;
            Price = newPrice;
### Sales Management Entities

```csharp
namespace DriveOps.Marketplace.Domain.Entities.Sales
{
    public class SalesLead : Entity
    {
        public SalesLeadId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public MarketplaceDealerId DealerId { get; private set; }
        public VehicleListingId VehicleListingId { get; private set; }
        public BuyerProfile Buyer { get; private set; }
        public LeadSource Source { get; private set; }
        public LeadStatus Status { get; private set; }
        public decimal? OfferedPrice { get; private set; }
        public string? Notes { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public UserId? AssignedTo { get; private set; }
        
        private readonly List<LeadActivity> _activities = new();
        public IReadOnlyCollection<LeadActivity> Activities => _activities.AsReadOnly();

        public void AssignToSalesPerson(UserId salesPersonId)
        {
            AssignedTo = salesPersonId;
            Status = LeadStatus.Assigned;
            AddActivity(LeadActivityType.Assigned, $"Lead assigned to salesperson");
            AddDomainEvent(new LeadAssignedEvent(TenantId, DealerId, Id, salesPersonId));
        }

        public void UpdateStatus(LeadStatus newStatus, string? notes = null)
        {
            var oldStatus = Status;
            Status = newStatus;
            Notes = notes;
            AddActivity(LeadActivityType.StatusChanged, $"Status changed from {oldStatus} to {newStatus}");
            AddDomainEvent(new LeadStatusUpdatedEvent(TenantId, DealerId, Id, oldStatus, newStatus));
        }

        public void RecordOffer(decimal offeredPrice, string? negotiationNotes = null)
        {
            OfferedPrice = offeredPrice;
            AddActivity(LeadActivityType.OfferMade, $"Buyer offered ${offeredPrice:N2}");
            AddDomainEvent(new BuyerOfferMadeEvent(TenantId, DealerId, Id, offeredPrice));
        }

        private void AddActivity(LeadActivityType type, string description)
        {
            var activity = new LeadActivity(TenantId, Id, type, description, DateTime.UtcNow);
            _activities.Add(activity);
        }
    }

    public class SalesContract : AggregateRoot
    {
        public SalesContractId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public MarketplaceDealerId DealerId { get; private set; }
        public VehicleListingId VehicleListingId { get; private set; }
        public SalesLeadId LeadId { get; private set; }
        public BuyerProfile Buyer { get; private set; }
        public decimal SalePrice { get; private set; }
        public decimal CommissionAmount { get; private set; }
        public decimal DealerAmount { get; private set; }
        public ContractStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? SignedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public FileId? ContractDocumentId { get; private set; }
        
        private readonly List<ContractPayment> _payments = new();
        public IReadOnlyCollection<ContractPayment> Payments => _payments.AsReadOnly();

        public static SalesContract Create(
            TenantId tenantId,
            MarketplaceDealerId dealerId,
            VehicleListingId vehicleListingId,
            SalesLeadId leadId,
            BuyerProfile buyer,
            decimal salePrice,
            decimal commissionRate)
        {
            var commissionAmount = salePrice * commissionRate;
            var dealerAmount = salePrice - commissionAmount;

            var contract = new SalesContract
            {
                Id = SalesContractId.New(),
                TenantId = tenantId,
                DealerId = dealerId,
                VehicleListingId = vehicleListingId,
                LeadId = leadId,
                Buyer = buyer,
                SalePrice = salePrice,
                CommissionAmount = commissionAmount,
                DealerAmount = dealerAmount,
                Status = ContractStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            contract.AddDomainEvent(new SalesContractCreatedEvent(tenantId, dealerId, contract.Id));
            return contract;
        }

        public void AttachSignedDocument(FileId documentId)
        {
            ContractDocumentId = documentId;
            Status = ContractStatus.Signed;
            SignedAt = DateTime.UtcNow;
            AddDomainEvent(new ContractSignedEvent(TenantId, DealerId, Id));
        }

        public void AddPayment(decimal amount, PaymentMethod method, string? transactionId = null)
        {
            var payment = new ContractPayment(TenantId, Id, amount, method, transactionId);
            _payments.Add(payment);

            if (_payments.Sum(p => p.Amount) >= SalePrice)
            {
                Status = ContractStatus.Completed;
                CompletedAt = DateTime.UtcNow;
                AddDomainEvent(new SalesContractCompletedEvent(TenantId, DealerId, Id));
            }

            AddDomainEvent(new PaymentReceivedEvent(TenantId, DealerId, Id, payment.Id, amount));
        }
    }

    public class DepositSale : AggregateRoot
    {
### Customer and Analytics Entities

```csharp
namespace DriveOps.Marketplace.Domain.Entities.Analytics
{
    public class BuyerProfile : Entity
    {
        public BuyerProfileId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Email { get; private set; }
        public string? Phone { get; private set; }
        public Address? Address { get; private set; }
        public BuyerPreferences Preferences { get; private set; }
        public CreditProfile? CreditProfile { get; private set; }
        public DateTime CreatedAt { get; private set; }
        
        private readonly List<VehicleInquiry> _inquiries = new();
        public IReadOnlyCollection<VehicleInquiry> Inquiries => _inquiries.AsReadOnly();
        
        private readonly List<SavedSearch> _savedSearches = new();
        public IReadOnlyCollection<SavedSearch> SavedSearches => _savedSearches.AsReadOnly();

        public void CreateInquiry(VehicleListingId listingId, string message, InquiryType type)
        {
            var inquiry = new VehicleInquiry(TenantId, Id, listingId, message, type);
            _inquiries.Add(inquiry);
            AddDomainEvent(new VehicleInquiryCreatedEvent(TenantId, Id, listingId, inquiry.Id));
        }

        public void SaveSearch(SearchCriteria criteria, string name)
        {
            var savedSearch = new SavedSearch(TenantId, Id, criteria, name);
            _savedSearches.Add(savedSearch);
            AddDomainEvent(new SearchSavedEvent(TenantId, Id, savedSearch.Id));
        }

        public void UpdateCreditProfile(CreditProfile creditProfile)
        {
            CreditProfile = creditProfile;
            AddDomainEvent(new BuyerCreditProfileUpdatedEvent(TenantId, Id, creditProfile));
        }
    }

    public class MarketplaceAnalytics : AggregateRoot
    {
        public MarketplaceAnalyticsId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public DateTime AnalysisDate { get; private set; }
        public MarketMetrics MarketMetrics { get; private set; }
        public DealerPerformanceMetrics DealerMetrics { get; private set; }
        public InventoryMetrics InventoryMetrics { get; private set; }
        public CustomerMetrics CustomerMetrics { get; private set; }
        
        public void UpdateMarketMetrics(int totalListings, decimal averagePrice, int totalSales, decimal totalRevenue)
        {
            MarketMetrics = new MarketMetrics(totalListings, averagePrice, totalSales, totalRevenue);
            AddDomainEvent(new MarketMetricsUpdatedEvent(TenantId, Id, MarketMetrics));
        }

        public void UpdateDealerMetrics(int activeDealers, decimal averageCommission, int topPerformers)
        {
            DealerMetrics = new DealerPerformanceMetrics(activeDealers, averageCommission, topPerformers);
            AddDomainEvent(new DealerMetricsUpdatedEvent(TenantId, Id, DealerMetrics));
        }
    }

    public class VehicleValuation : Entity
    {
        public VehicleValuationId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public VehicleId VehicleId { get; private set; }
        public ValuationSource Source { get; private set; } // KBB, Edmunds, Market Analysis
        public decimal EstimatedValue { get; private set; }
        public decimal MarketValue { get; private set; }
## 🗄️ Database Schema (PostgreSQL)

### Marketplace Core Tables

```sql
-- Marketplace module schema
CREATE SCHEMA IF NOT EXISTS marketplace;

-- Marketplace dealers table
CREATE TABLE marketplace.marketplace_dealers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_name VARCHAR(255) NOT NULL,
    business_license VARCHAR(100) NOT NULL,
    business_address JSONB NOT NULL, -- Address value object
    contact_info JSONB NOT NULL, -- ContactInfo value object  
    subscription_tier INTEGER NOT NULL, -- 1: Individual, 2: Premium
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: Suspended, 3: Inactive
    commission_rate DECIMAL(5,4) NOT NULL DEFAULT 0.025, -- 2.5% default
    joined_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    performance_metrics JSONB, -- Performance metrics value object
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, business_license)
);

-- Dealer stores (individual websites)
CREATE TABLE marketplace.dealer_stores (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id) ON DELETE CASCADE,
    store_name VARCHAR(255) NOT NULL,
    custom_domain VARCHAR(255),
    configuration JSONB NOT NULL, -- Store configuration
    theme JSONB NOT NULL, -- Store theme settings
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(custom_domain)
);

-- Store configurations
CREATE TABLE marketplace.store_configurations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    store_id UUID NOT NULL REFERENCES marketplace.dealer_stores(id) ON DELETE CASCADE,
    configuration_key VARCHAR(100) NOT NULL,
    configuration_value JSONB NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(store_id, configuration_key)
);

-- Dealer subscriptions
CREATE TABLE marketplace.dealer_subscriptions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id) ON DELETE CASCADE,
    subscription_tier INTEGER NOT NULL,
    monthly_fee DECIMAL(10,2) NOT NULL,
    billing_cycle INTEGER NOT NULL DEFAULT 1, -- Monthly
    status INTEGER NOT NULL DEFAULT 1, -- Active
    starts_at TIMESTAMP WITH TIME ZONE NOT NULL,
    ends_at TIMESTAMP WITH TIME ZONE,
    auto_renewal BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
```

### Vehicle Listings and Inventory

```sql
-- Vehicle listings in marketplace
CREATE TABLE marketplace.vehicle_listings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id) ON DELETE CASCADE,
    vehicle_id UUID NOT NULL, -- References vehicles.vehicles(id)
    price DECIMAL(12,2) NOT NULL,
    description TEXT,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: Pending, 3: Sold, 4: Withdrawn
    listing_type INTEGER NOT NULL DEFAULT 1, -- 1: Sale, 2: Consignment
    listed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    sold_at TIMESTAMP WITH TIME ZONE,
    is_featured BOOLEAN NOT NULL DEFAULT FALSE,
    view_count INTEGER NOT NULL DEFAULT 0,
    inquiry_count INTEGER NOT NULL DEFAULT 0,
    seo_title VARCHAR(255),
    seo_description TEXT,
    seo_keywords VARCHAR(500),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    INDEX idx_marketplace_listings_dealer_status (dealer_id, status),
    INDEX idx_marketplace_listings_price (price),
    INDEX idx_marketplace_listings_vehicle (vehicle_id)
);

-- Vehicle inventory management
CREATE TABLE marketplace.vehicle_inventory (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id),
    vehicle_id UUID NOT NULL,
    listing_id UUID REFERENCES marketplace.vehicle_listings(id),
    acquisition_date TIMESTAMP WITH TIME ZONE,
    acquisition_price DECIMAL(12,2),
    current_market_value DECIMAL(12,2),
    days_in_inventory INTEGER DEFAULT 0,
    location VARCHAR(255),
    condition_rating INTEGER, -- 1-10 scale
### Sales and Lead Management

```sql
-- Sales leads
CREATE TABLE marketplace.sales_leads (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id),
    listing_id UUID NOT NULL REFERENCES marketplace.vehicle_listings(id),
    buyer_profile JSONB NOT NULL, -- BuyerProfile value object
    lead_source INTEGER NOT NULL, -- 1: Marketplace, 2: Dealer Site, 3: Phone, 4: Walk-in
    status INTEGER NOT NULL DEFAULT 1, -- 1: New, 2: Contacted, 3: Qualified, 4: Converted, 5: Lost
    offered_price DECIMAL(12,2),
    notes TEXT,
    assigned_to UUID, -- References users.users(id)
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    INDEX idx_sales_leads_dealer_status (dealer_id, status),
    INDEX idx_sales_leads_assigned (assigned_to)
);

-- Customer inquiries
CREATE TABLE marketplace.customer_inquiries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    listing_id UUID NOT NULL REFERENCES marketplace.vehicle_listings(id),
    buyer_id UUID, -- References marketplace.buyer_profiles(id)
    inquiry_type INTEGER NOT NULL, -- 1: Price Question, 2: Test Drive, 3: Financing, 4: Trade-in
    message TEXT NOT NULL,
    contact_method INTEGER NOT NULL DEFAULT 1, -- 1: Email, 2: Phone, 3: SMS
    responded_at TIMESTAMP WITH TIME ZONE,
    response_time_minutes INTEGER,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Test drive appointments
CREATE TABLE marketplace.test_drives (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id),
    listing_id UUID NOT NULL REFERENCES marketplace.vehicle_listings(id),
    buyer_profile JSONB NOT NULL,
    scheduled_at TIMESTAMP WITH TIME ZONE NOT NULL,
    duration_minutes INTEGER NOT NULL DEFAULT 30,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Scheduled, 2: Completed, 3: Cancelled, 4: No-show
    sales_person_id UUID, -- References users.users(id)
    notes TEXT,
    completed_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Sales appointments
CREATE TABLE marketplace.sales_appointments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id),
    lead_id UUID NOT NULL REFERENCES marketplace.sales_leads(id),
    appointment_type INTEGER NOT NULL, -- 1: Consultation, 2: Negotiation, 3: Closing
    scheduled_at TIMESTAMP WITH TIME ZONE NOT NULL,
    duration_minutes INTEGER NOT NULL DEFAULT 60,
    status INTEGER NOT NULL DEFAULT 1, -- Scheduled, Completed, Cancelled, Rescheduled
    sales_person_id UUID NOT NULL,
    notes TEXT,
    outcome INTEGER, -- Meeting outcome
    follow_up_required BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
```

### Sales Contracts and Transactions

```sql
-- Sales contracts
CREATE TABLE marketplace.sales_contracts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id),
    listing_id UUID NOT NULL REFERENCES marketplace.vehicle_listings(id),
    lead_id UUID NOT NULL REFERENCES marketplace.sales_leads(id),
    buyer_profile JSONB NOT NULL,
    sale_price DECIMAL(12,2) NOT NULL,
    commission_amount DECIMAL(12,2) NOT NULL,
    dealer_amount DECIMAL(12,2) NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Draft, 2: Signed, 3: Completed, 4: Cancelled
    contract_terms JSONB, -- Contract terms and conditions
    contract_document_id UUID, -- References files.files(id)
    signed_at TIMESTAMP WITH TIME ZONE,
    completed_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Deposit sales (consignment)
CREATE TABLE marketplace.deposit_sales (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id),
    vehicle_id UUID NOT NULL,
    owner_profile JSONB NOT NULL, -- Vehicle owner information
    reserve_price DECIMAL(12,2) NOT NULL,
    commission_rate DECIMAL(5,4) NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: Sold, 3: Withdrawn, 4: Expired
    special_instructions TEXT,
    deposited_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
### Buyer Profiles and Analytics

```sql
-- Buyer profiles
CREATE TABLE marketplace.buyer_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL,
    phone VARCHAR(20),
    address JSONB, -- Address value object
    preferences JSONB, -- Buyer preferences (budget, brands, features)
    credit_profile JSONB, -- Credit information and pre-approval
    registration_source INTEGER NOT NULL DEFAULT 1, -- 1: Marketplace, 2: Dealer Site
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    INDEX idx_buyer_profiles_email (email)
);

-- Financing applications
CREATE TABLE marketplace.financing_applications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    buyer_id UUID NOT NULL REFERENCES marketplace.buyer_profiles(id),
    listing_id UUID REFERENCES marketplace.vehicle_listings(id),
    loan_amount DECIMAL(12,2) NOT NULL,
    down_payment DECIMAL(12,2),
    desired_term_months INTEGER NOT NULL,
    employment_info JSONB NOT NULL,
    financial_info JSONB NOT NULL,
    application_status INTEGER NOT NULL DEFAULT 1, -- 1: Submitted, 2: Under Review, 3: Approved, 4: Declined
    lender_id UUID, -- References to lender partners
    approval_amount DECIMAL(12,2),
    approved_rate DECIMAL(5,4),
    approved_term_months INTEGER,
    submitted_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Insurance quotes
CREATE TABLE marketplace.insurance_quotes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    buyer_id UUID NOT NULL REFERENCES marketplace.buyer_profiles(id),
    listing_id UUID NOT NULL REFERENCES marketplace.vehicle_listings(id),
    coverage_type INTEGER NOT NULL, -- 1: Liability, 2: Comprehensive, 3: Full Coverage
    coverage_details JSONB NOT NULL,
    quoted_premium DECIMAL(10,2) NOT NULL,
    insurance_provider VARCHAR(255) NOT NULL,
    quote_reference VARCHAR(255),
    valid_until TIMESTAMP WITH TIME ZONE NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Active, 2: Expired, 3: Purchased
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Purchase history
CREATE TABLE marketplace.purchase_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    buyer_id UUID NOT NULL REFERENCES marketplace.buyer_profiles(id),
    contract_id UUID NOT NULL REFERENCES marketplace.sales_contracts(id),
    vehicle_info JSONB NOT NULL, -- Vehicle details at time of purchase
    purchase_price DECIMAL(12,2) NOT NULL,
    financing_details JSONB, -- Loan information if financed
    insurance_details JSONB, -- Insurance information
    delivery_date TIMESTAMP WITH TIME ZONE,
    satisfaction_rating INTEGER, -- 1-5 stars
    review_text TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Marketplace analytics
CREATE TABLE marketplace.marketplace_analytics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    analysis_date DATE NOT NULL,
    total_listings INTEGER NOT NULL DEFAULT 0,
    active_listings INTEGER NOT NULL DEFAULT 0,
    total_dealers INTEGER NOT NULL DEFAULT 0,
    active_dealers INTEGER NOT NULL DEFAULT 0,
    total_sales INTEGER NOT NULL DEFAULT 0,
    total_revenue DECIMAL(15,2) NOT NULL DEFAULT 0,
    average_sale_price DECIMAL(12,2),
    average_days_to_sell INTEGER,
    conversion_rate DECIMAL(5,4), -- Lead to sale conversion
    market_metrics JSONB, -- Detailed market analysis
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, analysis_date)
);

-- Dealer performance tracking
CREATE TABLE marketplace.dealer_performance (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
## 🔄 Application Layer - CQRS Commands and Queries

### Marketplace Management Commands

```csharp
namespace DriveOps.Marketplace.Application.Commands
{
    // Dealer Management Commands
    public record RegisterDealerCommand(
        TenantId TenantId,
        string DealerName,
        string BusinessLicense,
        Address BusinessAddress,
        ContactInfo ContactInfo,
        DealerSubscriptionTier SubscriptionTier
    ) : ICommand<Result<MarketplaceDealerId>>;

    public record CreateDealerStoreCommand(
        TenantId TenantId,
        MarketplaceDealerId DealerId,
        string StoreName,
        StoreConfiguration Configuration,
        StoreTheme Theme
    ) : ICommand<Result<DealerStoreId>>;

    public record UpdateDealerCommissionRateCommand(
        TenantId TenantId,
        MarketplaceDealerId DealerId,
        decimal NewCommissionRate,
        UserId UpdatedBy,
        string Reason
    ) : ICommand<Result>;

    // Vehicle Listing Commands
    public record CreateVehicleListingCommand(
        TenantId TenantId,
        MarketplaceDealerId DealerId,
        VehicleId VehicleId,
        decimal Price,
        string Description,
        ListingType Type,
        bool IsFeatured = false
    ) : ICommand<Result<VehicleListingId>>;

    public record UpdateVehiclePriceCommand(
        TenantId TenantId,
        VehicleListingId ListingId,
        decimal NewPrice,
        PriceChangeReason Reason,
        UserId UpdatedBy
    ) : ICommand<Result>;

    public record AddVehiclePhotosCommand(
        TenantId TenantId,
        VehicleListingId ListingId,
        List<VehiclePhotoRequest> Photos
    ) : ICommand<Result>;

    public record MarkVehicleAsSoldCommand(
        TenantId TenantId,
        VehicleListingId ListingId,
        SalesContractId ContractId,
        UserId SoldBy
    ) : ICommand<Result>;

    // Sales and Lead Commands
    public record CreateSalesLeadCommand(
        TenantId TenantId,
        MarketplaceDealerId DealerId,
        VehicleListingId ListingId,
        BuyerProfile Buyer,
        LeadSource Source,
        decimal? OfferedPrice = null
    ) : ICommand<Result<SalesLeadId>>;

    public record AssignLeadToSalesPersonCommand(
        TenantId TenantId,
        SalesLeadId LeadId,
        UserId SalesPersonId,
        UserId AssignedBy
    ) : ICommand<Result>;

    public record CreateSalesContractCommand(
        TenantId TenantId,
        MarketplaceDealerId DealerId,
        VehicleListingId ListingId,
        SalesLeadId LeadId,
        BuyerProfile Buyer,
        decimal SalePrice,
        ContractTerms Terms
    ) : ICommand<Result<SalesContractId>>;

    public record ProcessPaymentCommand(
        TenantId TenantId,
        SalesContractId ContractId,
        decimal Amount,
        PaymentMethod Method,
        string? TransactionId = null
    ) : ICommand<Result<ContractPaymentId>>;

    // Consignment Commands
    public record CreateDepositSaleCommand(
        TenantId TenantId,
        MarketplaceDealerId DealerId,
        VehicleId VehicleId,
        VehicleOwnerProfile Owner,
        decimal ReservePrice,
        decimal CommissionRate,
        string? SpecialInstructions = null
    ) : ICommand<Result<DepositSaleId>>;
}

// Command Handlers
namespace DriveOps.Marketplace.Application.Handlers.Commands
{
    public class CreateVehicleListingHandler : ICommandHandler<CreateVehicleListingCommand, Result<VehicleListingId>>
    {
        private readonly IMarketplaceDealerRepository _dealerRepository;
        private readonly IVehicleService _vehicleService;
        private readonly IVehicleListingRepository _listingRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITenantContext _tenantContext;

        public async Task<Result<VehicleListingId>> Handle(CreateVehicleListingCommand request, CancellationToken cancellationToken)
        {
            // Validate dealer exists and is active
            var dealer = await _dealerRepository.GetByIdAsync(request.DealerId);
            if (dealer == null || dealer.Status != DealerStatus.Active)
                return Result.Failure<VehicleListingId>("Dealer not found or inactive");

            // Validate vehicle exists and belongs to tenant
            var vehicleExists = await _vehicleService.VehicleExistsAsync(request.TenantId, request.VehicleId);
            if (!vehicleExists)
                return Result.Failure<VehicleListingId>("Vehicle not found");

            // Create vehicle listing details
            var listingDetails = new VehicleListingDetails(
                request.Price,
                request.Description,
                request.Type,
                request.IsFeatured
            );

            // Create the listing
            dealer.ListVehicle(new Vehicle { Id = request.VehicleId }, listingDetails);

            await _dealerRepository.UpdateAsync(dealer);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var newListing = dealer.Listings.OrderByDescending(l => l.ListedAt).First();
            return Result.Success(newListing.Id);
        }
    }

    public class ProcessSalesTransactionHandler : ICommandHandler<CreateSalesContractCommand, Result<SalesContractId>>
    {
        private readonly IMarketplaceDealerRepository _dealerRepository;
        private readonly ISalesContractRepository _contractRepository;
        private readonly ICommissionCalculationService _commissionService;
        private readonly IPaymentProcessor _paymentProcessor;
        private readonly IUnitOfWork _unitOfWork;

        public async Task<Result<SalesContractId>> Handle(CreateSalesContractCommand request, CancellationToken cancellationToken)
        {
            var dealer = await _dealerRepository.GetByIdAsync(request.DealerId);
            if (dealer == null)
### Marketplace Search and Analytics Queries

```csharp
namespace DriveOps.Marketplace.Application.Queries
{
    // Vehicle Search Queries
    public record SearchVehiclesQuery(
        TenantId TenantId,
        VehicleSearchCriteria Criteria,
        int Page = 1,
        int PageSize = 20
    ) : IQuery<Result<PagedResult<VehicleListingDto>>>;

    public record GetVehicleListingDetailsQuery(
        TenantId TenantId,
        VehicleListingId ListingId,
        UserId? ViewerId = null
    ) : IQuery<Result<VehicleListingDetailsDto>>;

    public record GetDealerInventoryQuery(
        TenantId TenantId,
        MarketplaceDealerId DealerId,
        InventoryFilter Filter,
        int Page = 1,
        int PageSize = 20
    ) : IQuery<Result<PagedResult<VehicleListingDto>>>;

    // Lead Management Queries
    public record GetDealerLeadsQuery(
        TenantId TenantId,
        MarketplaceDealerId DealerId,
        LeadFilter Filter,
        int Page = 1,
        int PageSize = 20
    ) : IQuery<Result<PagedResult<SalesLeadDto>>>;

    public record GetLeadDetailsQuery(
        TenantId TenantId,
        SalesLeadId LeadId
    ) : IQuery<Result<SalesLeadDetailsDto>>;

    // Analytics Queries
    public record GetMarketplaceAnalyticsQuery(
        TenantId TenantId,
        DateTime FromDate,
        DateTime ToDate,
        AnalyticsGranularity Granularity = AnalyticsGranularity.Daily
    ) : IQuery<Result<MarketplaceAnalyticsDto>>;

    public record GetDealerPerformanceQuery(
        TenantId TenantId,
        MarketplaceDealerId DealerId,
        DateTime FromDate,
        DateTime ToDate
    ) : IQuery<Result<DealerPerformanceDto>>;

    public record GetInventoryMetricsQuery(
        TenantId TenantId,
        MarketplaceDealerId? DealerId = null,
        DateTime? AsOfDate = null
    ) : IQuery<Result<InventoryMetricsDto>>;

    // Commission and Financial Queries
    public record GetCommissionReportQuery(
        TenantId TenantId,
        MarketplaceDealerId? DealerId = null,
        DateTime FromDate,
        DateTime ToDate
    ) : IQuery<Result<CommissionReportDto>>;
}

// Query Handlers with Elasticsearch Integration
namespace DriveOps.Marketplace.Application.Handlers.Queries
{
    public class SearchVehiclesHandler : IQueryHandler<SearchVehiclesQuery, Result<PagedResult<VehicleListingDto>>>
    {
        private readonly IElasticsearchClient _elasticsearchClient;
        private readonly IVehicleListingRepository _listingRepository;
        private readonly IMapper _mapper;

        public async Task<Result<PagedResult<VehicleListingDto>>> Handle(SearchVehiclesQuery request, CancellationToken cancellationToken)
        {
            var searchRequest = new SearchRequest<VehicleListingDocument>("vehicle-listings")
            {
                Query = BuildSearchQuery(request.Criteria),
                Sort = BuildSortOptions(request.Criteria.SortBy),
                From = (request.Page - 1) * request.PageSize,
                Size = request.PageSize,
                Highlight = new Highlight
                {
                    Fields = new Dictionary<string, IHighlightField>
                    {
                        ["description"] = new HighlightField(),
                        ["vehicle.make"] = new HighlightField(),
                        ["vehicle.model"] = new HighlightField()
                    }
                }
            };

            var response = await _elasticsearchClient.SearchAsync<VehicleListingDocument>(searchRequest, cancellationToken);

            if (!response.IsValid)
                return Result.Failure<PagedResult<VehicleListingDto>>("Search service unavailable");

            var listings = response.Documents.Select(doc => _mapper.Map<VehicleListingDto>(doc)).ToList();
            
            var pagedResult = new PagedResult<VehicleListingDto>(
                listings,
                (int)response.Total,
                request.Page,
                request.PageSize
            );

            return Result.Success(pagedResult);
        }

        private QueryContainer BuildSearchQuery(VehicleSearchCriteria criteria)
        {
            var queries = new List<QueryContainer>();

            // Text search
            if (!string.IsNullOrEmpty(criteria.SearchText))
            {
                queries.Add(new MultiMatchQuery
                {
                    Query = criteria.SearchText,
                    Fields = new[] { "vehicle.make^2", "vehicle.model^2", "description", "dealer.name" }
                });
            }

            // Price range
            if (criteria.MinPrice.HasValue || criteria.MaxPrice.HasValue)
            {
                queries.Add(new RangeQuery
                {
                    Field = "price",
                    GreaterThanOrEqualTo = criteria.MinPrice,
                    LessThanOrEqualTo = criteria.MaxPrice
                });
            }

            // Year range
            if (criteria.MinYear.HasValue || criteria.MaxYear.HasValue)
            {
                queries.Add(new RangeQuery
                {
                    Field = "vehicle.year",
                    GreaterThanOrEqualTo = criteria.MinYear,
                    LessThanOrEqualTo = criteria.MaxYear
                });
            }

            // Brand filter
            if (criteria.BrandIds?.Any() == true)
            {
                queries.Add(new TermsQuery
                {
                    Field = "vehicle.brandId",
                    Terms = criteria.BrandIds.Cast<object>()
                });
            }

            // Location filter
            if (criteria.Location != null && criteria.RadiusKm > 0)
            {
                queries.Add(new GeoDistanceQuery
                {
                    Field = "dealer.location",
                    Location = new GeoLocation(criteria.Location.Latitude, criteria.Location.Longitude),
                    Distance = $"{criteria.RadiusKm}km"
                });
            }

            // Status filter (only active listings)
            queries.Add(new TermQuery
            {
## 🏗️ Infrastructure Layer

### Payment Processing Integration

```csharp
namespace DriveOps.Marketplace.Infrastructure.Payment
{
    public interface IPaymentProcessor
    {
        Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request);
        Task<PaymentResult> ProcessPartialPaymentAsync(PartialPaymentRequest request);
        Task<RefundResult> RefundPaymentAsync(RefundRequest request);
        Task<PaymentResult> ProcessCommissionPayoutAsync(CommissionPayoutRequest request);
    }

    public class StripePaymentProcessor : IPaymentProcessor
    {
        private readonly StripeClient _stripeClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StripePaymentProcessor> _logger;

        public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
        {
            try
            {
                var options = new ChargeCreateOptions
                {
                    Amount = (long)(request.Amount * 100), // Convert to cents
                    Currency = "eur",
                    Source = request.PaymentToken,
                    Description = $"Vehicle purchase - Contract {request.ContractId}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["contract_id"] = request.ContractId.ToString(),
                        ["dealer_id"] = request.DealerId.ToString(),
                        ["tenant_id"] = request.TenantId.ToString()
                    }
                };

                var service = new ChargeService(_stripeClient);
                var charge = await service.CreateAsync(options);

                return new PaymentResult
                {
                    IsSuccess = charge.Status == "succeeded",
                    TransactionId = charge.Id,
                    Amount = request.Amount,
                    ProcessedAt = DateTime.UtcNow,
                    ProcessorResponse = charge.Outcome?.SellerMessage
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe payment processing failed for contract {ContractId}", request.ContractId);
                return new PaymentResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ErrorCode = ex.StripeError?.Code
                };
            }
        }

        public async Task<PaymentResult> ProcessCommissionPayoutAsync(CommissionPayoutRequest request)
        {
            try
            {
                var transferOptions = new TransferCreateOptions
                {
                    Amount = (long)(request.Amount * 100),
                    Currency = "eur",
                    Destination = request.DealerStripeAccountId,
                    Description = $"Commission payout for sales in period {request.PeriodStart:yyyy-MM-dd} to {request.PeriodEnd:yyyy-MM-dd}",
                    Metadata = new Dictionary<string, string>
                    {
                        ["dealer_id"] = request.DealerId.ToString(),
                        ["payout_period"] = $"{request.PeriodStart:yyyy-MM-dd}_{request.PeriodEnd:yyyy-MM-dd}"
                    }
                };

                var service = new TransferService(_stripeClient);
                var transfer = await service.CreateAsync(transferOptions);

                return new PaymentResult
                {
                    IsSuccess = true,
                    TransactionId = transfer.Id,
                    Amount = request.Amount,
                    ProcessedAt = DateTime.UtcNow
                };
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Commission payout failed for dealer {DealerId}", request.DealerId);
                return new PaymentResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ErrorCode = ex.StripeError?.Code
                };
            }
        }
    }
}
```

### Search Infrastructure with Elasticsearch

```csharp
namespace DriveOps.Marketplace.Infrastructure.Search
{
    public class VehicleSearchService : IVehicleSearchService
    {
        private readonly IElasticsearchClient _elasticsearchClient;
        private readonly IMapper _mapper;
        private readonly ILogger<VehicleSearchService> _logger;

        public async Task<SearchResult<VehicleListingDto>> SearchVehiclesAsync(VehicleSearchCriteria criteria)
        {
            var searchRequest = new SearchRequest<VehicleListingDocument>("vehicle-listings")
            {
                Query = BuildAdvancedQuery(criteria),
                Aggregations = BuildAggregations(),
                Sort = BuildSortOptions(criteria.SortBy),
                From = (criteria.Page - 1) * criteria.PageSize,
                Size = criteria.PageSize
            };

            var response = await _elasticsearchClient.SearchAsync<VehicleListingDocument>(searchRequest);

            if (!response.IsValid)
            {
                _logger.LogError("Elasticsearch search failed: {Error}", response.ServerError);
                throw new SearchServiceException("Vehicle search service unavailable");
            }

            return new SearchResult<VehicleListingDto>
            {
                Items = response.Documents.Select(doc => _mapper.Map<VehicleListingDto>(doc)).ToList(),
                TotalCount = (int)response.Total,
                Facets = ExtractFacetsFromAggregations(response.Aggregations),
                SuggestedFilters = BuildSuggestedFilters(response.Aggregations),
                SearchTime = response.Took
            };
        }

        private QueryContainer BuildAdvancedQuery(VehicleSearchCriteria criteria)
        {
            var shouldQueries = new List<QueryContainer>();
            var mustQueries = new List<QueryContainer>();
            var filterQueries = new List<QueryContainer>();

            // Fuzzy text search with boosting
            if (!string.IsNullOrEmpty(criteria.SearchText))
            {
                shouldQueries.Add(new MultiMatchQuery
                {
                    Query = criteria.SearchText,
                    Fields = new[] 
                    { 
                        "vehicle.make^3", 
                        "vehicle.model^3", 
                        "description^1", 
                        "dealer.name^2" 
                    },
                    Type = TextQueryType.BestFields,
                    Fuzziness = Fuzziness.Auto
                });

                shouldQueries.Add(new MatchQuery
                {
                    Field = "vehicle.features",
                    Query = criteria.SearchText,
                    Boost = 1.5
                });
            }

            // Price range with boost for good deals
            if (criteria.MinPrice.HasValue || criteria.MaxPrice.HasValue)
            {
                filterQueries.Add(new RangeQuery
                {
                    Field = "price",
                    GreaterThanOrEqualTo = criteria.MinPrice,
                    LessThanOrEqualTo = criteria.MaxPrice
                });
            }
### External Integrations

```csharp
namespace DriveOps.Marketplace.Infrastructure.External
{
    // Vehicle Valuation API Integration
    public class KelleyBlueBookService : IVehicleValuationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public async Task<VehicleValuation> GetVehicleValuationAsync(VehicleValuationRequest request)
        {
            var apiRequest = new
            {
                vin = request.Vin,
                mileage = request.Mileage,
                condition = request.Condition,
                zip = request.ZipCode
            };

            var response = await _httpClient.PostAsJsonAsync("/api/v1/valuation", apiRequest);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<KbbValuationResponse>();

            return new VehicleValuation
            {
                EstimatedValue = result.TradeInValue,
                MarketValue = result.PrivatePartyValue,
                RetailValue = result.SuggestedRetailValue,
                Source = ValuationSource.KelleyBlueBook,
                ValuationDate = DateTime.UtcNow
            };
        }
    }

    // Financing Integration
    public class FinancingPartnerService : IFinancingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FinancingPartnerService> _logger;

        public async Task<FinancingPreApprovalResult> ProcessPreApprovalAsync(FinancingApplication application)
        {
            try
            {
                var request = new
                {
                    applicant = new
                    {
                        firstName = application.Buyer.FirstName,
                        lastName = application.Buyer.LastName,
                        email = application.Buyer.Email,
                        ssn = application.EmploymentInfo.SSN,
                        annualIncome = application.FinancialInfo.AnnualIncome,
                        employmentStatus = application.EmploymentInfo.Status
                    },
                    loan = new
                    {
                        amount = application.LoanAmount,
                        term = application.DesiredTermMonths,
                        vehicleYear = application.VehicleInfo.Year,
                        vehicleValue = application.VehicleInfo.EstimatedValue
                    }
                };

                var response = await _httpClient.PostAsJsonAsync("/api/preapproval", request);
                var result = await response.Content.ReadFromJsonAsync<FinancingResponse>();

                return new FinancingPreApprovalResult
                {
                    IsApproved = result.Decision == "APPROVED",
                    ApprovedAmount = result.ApprovedAmount,
                    InterestRate = result.InterestRate,
                    TermMonths = result.TermMonths,
                    MonthlyPayment = result.MonthlyPayment,
                    ReferenceNumber = result.ReferenceNumber
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Financing pre-approval failed for application {ApplicationId}", application.Id);
                throw new FinancingServiceException("Pre-approval service unavailable", ex);
            }
        }
    }

    // Insurance Quote Integration
    public class InsuranceQuoteService : IInsuranceQuoteService
    {
        public async Task<List<InsuranceQuote>> GetQuotesAsync(InsuranceQuoteRequest request)
        {
            var quotes = new List<InsuranceQuote>();
## 🌐 API Controllers and GraphQL Services

### REST API Controllers

```csharp
namespace DriveOps.Marketplace.Presentation.Controllers
{
    [ApiController]
    [Route("api/marketplace/v1/[controller]")]
    [Authorize]
    public class VehicleListingsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ITenantContext _tenantContext;

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<VehicleListingDto>>> SearchVehicles(
            [FromQuery] VehicleSearchRequest request)
        {
            var query = new SearchVehiclesQuery(
                _tenantContext.TenantId,
                request.ToCriteria(),
                request.Page,
                request.PageSize
            );

            var result = await _mediator.Send(query);
            return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<VehicleListingDetailsDto>> GetVehicleDetails(Guid id)
        {
            var query = new GetVehicleListingDetailsQuery(
                _tenantContext.TenantId,
                new VehicleListingId(id),
                User.GetUserId()
            );

            var result = await _mediator.Send(query);
            return result.IsSuccess ? Ok(result.Value) : NotFound();
        }

        [HttpPost]
        [Authorize(Policy = "DealerOnly")]
        public async Task<ActionResult<VehicleListingDto>> CreateListing(CreateVehicleListingRequest request)
        {
            var command = new CreateVehicleListingCommand(
                _tenantContext.TenantId,
                new MarketplaceDealerId(request.DealerId),
                new VehicleId(request.VehicleId),
                request.Price,
                request.Description,
                request.Type,
                request.IsFeatured
            );

            var result = await _mediator.Send(command);
            return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
        }

        [HttpPut("{id}/price")]
        [Authorize(Policy = "DealerOnly")]
        public async Task<ActionResult> UpdatePrice(Guid id, UpdatePriceRequest request)
        {
            var command = new UpdateVehiclePriceCommand(
                _tenantContext.TenantId,
                new VehicleListingId(id),
                request.NewPrice,
                request.Reason,
                User.GetUserId()
            );

            var result = await _mediator.Send(command);
            return result.IsSuccess ? NoContent() : BadRequest(result.Error);
        }

        [HttpPost("{id}/photos")]
        [Authorize(Policy = "DealerOnly")]
        public async Task<ActionResult> AddPhotos(Guid id, IFormFileCollection photos)
        {
            var photoRequests = new List<VehiclePhotoRequest>();
            
            for (int i = 0; i < photos.Count; i++)
            {
                var photo = photos[i];
                var fileResult = await _fileService.UploadFileAsync(photo, "vehicle-photos");
                
                photoRequests.Add(new VehiclePhotoRequest
                {
                    FileId = fileResult.FileId,
                    DisplayOrder = i,
                    IsPrimary = i == 0
                });
            }

            var command = new AddVehiclePhotosCommand(
                _tenantContext.TenantId,
                new VehicleListingId(id),
                photoRequests
            );

            var result = await _mediator.Send(command);
            return result.IsSuccess ? NoContent() : BadRequest(result.Error);
        }

        [HttpPost("{id}/sold")]
        [Authorize(Policy = "DealerOnly")]
        public async Task<ActionResult> MarkAsSold(Guid id, MarkAsSoldRequest request)
        {
            var command = new MarkVehicleAsSoldCommand(
                _tenantContext.TenantId,
                new VehicleListingId(id),
                new SalesContractId(request.ContractId),
                User.GetUserId()
            );

            var result = await _mediator.Send(command);
            return result.IsSuccess ? NoContent() : BadRequest(result.Error);
        }
    }

    [ApiController]
    [Route("api/marketplace/v1/[controller]")]
    [Authorize(Policy = "DealerOnly")]
    public class SalesLeadsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ITenantContext _tenantContext;

        [HttpGet]
        public async Task<ActionResult<PagedResult<SalesLeadDto>>> GetLeads(
            [FromQuery] GetLeadsRequest request)
        {
            var query = new GetDealerLeadsQuery(
                _tenantContext.TenantContext,
                User.GetDealerId(),
                request.ToFilter(),
                request.Page,
                request.PageSize
            );

            var result = await _mediator.Send(query);
            return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
        }

        [HttpPost]
        public async Task<ActionResult<SalesLeadDto>> CreateLead(CreateSalesLeadRequest request)
        {
            var command = new CreateSalesLeadCommand(
                _tenantContext.TenantId,
                User.GetDealerId(),
                new VehicleListingId(request.ListingId),
                request.Buyer,
                request.Source,
                request.OfferedPrice
            );

            var result = await _mediator.Send(command);
            return result.IsSuccess ? CreatedAtAction(nameof(GetLead), new { id = result.Value }, result.Value) 
                : BadRequest(result.Error);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SalesLeadDetailsDto>> GetLead(Guid id)
        {
            var query = new GetLeadDetailsQuery(_tenantContext.TenantId, new SalesLeadId(id));
            var result = await _mediator.Send(query);
            return result.IsSuccess ? Ok(result.Value) : NotFound();
        }

        [HttpPut("{id}/assign")]
        public async Task<ActionResult> AssignLead(Guid id, AssignLeadRequest request)
        {
            var command = new AssignLeadToSalesPersonCommand(
                _tenantContext.TenantId,
                new SalesLeadId(id),
                new UserId(request.SalesPersonId),
                User.GetUserId()
            );
### GraphQL Services

```csharp
namespace DriveOps.Marketplace.Presentation.GraphQL
{
    [ExtendObjectType<Query>]
    public class MarketplaceQueries
    {
        [UsePaging]
        [UseFiltering]
        [UseSorting]
        public async Task<IQueryable<VehicleListingDto>> GetVehicleListings(
            [Service] IMediator mediator,
            [Service] ITenantContext tenantContext,
            VehicleSearchCriteria? criteria = null)
        {
            var query = new SearchVehiclesQuery(
                tenantContext.TenantId,
                criteria ?? new VehicleSearchCriteria(),
                1,
                1000
            );

            var result = await mediator.Send(query);
            return result.Value.Items.AsQueryable();
        }

        public async Task<VehicleListingDetailsDto?> GetVehicleListing(
            Guid id,
            [Service] IMediator mediator,
            [Service] ITenantContext tenantContext)
        {
            var query = new GetVehicleListingDetailsQuery(
                tenantContext.TenantId,
                new VehicleListingId(id)
            );

            var result = await mediator.Send(query);
            return result.IsSuccess ? result.Value : null;
        }

        [Authorize(Policy = "DealerOnly")]
        public async Task<DealerPerformanceDto?> GetDealerPerformance(
            DateTime fromDate,
            DateTime toDate,
            [Service] IMediator mediator,
            [Service] ITenantContext tenantContext,
            ClaimsPrincipal claimsPrincipal)
        {
            var dealerId = claimsPrincipal.GetDealerId();
            var query = new GetDealerPerformanceQuery(
                tenantContext.TenantId,
                dealerId,
                fromDate,
                toDate
            );

            var result = await mediator.Send(query);
            return result.IsSuccess ? result.Value : null;
        }

        [Authorize(Policy = "AdminOnly")]
        public async Task<MarketplaceAnalyticsDto?> GetMarketplaceAnalytics(
            DateTime fromDate,
            DateTime toDate,
            AnalyticsGranularity granularity,
            [Service] IMediator mediator,
            [Service] ITenantContext tenantContext)
        {
            var query = new GetMarketplaceAnalyticsQuery(
                tenantContext.TenantId,
                fromDate,
                toDate,
                granularity
            );

            var result = await mediator.Send(query);
            return result.IsSuccess ? result.Value : null;
        }
    }

    [ExtendObjectType<Mutation>]
    public class MarketplaceMutations
    {
        [Authorize(Policy = "DealerOnly")]
        public async Task<VehicleListingDto?> CreateVehicleListing(
            CreateVehicleListingInput input,
            [Service] IMediator mediator,
            [Service] ITenantContext tenantContext,
            ClaimsPrincipal claimsPrincipal)
        {
            var command = new CreateVehicleListingCommand(
                tenantContext.TenantId,
                claimsPrincipal.GetDealerId(),
                new VehicleId(input.VehicleId),
                input.Price,
                input.Description,
                input.Type,
                input.IsFeatured
            );

            var result = await mediator.Send(command);
            if (!result.IsSuccess)
                throw new GraphQLException(result.Error);

            // Return the created listing
            var query = new GetVehicleListingDetailsQuery(tenantContext.TenantId, result.Value);
            var listingResult = await mediator.Send(query);
            return listingResult.Value;
        }

        [Authorize(Policy = "DealerOnly")]
        public async Task<SalesLeadDto?> CreateSalesLead(
            CreateSalesLeadInput input,
            [Service] IMediator mediator,
            [Service] ITenantContext tenantContext,
            ClaimsPrincipal claimsPrincipal)
        {
            var command = new CreateSalesLeadCommand(
                tenantContext.TenantId,
                claimsPrincipal.GetDealerId(),
                new VehicleListingId(input.ListingId),
                input.Buyer,
                input.Source,
                input.OfferedPrice
            );

            var result = await mediator.Send(command);
            if (!result.IsSuccess)
                throw new GraphQLException(result.Error);

            // Return the created lead
            var query = new GetLeadDetailsQuery(tenantContext.TenantId, result.Value);
            var leadResult = await mediator.Send(query);
            return leadResult.Value?.ToDto();
        }

        public async Task<VehicleInquiryResponseDto?> SubmitVehicleInquiry(
            VehicleInquiryInput input,
            [Service] IMediator mediator,
            [Service] ITenantContext tenantContext)
        {
            var command = new SubmitVehicleInquiryCommand(
                tenantContext.TenantId,
                new VehicleListingId(input.ListingId),
                input.Buyer,
                input.Message,
                input.InquiryType
            );

            var result = await mediator.Send(command);
            if (!result.IsSuccess)
                throw new GraphQLException(result.Error);

            return new VehicleInquiryResponseDto
            {
                InquiryId = result.Value,
                Status = "Submitted",
                Message = "Your inquiry has been submitted successfully. The dealer will contact you soon."
            };
        }
    }

    // GraphQL Subscriptions for Real-time Updates
    [ExtendObjectType<Subscription>]
    public class MarketplaceSubscriptions
    {
        [Subscribe]
        [Authorize(Policy = "DealerOnly")]
        public VehicleListingDto OnVehicleListingUpdated(
            [EventMessage] VehicleListingUpdatedEvent eventMessage,
            ClaimsPrincipal claimsPrincipal)
        {
            // Filter by dealer to ensure dealers only receive updates for their own listings
            var dealerId = claimsPrincipal.GetDealerId();
            if (eventMessage.DealerId != dealerId)
                throw new GraphQLException("Unauthorized");

            return eventMessage.UpdatedListing;
        }

        [Subscribe]
        [Authorize(Policy = "DealerOnly")]
        public SalesLeadDto OnNewSalesLead(
            [EventMessage] NewSalesLeadEvent eventMessage,
            ClaimsPrincipal claimsPrincipal)
        {
            var dealerId = claimsPrincipal.GetDealerId();
            if (eventMessage.DealerId != dealerId)
                throw new GraphQLException("Unauthorized");

            return eventMessage.NewLead;
        }

        [Subscribe]
        [Topic("marketplace_alerts")]
        public MarketplaceAlertDto OnMarketplaceAlert([EventMessage] MarketplaceAlert alert)
        {
            return new MarketplaceAlertDto
            {
                Type = alert.Type,
                Message = alert.Message,
                Timestamp = alert.Timestamp,
                Severity = alert.Severity
            };
        }
    }
}
```

## 📱 Mobile Applications

### Buyer Mobile App (React Native)

```typescript
// VehicleSearchScreen.tsx
import React, { useState, useCallback } from 'react';
import { 
  ScrollView, 
  View, 
  Text, 
  TextInput, 
  TouchableOpacity, 
  FlatList,
  Image,
  RefreshControl 
} from 'react-native';
import { useQuery, useMutation } from '@apollo/client';
import { 
  SEARCH_VEHICLES_QUERY, 
  SUBMIT_VEHICLE_INQUIRY_MUTATION,
  SAVE_VEHICLE_SEARCH_MUTATION 
} from '../graphql/queries';

interface VehicleSearchScreenProps {
  navigation: any;
}

export const VehicleSearchScreen: React.FC<VehicleSearchScreenProps> = ({ navigation }) => {
  const [searchCriteria, setSearchCriteria] = useState({
    searchText: '',
    minPrice: '',
    maxPrice: '',
    brandIds: [],
    location: null,
    radiusKm: 50
  });

  const { data, loading, error, refetch } = useQuery(SEARCH_VEHICLES_QUERY, {
    variables: { criteria: searchCriteria },
    fetchPolicy: 'cache-and-network'
  });

  const [submitInquiry] = useMutation(SUBMIT_VEHICLE_INQUIRY_MUTATION);
  const [saveSearch] = useMutation(SAVE_VEHICLE_SEARCH_MUTATION);

  const handleVehiclePress = useCallback((vehicle: VehicleListing) => {
    navigation.navigate('VehicleDetails', { vehicleId: vehicle.id });
  }, [navigation]);

  const handleInquiry = useCallback(async (vehicleId: string) => {
    try {
      await submitInquiry({
        variables: {
          input: {
            listingId: vehicleId,
            buyer: {
              firstName: 'John',
              lastName: 'Doe',
              email: 'john@example.com',
              phone: '+1234567890'
            },
            message: 'I am interested in this vehicle. Please contact me.',
            inquiryType: 'GENERAL_INQUIRY'
          }
        }
      });
      
      Alert.alert('Success', 'Your inquiry has been submitted!');
    } catch (error) {
      Alert.alert('Error', 'Failed to submit inquiry');
    }
  }, [submitInquiry]);

  const renderVehicleItem = ({ item }: { item: VehicleListing }) => (
    <TouchableOpacity 
      style={styles.vehicleCard}
      onPress={() => handleVehiclePress(item)}
    >
      <Image source={{ uri: item.primaryPhoto?.url }} style={styles.vehicleImage} />
      <View style={styles.vehicleInfo}>
        <Text style={styles.vehicleTitle}>
          {item.vehicle.year} {item.vehicle.make} {item.vehicle.model}
        </Text>
        <Text style={styles.vehiclePrice}>€{item.price.toLocaleString()}</Text>
        <Text style={styles.vehicleMileage}>{item.vehicle.mileage.toLocaleString()} km</Text>
        <Text style={styles.dealerName}>{item.dealer.name}</Text>
        <TouchableOpacity 
          style={styles.inquiryButton}
          onPress={() => handleInquiry(item.id)}
        >
          <Text style={styles.inquiryButtonText}>Inquire</Text>
        </TouchableOpacity>
      </View>
    </TouchableOpacity>
  );

  return (
    <View style={styles.container}>
      <View style={styles.searchBar}>
        <TextInput
          style={styles.searchInput}
          placeholder="Search vehicles..."
          value={searchCriteria.searchText}
          onChangeText={(text) => setSearchCriteria(prev => ({ ...prev, searchText: text }))}
        />
        <TouchableOpacity style={styles.filterButton}>
          <Text>Filters</Text>
## 🔍 SEO and Marketing Automation

### SEO Optimization Service

```csharp
namespace DriveOps.Marketplace.Infrastructure.SEO
{
    public class VehicleListingSeoService : IVehicleListingSeoService
    {
        private readonly ISeoTemplateService _templateService;
        private readonly IVehicleValuationService _valuationService;

        public async Task<SeoMetadata> GenerateSeoMetadataAsync(VehicleListingId listingId)
        {
            var listing = await _listingRepository.GetByIdWithDetailsAsync(listingId);
            
            var vehicle = listing.Vehicle;
            var dealer = listing.Dealer;
            
            // Generate dynamic SEO title
            var title = _templateService.GenerateTitle(new
            {
                Year = vehicle.Year,
                Make = vehicle.Brand.Name,
                Model = vehicle.Model,
                Price = listing.Price,
                Location = dealer.BusinessAddress.City
            });

            // Generate rich description
            var description = _templateService.GenerateDescription(new
            {
                Vehicle = vehicle,
                Price = listing.Price,
                Dealer = dealer.DealerName,
                Features = listing.Description,
                Mileage = vehicle.Mileage
            });

            // Generate keywords
            var keywords = GenerateKeywords(vehicle, listing, dealer);

            // Generate Schema.org structured data
            var structuredData = GenerateVehicleSchema(listing);

            return new SeoMetadata
            {
                Title = title,
                Description = description,
                Keywords = keywords,
                StructuredData = structuredData,
                CanonicalUrl = $"/vehicles/{listing.Id}",
                OpenGraphImage = listing.Photos.FirstOrDefault(p => p.IsPrimary)?.Url
            };
        }

        private VehicleSchema GenerateVehicleSchema(VehicleListing listing)
        {
            return new VehicleSchema
            {
                Type = "Vehicle",
                Name = $"{listing.Vehicle.Year} {listing.Vehicle.Brand.Name} {listing.Vehicle.Model}",
                Description = listing.Description,
                Price = new PriceSchema
                {
                    Currency = "EUR",
                    Value = listing.Price
                },
                Offers = new OfferSchema
                {
                    Type = "Offer",
                    Price = listing.Price,
                    PriceCurrency = "EUR",
                    Availability = "InStock",
                    Seller = new OrganizationSchema
                    {
                        Type = "Organization",
                        Name = listing.Dealer.DealerName,
                        Address = listing.Dealer.BusinessAddress
                    }
                },
                Vehicle = new VehicleDetailsSchema
                {
                    Make = listing.Vehicle.Brand.Name,
                    Model = listing.Vehicle.Model,
                    ModelYear = listing.Vehicle.Year,
                    Mileage = listing.Vehicle.Mileage,
                    Color = listing.Vehicle.Color,
                    FuelType = listing.Vehicle.FuelType,
                    Transmission = listing.Vehicle.Transmission,
                    BodyType = listing.Vehicle.BodyType
                }
            };
        }
    }

    public class MarketplaceSitemapService : ISitemapService
    {
        public async Task<string> GenerateSitemapAsync(TenantId tenantId)
        {
            var listings = await _listingRepository.GetActiveListingsAsync(tenantId);
            var dealers = await _dealerRepository.GetActiveDealersAsync(tenantId);

            var sitemap = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement("urlset",
                    new XAttribute("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9"),
                    
                    // Homepage
                    new XElement("url",
                        new XElement("loc", "https://marketplace.driveops.com/"),
                        new XElement("changefreq", "daily"),
                        new XElement("priority", "1.0")
                    ),

                    // Vehicle listings
                    listings.Select(listing => new XElement("url",
                        new XElement("loc", $"https://marketplace.driveops.com/vehicles/{listing.Id}"),
                        new XElement("lastmod", listing.UpdatedAt.ToString("yyyy-MM-dd")),
                        new XElement("changefreq", "weekly"),
                        new XElement("priority", "0.8")
                    )),

                    // Dealer pages
                    dealers.Select(dealer => new XElement("url",
                        new XElement("loc", $"https://marketplace.driveops.com/dealers/{dealer.Id}"),
                        new XElement("lastmod", dealer.UpdatedAt.ToString("yyyy-MM-dd")),
                        new XElement("changefreq", "weekly"),
                        new XElement("priority", "0.6")
                    ))
                )
            );

            return sitemap.ToString();
        }
    }
}
```

### Marketing Automation

```csharp
namespace DriveOps.Marketplace.Infrastructure.Marketing
{
    public class MarketingAutomationService : IMarketingAutomationService
    {
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly ISocialMediaService _socialMediaService;
        private readonly IBuyerSegmentationService _segmentationService;

        public async Task ProcessNewVehicleListingAsync(VehicleListingId listingId)
        {
            var listing = await _listingRepository.GetByIdWithDetailsAsync(listingId);
            
            // 1. Auto-post to social media
            await PostToSocialMediaAsync(listing);
            
            // 2. Notify relevant saved searches
            await NotifyMatchingSavedSearchesAsync(listing);
            
            // 3. Send to remarketing audiences
            await AddToRemarketingCampaignsAsync(listing);
            
            // 4. Generate SEO-optimized content
            await GenerateVehicleContentAsync(listing);
        }

        private async Task NotifyMatchingSavedSearchesAsync(VehicleListing listing)
        {
            var matchingSearches = await _savedSearchRepository.FindMatchingSearchesAsync(listing);
            
            foreach (var search in matchingSearches)
            {
                var buyer = search.Buyer;
                var emailTemplate = await _templateService.GetTemplateAsync("new_vehicle_match");
                
                var emailContent = emailTemplate.Render(new
                {
                    BuyerName = buyer.FirstName,
                    Vehicle = listing.Vehicle,
                    Price = listing.Price,
                    DealerName = listing.Dealer.DealerName,
                    ListingUrl = $"https://marketplace.driveops.com/vehicles/{listing.Id}",
                    Photos = listing.Photos.Take(3).Select(p => p.Url)
                });

                await _emailService.SendEmailAsync(new EmailRequest
                {
                    To = buyer.Email,
                    Subject = $"New {listing.Vehicle.Brand.Name} {listing.Vehicle.Model} matches your search!",
                    HtmlContent = emailContent,
                    Categories = new[] { "saved_search_match", "vehicle_alert" }
                });

                // Track email campaign metrics
                await _analyticsService.TrackEmailCampaignAsync(new EmailCampaignMetric
                {
                    CampaignType = "SavedSearchMatch",
                    BuyerId = buyer.Id,
                    VehicleListingId = listing.Id,
## ⚡ Performance and Scalability Considerations

### High-Performance Search Architecture

```csharp
namespace DriveOps.Marketplace.Infrastructure.Performance
{
    public class VehicleSearchOptimizationService : IVehicleSearchOptimizationService
    {
        private readonly IElasticsearchClient _elasticsearchClient;
        private readonly IDistributedCache _cache;
        private readonly ILogger<VehicleSearchOptimizationService> _logger;

        public async Task OptimizeSearchIndexAsync()
        {
            // 1. Optimize Elasticsearch index settings
            await _elasticsearchClient.Indices.PutSettingsAsync("vehicle-listings", new PutIndexSettingsRequest
            {
                IndexSettings = new IndexSettings
                {
                    RefreshInterval = "30s", // Reduce refresh frequency for bulk operations
                    NumberOfReplicas = 2, // Ensure high availability
                    Translog = new TranslogSettings
                    {
                        SyncInterval = "5s",
                        DurabilityMode = DurabilityMode.Request
                    }
                }
            });

            // 2. Create optimized field mappings
            await _elasticsearchClient.Indices.PutMappingAsync<VehicleListingDocument>("vehicle-listings", m => m
                .Properties(p => p
                    .Text(t => t.Name(n => n.Description).Analyzer("standard"))
                    .Keyword(k => k.Name(n => n.Vehicle.Make))
                    .Keyword(k => k.Name(n => n.Vehicle.Model))
                    .Number(n => n.Name(nn => nn.Price).Type(NumberType.Double))
                    .Number(n => n.Name(nn => nn.Vehicle.Year).Type(NumberType.Integer))
                    .GeoPoint(g => g.Name(n => n.Dealer.Location))
                    .Date(d => d.Name(n => n.ListedAt))
                )
            );

            // 3. Implement search result caching strategy
            await ImplementSearchCachingAsync();
        }

        private async Task ImplementSearchCachingAsync()
        {
            // Cache popular search results for 15 minutes
            var popularSearchCriteria = await GetPopularSearchCriteriaAsync();
            
            foreach (var criteria in popularSearchCriteria)
            {
                var cacheKey = GenerateSearchCacheKey(criteria);
                var cachedResult = await _cache.GetStringAsync(cacheKey);
                
                if (string.IsNullOrEmpty(cachedResult))
                {
                    var searchResult = await PerformSearchAsync(criteria);
                    var serializedResult = JsonSerializer.Serialize(searchResult);
                    
                    await _cache.SetStringAsync(cacheKey, serializedResult, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                        SlidingExpiration = TimeSpan.FromMinutes(5)
                    });
                }
            }
        }
    }

    public class DatabaseOptimizationService
    {
        public async Task CreateOptimizedIndexesAsync()
        {
            var indexCreationScripts = new[]
            {
                // Composite index for common search patterns
                @"CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_vehicle_listings_search_composite 
                  ON marketplace.vehicle_listings (status, price, dealer_id) 
                  WHERE status = 1;",

                // Partial index for active listings only
                @"CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_vehicle_listings_active_price 
                  ON marketplace.vehicle_listings (price DESC) 
                  WHERE status = 1;",

                // GIN index for full-text search
                @"CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_vehicle_listings_fts 
                  ON marketplace.vehicle_listings USING gin(to_tsvector('english', description));",

                // Index for geographic searches
                @"CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_dealer_location 
                  ON marketplace.marketplace_dealers USING gist((business_address->'coordinates'))
                  WHERE status = 1;",

                // Index for lead management
                @"CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_sales_leads_dealer_status_created 
                  ON marketplace.sales_leads (dealer_id, status, created_at DESC);",

                // Index for analytics queries
                @"CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_sales_contracts_dealer_completed 
                  ON marketplace.sales_contracts (dealer_id, completed_at) 
                  WHERE status = 3;"
            };

            foreach (var script in indexCreationScripts)
            {
                await _dbContext.Database.ExecuteSqlRawAsync(script);
            }
        }

        public async Task ImplementPartitioningAsync()
        {
            // Partition large tables by date for better performance
            var partitioningScript = @"
                -- Partition pricing history by month
                CREATE TABLE marketplace.pricing_history_y2024m01 
                PARTITION OF marketplace.pricing_history 
                FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');

                -- Partition analytics by month
                CREATE TABLE marketplace.marketplace_analytics_y2024m01 
                PARTITION OF marketplace.marketplace_analytics 
                FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');
            ";

            await _dbContext.Database.ExecuteSqlRawAsync(partitioningScript);
        }
    }

    public class CachingStrategy : ICachingStrategy
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryCache;

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? expiry = null)
        {
            // Try L1 cache (memory) first
            if (_memoryCache.TryGetValue(key, out T cachedItem))
                return cachedItem;

            // Try L2 cache (distributed) second
            var distributedCachedItem = await _distributedCache.GetStringAsync(key);
            if (!string.IsNullOrEmpty(distributedCachedItem))
            {
                var deserializedItem = JsonSerializer.Deserialize<T>(distributedCachedItem);
                
                // Store in L1 cache for faster subsequent access
                _memoryCache.Set(key, deserializedItem, TimeSpan.FromMinutes(5));
                return deserializedItem;
            }

            // Fallback to data source
            var item = await getItem();
            var serializedItem = JsonSerializer.Serialize(item);
            
            // Store in both caches
            await _distributedCache.SetStringAsync(key, serializedItem, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(30)
            });
            
            _memoryCache.Set(key, item, TimeSpan.FromMinutes(5));
            
            return item;
        }
    }
}
```

### Multi-Tenant Performance Optimization

```csharp
namespace DriveOps.Marketplace.Infrastructure.MultiTenant
{
## 🔗 Integration Points with Core Modules

### Users & Permissions Integration

```csharp
namespace DriveOps.Marketplace.Infrastructure.Integration
{
    public class MarketplaceAuthorizationService : IMarketplaceAuthorizationService
    {
        private readonly IUserService _userService;
        private readonly IPermissionService _permissionService;

        public async Task<bool> CanUserManageListingAsync(UserId userId, VehicleListingId listingId)
        {
            var listing = await _listingRepository.GetByIdAsync(listingId);
            if (listing == null) return false;

            // Check if user is dealer owner or authorized sales person
            var userPermissions = await _permissionService.GetUserPermissionsAsync(userId);
            
            return userPermissions.Any(p => 
                p.PermissionName == "ManageVehicleListings" && 
                p.Context.Contains($"dealer:{listing.DealerId}"));
        }

        public async Task<bool> CanUserAccessDealerDataAsync(UserId userId, MarketplaceDealerId dealerId)
        {
            var userRoles = await _userService.GetUserRolesAsync(userId);
            
            return userRoles.Any(r => 
                r.RoleName == "DealerOwner" && r.Context.Contains($"dealer:{dealerId}") ||
                r.RoleName == "DealerSalesPerson" && r.Context.Contains($"dealer:{dealerId}") ||
                r.RoleName == "SystemAdmin");
        }
    }

    // Permission definitions for marketplace
    public static class MarketplacePermissions
    {
        public const string ManageVehicleListings = "Marketplace.ManageVehicleListings";
        public const string ViewSalesLeads = "Marketplace.ViewSalesLeads";
        public const string ManageSalesLeads = "Marketplace.ManageSalesLeads";
        public const string ProcessSalesContracts = "Marketplace.ProcessSalesContracts";
        public const string ViewDealerAnalytics = "Marketplace.ViewDealerAnalytics";
        public const string ManageCommissions = "Marketplace.ManageCommissions";
        public const string ViewMarketplaceAnalytics = "Marketplace.ViewMarketplaceAnalytics";
    }
}
```

### Vehicles Module Integration

```csharp
namespace DriveOps.Marketplace.Infrastructure.Integration
{
    public class VehicleIntegrationService : IVehicleIntegrationService
    {
        private readonly IVehicleService _vehicleService;
        private readonly IVehicleListingRepository _listingRepository;

        public async Task<VehicleDetailsDto> GetVehicleDetailsAsync(VehicleId vehicleId)
        {
            // Fetch complete vehicle details from core Vehicles module
            var vehicle = await _vehicleService.GetVehicleByIdAsync(vehicleId);
            
            if (vehicle == null)
                throw new VehicleNotFoundException($"Vehicle {vehicleId} not found");

            return new VehicleDetailsDto
            {
                Id = vehicle.Id,
                Vin = vehicle.Vin,
                LicensePlate = vehicle.LicensePlate,
                Brand = vehicle.Brand,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Color = vehicle.Color,
                Mileage = vehicle.Mileage,
                BodyType = vehicle.BodyType,
                FuelType = vehicle.FuelType,
                Transmission = vehicle.Transmission,
                MaintenanceRecords = vehicle.MaintenanceRecords
            };
        }

        public async Task ValidateVehicleOwnershipAsync(VehicleId vehicleId, MarketplaceDealerId dealerId)
        {
            var vehicle = await _vehicleService.GetVehicleByIdAsync(vehicleId);
            var dealer = await _dealerRepository.GetByIdAsync(dealerId);

            // Verify that the dealer owns or has rights to list this vehicle
            if (vehicle.OwnerId != dealer.UserId)
            {
                // Check for consignment agreement
                var consignment = await _depositSaleRepository
                    .GetByVehicleIdAsync(vehicleId);
                
                if (consignment?.DealerId != dealerId)
                    throw new UnauthorizedVehicleAccessException(
                        $"Dealer {dealerId} not authorized to list vehicle {vehicleId}");
            }
        }

        // Handle vehicle update events from core module
        public async Task HandleVehicleUpdatedAsync(VehicleUpdatedEvent @event)
        {
            var listings = await _listingRepository.GetByVehicleIdAsync(@event.VehicleId);
            
            foreach (var listing in listings)
            {
                // Update listing with new vehicle information
                listing.UpdateVehicleDetails(@event.UpdatedVehicle);
                
                // Re-index in Elasticsearch
                await _searchService.UpdateVehicleListingAsync(listing.Id);
                
                // Notify interested buyers
                await _notificationService.NotifyVehicleUpdatedAsync(listing.Id);
            }
        }
    }
}
```

### Files Module Integration

```csharp
namespace DriveOps.Marketplace.Infrastructure.Integration
{
    public class MarketplaceFileService : IMarketplaceFileService
    {
        private readonly IFileService _fileService;
        private readonly IImageProcessingService _imageProcessingService;

        public async Task<FileUploadResult> UploadVehiclePhotoAsync(
            VehicleListingId listingId, 
            IFormFile photo, 
            int displayOrder,
            bool isPrimary = false)
        {
            // Validate image
            if (!IsValidImageFile(photo))
                throw new InvalidFileTypeException("Only JPEG, PNG, and WebP images are allowed");

            if (photo.Length > 10 * 1024 * 1024) // 10MB limit
                throw new FileSizeExceededException("Image size cannot exceed 10MB");

            // Upload original image
            var originalUpload = await _fileService.UploadFileAsync(new FileUploadRequest
            {
                File = photo,
                EntityType = "VehicleListing",
                EntityId = listingId.Value.ToString(),
                Category = "Photos",
                Tags = new[] { "vehicle", "listing", isPrimary ? "primary" : "secondary" }
            });

            // Generate thumbnails and optimized versions
            var thumbnailTasks = new[]
            {
                GenerateThumbnailAsync(originalUpload.FileId, 150, 150, "thumbnail"),
                GenerateThumbnailAsync(originalUpload.FileId, 400, 300, "medium"),
                GenerateThumbnailAsync(originalUpload.FileId, 800, 600, "large"),
                GenerateWebPVersionAsync(originalUpload.FileId)
            };

            await Task.WhenAll(thumbnailTasks);

            // Create photo record
            var photo = new VehiclePhoto(
                listingId.TenantId,
                listingId,
                originalUpload.FileId,
                displayOrder,
                isPrimary
            );

            await _vehiclePhotoRepository.AddAsync(photo);

            return new FileUploadResult
            {
                PhotoId = photo.Id,
                FileId = originalUpload.FileId,
                Url = originalUpload.Url,
                ThumbnailUrls = await GetThumbnailUrlsAsync(originalUpload.FileId)
            };
        }

        private async Task<ThumbnailUrls> GetThumbnailUrlsAsync(FileId fileId)
        {
            var thumbnailFiles = await _fileService.GetRelatedFilesAsync(fileId, "thumbnail");
            
            return new ThumbnailUrls
            {
                Thumbnail = thumbnailFiles.FirstOrDefault(f => f.Tags.Contains("thumbnail"))?.Url,
                Medium = thumbnailFiles.FirstOrDefault(f => f.Tags.Contains("medium"))?.Url,
                Large = thumbnailFiles.FirstOrDefault(f => f.Tags.Contains("large"))?.Url,
                WebP = thumbnailFiles.FirstOrDefault(f => f.ContentType == "image/webp")?.Url
            };
        }

        public async Task<FileId> GenerateContractDocumentAsync(SalesContractId contractId, ContractData data)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            
            // Generate PDF contract using template
            var templateData = new
            {
                ContractNumber = contract.Id.Value.ToString("N")[..8].ToUpper(),
                ContractDate = contract.CreatedAt,
                Vehicle = contract.VehicleListing.Vehicle,
                Buyer = contract.Buyer,
                Dealer = contract.Dealer,
                SalePrice = contract.SalePrice,
                Terms = contract.Terms
            };

            var pdfBytes = await _documentGenerationService.GeneratePdfAsync(
                "sales_contract_template", 
                templateData
            );

            // Upload generated contract
            using var pdfStream = new MemoryStream(pdfBytes);
            var uploadResult = await _fileService.UploadFileAsync(new FileUploadRequest
            {
                Stream = pdfStream,
                FileName = $"Contract_{contract.Id.Value:N}.pdf",
                ContentType = "application/pdf",
                EntityType = "SalesContract",
                EntityId = contractId.Value.ToString(),
                Category = "Documents",
                Tags = new[] { "contract", "legal", "generated" }
            });

            return uploadResult.FileId;
        }
    }
}
```

### Notifications Module Integration
## 🧪 Testing Strategy

### Integration Tests

```csharp
namespace DriveOps.Marketplace.Tests.Integration
{
    public class VehicleListingWorkflowTests : IntegrationTestBase
    {
        [Test]
        public async Task CreateVehicleListing_ProcessLead_CompleteSale_ShouldWorkEndToEnd()
        {
            // Arrange
            var dealer = await CreateTestDealerAsync();
            var vehicle = await CreateTestVehicleAsync();
            
            // Act 1: Create listing
            var createListingCommand = new CreateVehicleListingCommand(
                TenantId, dealer.Id, vehicle.Id, 25000m, "Excellent condition", ListingType.Sale);
            var listingResult = await Mediator.Send(createListingCommand);
            
            // Act 2: Create lead
            var createLeadCommand = new CreateSalesLeadCommand(
                TenantId, dealer.Id, listingResult.Value, TestBuyer, LeadSource.Marketplace, 24000m);
            var leadResult = await Mediator.Send(createLeadCommand);
            
            // Act 3: Create contract
            var createContractCommand = new CreateSalesContractCommand(
                TenantId, dealer.Id, listingResult.Value, leadResult.Value, TestBuyer, 24500m, TestTerms);
            var contractResult = await Mediator.Send(createContractCommand);
            
            // Act 4: Process payment
            var paymentCommand = new ProcessPaymentCommand(
                TenantId, contractResult.Value, 24500m, PaymentMethod.BankTransfer);
            await Mediator.Send(paymentCommand);
            
            // Assert
            var finalListing = await GetVehicleListingAsync(listingResult.Value);
            Assert.That(finalListing.Status, Is.EqualTo(ListingStatus.Sold));
            
            var commission = await GetCommissionAsync(contractResult.Value);
            Assert.That(commission.Amount, Is.EqualTo(24500m * 0.025m)); // 2.5% commission
        }
    }

    public class MarketplaceSearchTests : IntegrationTestBase
    {
        [Test]
        public async Task SearchVehicles_WithComplexCriteria_ShouldReturnRelevantResults()
        {
            // Arrange
            await SeedTestVehicleListingsAsync();
            
            var searchCriteria = new VehicleSearchCriteria
            {
                SearchText = "BMW",
                MinPrice = 20000m,
                MaxPrice = 50000m,
                MinYear = 2018,
                Location = new Location(48.8566m, 2.3522m), // Paris
                RadiusKm = 100
            };
            
            // Act
            var searchQuery = new SearchVehiclesQuery(TenantId, searchCriteria, 1, 10);
            var result = await Mediator.Send(searchQuery);
            
            // Assert
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Items, Is.Not.Empty);
            Assert.That(result.Value.Items.All(v => v.Vehicle.Make == "BMW"), Is.True);
            Assert.That(result.Value.Items.All(v => v.Price >= 20000m && v.Price <= 50000m), Is.True);
        }
    }
}
```

## 📊 Business Intelligence and Reporting

### Analytics Dashboard

```csharp
namespace DriveOps.Marketplace.Application.Analytics
{
    public class MarketplaceBusinessIntelligenceService : IMarketplaceBusinessIntelligenceService
    {
        public async Task<MarketplaceDashboardDto> GenerateDashboardAsync(TenantId tenantId, DateRange dateRange)
        {
            var tasks = new[]
            {
                GetMarketOverviewAsync(tenantId, dateRange),
                GetTopPerformingDealersAsync(tenantId, dateRange),
                GetInventoryInsightsAsync(tenantId, dateRange),
                GetRevenueAnalyticsAsync(tenantId, dateRange),
                GetCustomerInsightsAsync(tenantId, dateRange)
            };

            var results = await Task.WhenAll(tasks);

            return new MarketplaceDashboardDto
            {
                MarketOverview = results[0],
                TopDealers = results[1],
                InventoryInsights = results[2],
                RevenueAnalytics = results[3],
                CustomerInsights = results[4],
                GeneratedAt = DateTime.UtcNow
            };
        }

        private async Task<MarketOverviewDto> GetMarketOverviewAsync(TenantId tenantId, DateRange dateRange)
        {
            return new MarketOverviewDto
            {
                TotalListings = await _context.VehicleListings.CountAsync(v => v.TenantId == tenantId),
                ActiveListings = await _context.VehicleListings.CountAsync(v => v.TenantId == tenantId && v.Status == ListingStatus.Active),
                TotalSales = await _context.SalesContracts.CountAsync(c => c.TenantId == tenantId && c.CompletedAt >= dateRange.Start && c.CompletedAt <= dateRange.End),
                TotalRevenue = await _context.SalesContracts.Where(c => c.TenantId == tenantId && c.CompletedAt >= dateRange.Start && c.CompletedAt <= dateRange.End).SumAsync(c => c.SalePrice),
                AveragePrice = await _context.VehicleListings.Where(v => v.TenantId == tenantId && v.Status == ListingStatus.Active).AverageAsync(v => v.Price),
                AverageDaysToSell = await CalculateAverageDaysToSellAsync(tenantId, dateRange)
            };
        }
    }
}
```

## 🚀 Deployment and Infrastructure

### Docker Configuration

```dockerfile
# Dockerfile.marketplace
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/DriveOps.Marketplace.API/DriveOps.Marketplace.API.csproj", "src/DriveOps.Marketplace.API/"]
COPY ["src/DriveOps.Marketplace.Application/DriveOps.Marketplace.Application.csproj", "src/DriveOps.Marketplace.Application/"]
COPY ["src/DriveOps.Marketplace.Domain/DriveOps.Marketplace.Domain.csproj", "src/DriveOps.Marketplace.Domain/"]
COPY ["src/DriveOps.Marketplace.Infrastructure/DriveOps.Marketplace.Infrastructure.csproj", "src/DriveOps.Marketplace.Infrastructure/"]

RUN dotnet restore "src/DriveOps.Marketplace.API/DriveOps.Marketplace.API.csproj"
COPY . .
WORKDIR "/src/src/DriveOps.Marketplace.API"
RUN dotnet build "DriveOps.Marketplace.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DriveOps.Marketplace.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DriveOps.Marketplace.API.dll"]
```

### Kubernetes Deployment

```yaml
# marketplace-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: marketplace-api
  namespace: driveops
spec:
  replicas: 3
  selector:
    matchLabels:
      app: marketplace-api
  template:
    metadata:
      labels:
        app: marketplace-api
    spec:
      containers:
      - name: marketplace-api
        image: driveops/marketplace-api:latest
        ports:
        - containerPort: 80
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: marketplace-secrets
              key: database-connection
        - name: Elasticsearch__Url
          value: "http://elasticsearch:9200"
        - name: Redis__ConnectionString
          valueFrom:
            secretKeyRef:
              name: marketplace-secrets
              key: redis-connection
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "1Gi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: marketplace-api-service
  namespace: driveops
spec:
  selector:
    app: marketplace-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 80
  type: ClusterIP
```

## 🎯 Conclusion

The MARKETPLACE AUTO module provides a comprehensive vehicle sales and consignment management system that delivers:

### ✅ Key Achievements

- **Dual Revenue Model**: Individual dealer websites (29€/month) + Marketplace Premium (59€/month) + 2-3% commissions
- **Network Effect**: Growing marketplace creates value for both buyers and dealers
- **Multi-Tenant Architecture**: Scalable infrastructure supporting thousands of dealers
- **Advanced Search**: Elasticsearch-powered vehicle discovery with intelligent filtering
- **Complete Sales Workflow**: From lead generation to contract completion and payment processing
- **Mobile-First Design**: Native apps for both buyers and dealers
- **SEO Optimization**: Comprehensive search engine optimization for organic traffic
- **Marketing Automation**: Intelligent campaigns driving buyer engagement
- **Real-time Analytics**: Business intelligence dashboards for performance monitoring

### 🔧 Technical Excellence

- **Domain-Driven Design**: Clean architecture with proper aggregates and bounded contexts
- **Event-Driven Architecture**: Asynchronous processing for scalability
- **CQRS Implementation**: Optimized read/write operations for high performance
- **Multi-Channel Integrations**: Payment processors, financing partners, insurance providers
- **Performance Optimization**: Caching strategies, database indexing, search optimization
- **Security First**: Role-based permissions, data encryption, audit trails

### 📈 Business Impact

- **Viral Growth Mechanism**: Buyers discover dealers, dealers see success and join
- **Commission-Based Revenue**: Sustainable income stream tied to dealer success
- **Market Leadership**: Comprehensive feature set differentiating from competitors
- **Scalable Operations**: Infrastructure supporting rapid growth and expansion

This module establishes DriveOps as the leading marketplace platform in the automotive industry, providing dealers with powerful tools to grow their business while offering buyers the best vehicle discovery experience.

```csharp
namespace DriveOps.Marketplace.Infrastructure.Integration
{
    public class MarketplaceNotificationService : IMarketplaceNotificationService
    {
        private readonly INotificationService _notificationService;
        private readonly ITemplateService _templateService;

        public async Task NotifyNewLeadAsync(SalesLeadId leadId)
        {
            var lead = await _leadRepository.GetByIdWithDetailsAsync(leadId);
            var dealer = lead.Dealer;
            var dealerUsers = await _userService.GetDealerUsersAsync(dealer.Id);

            // Notify all dealer users with appropriate roles
            var notificationRecipients = dealerUsers
                .Where(u => u.Roles.Any(r => r.RoleName is "DealerOwner" or "SalesManager" or "SalesPerson"))
                .Select(u => u.Id)
                .ToList();

            await _notificationService.SendNotificationAsync(new SendNotificationRequest
            {
                TenantId = lead.TenantId,
                RecipientIds = notificationRecipients,
                Type = NotificationType.NewSalesLead,
                Title = "New Sales Lead",
                Message = $"New inquiry for {lead.VehicleListing.Vehicle.Brand.Name} {lead.VehicleListing.Vehicle.Model}",
                TemplateId = "new_sales_lead",
                Variables = new Dictionary<string, string>
                {
                    ["lead_id"] = lead.Id.Value.ToString(),
                    ["vehicle_info"] = $"{lead.VehicleListing.Vehicle.Year} {lead.VehicleListing.Vehicle.Brand.Name} {lead.VehicleListing.Vehicle.Model}",
                    ["buyer_name"] = $"{lead.Buyer.FirstName} {lead.Buyer.LastName}",
                    ["offered_price"] = lead.OfferedPrice?.ToString("C") ?? "Not specified",
                    ["lead_source"] = lead.Source.ToString()
                },
                Actions = new[]
                {
                    new NotificationAction("View Lead", $"/leads/{lead.Id}"),
                    new NotificationAction("Contact Buyer", $"tel:{lead.Buyer.Phone}")
                },
                Priority = NotificationPriority.High
            });

            // Send immediate SMS for high-priority leads
            if (lead.OfferedPrice >= lead.VehicleListing.Price * 0.9m) // Within 10% of asking price
            {
                await _notificationService.SendSmsAsync(new SendSmsRequest
                {
                    TenantId = lead.TenantId,
                    Recipients = dealerUsers.Where(u => u.Phone != null).Select(u => u.Phone).ToList(),
                    Message = $"🚨 HIGH PRIORITY LEAD: {lead.Buyer.FirstName} {lead.Buyer.LastName} offered €{lead.OfferedPrice:N0} for your {lead.VehicleListing.Vehicle.Brand.Name} {lead.VehicleListing.Vehicle.Model}. Contact immediately!",
                    TemplateId = "urgent_lead_sms"
                });
            }
        }

        public async Task NotifyVehicleSoldAsync(VehicleListingId listingId, SalesContractId contractId)
        {
            var listing = await _listingRepository.GetByIdWithDetailsAsync(listingId);
            var contract = await _contractRepository.GetByIdAsync(contractId);

            // Notify dealer of successful sale
            await _notificationService.SendNotificationAsync(new SendNotificationRequest
            {
                TenantId = listing.TenantId,
                RecipientIds = new[] { listing.Dealer.UserId },
                Type = NotificationType.VehicleSold,
                Title = "Vehicle Sold! 🎉",
                Message = $"Congratulations! Your {listing.Vehicle.Brand.Name} {listing.Vehicle.Model} has been sold for €{contract.SalePrice:N0}",
                TemplateId = "vehicle_sold_celebration",
                Variables = new Dictionary<string, string>
                {
                    ["vehicle_info"] = $"{listing.Vehicle.Year} {listing.Vehicle.Brand.Name} {listing.Vehicle.Model}",
                    ["sale_price"] = contract.SalePrice.ToString("C"),
                    ["commission_amount"] = contract.CommissionAmount.ToString("C"),
                    ["dealer_amount"] = contract.DealerAmount.ToString("C"),
                    ["buyer_name"] = $"{contract.Buyer.FirstName} {contract.Buyer.LastName}"
                }
            });

            // Notify interested buyers that vehicle is no longer available
            var interestedBuyers = await _leadRepository.GetInterestedBuyersAsync(listingId);
            
            foreach (var buyer in interestedBuyers.Where(b => b.Id != contract.Buyer.Id))
            {
                await _notificationService.SendEmailAsync(new SendEmailRequest
                {
                    TenantId = listing.TenantId,
                    RecipientEmail = buyer.Email,
                    Subject = "Vehicle No Longer Available",
                    TemplateId = "vehicle_sold_to_others",
                    Variables = new Dictionary<string, string>
                    {
                        ["buyer_name"] = buyer.FirstName,
                        ["vehicle_info"] = $"{listing.Vehicle.Year} {listing.Vehicle.Brand.Name} {listing.Vehicle.Model}",
                        ["similar_vehicles_url"] = $"/search?make={listing.Vehicle.Brand.Name}&model={listing.Vehicle.Model}"
                    }
                });
            }
        }

        public async Task SendMarketingCampaignAsync(MarketingCampaignId campaignId)
        {
            var campaign = await _marketingCampaignRepository.GetByIdAsync(campaignId);
            var targetAudience = await _audienceService.GetTargetAudienceAsync(campaign.AudienceId);

            var notificationRequest = new SendBulkNotificationsRequest
            {
                TenantId = campaign.TenantId,
                RecipientIds = targetAudience.BuyerIds.ToList(),
                Type = NotificationType.MarketingEmail,
                TemplateId = campaign.TemplateId,
                Variables = campaign.Variables,
                ScheduledFor = campaign.ScheduledFor,
                Channels = campaign.Channels // Email, SMS, Push
            };

            await _notificationService.SendBulkNotificationsAsync(notificationRequest);
        }
    }
}
```
    public class TenantAwareDbContext : DbContext, ITenantAwareDbContext
    {
        private readonly ITenantContext _tenantContext;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply tenant-specific query filters globally
            modelBuilder.Entity<VehicleListing>()
                .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<MarketplaceDealer>()
                .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);

            modelBuilder.Entity<SalesLead>()
                .HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);

            // Optimize for tenant-specific queries
            modelBuilder.Entity<VehicleListing>()
                .HasIndex(e => new { e.TenantId, e.Status, e.Price })
                .HasDatabaseName("IX_VehicleListing_Tenant_Status_Price");

            modelBuilder.Entity<SalesLead>()
                .HasIndex(e => new { e.TenantId, e.DealerId, e.Status, e.CreatedAt })
                .HasDatabaseName("IX_SalesLead_Tenant_Dealer_Status_Created");
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Automatically set TenantId for new entities
            foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.TenantId = _tenantContext.TenantId;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    public class TenantAwareElasticsearchService : ITenantAwareElasticsearchService
    {
        public string GetTenantSpecificIndexName(string baseIndexName)
        {
            return $"{baseIndexName}-{_tenantContext.TenantId.Value.ToString("N")[..8]}";
        }

        public async Task EnsureTenantIndexExistsAsync(string indexName)
        {
            var tenantIndexName = GetTenantSpecificIndexName(indexName);
            
            var indexExists = await _elasticsearchClient.Indices.ExistsAsync(tenantIndexName);
            if (!indexExists.Exists)
            {
                await _elasticsearchClient.Indices.CreateAsync(tenantIndexName, c => c
                    .Settings(s => s
                        .NumberOfShards(1)
                        .NumberOfReplicas(1)
                        .RefreshInterval("1s")
                    )
                    .Map<VehicleListingDocument>(m => m.AutoMap())
                );
            }
        }
    }
}
```
                    EmailSentAt = DateTime.UtcNow
                });
            }
        }

        private async Task PostToSocialMediaAsync(VehicleListing listing)
        {
            var socialPost = new SocialMediaPost
            {
                Message = $"🚗 NEW ARRIVAL: {listing.Vehicle.Year} {listing.Vehicle.Brand.Name} {listing.Vehicle.Model} - €{listing.Price:N0}\n\n" +
                         $"✅ {listing.Vehicle.Mileage:N0} km\n" +
                         $"📍 {listing.Dealer.BusinessAddress.City}\n\n" +
                         $"View details: https://marketplace.driveops.com/vehicles/{listing.Id}\n\n" +
                         "#UsedCars #" + listing.Vehicle.Brand.Name.Replace(" ", "") + " #CarDealer",
                
                Images = listing.Photos.Take(4).Select(p => p.Url).ToList(),
                TargetAudiences = await _segmentationService.GetTargetAudiencesAsync(listing),
                ScheduledTime = DateTime.UtcNow.AddMinutes(15) // Slight delay for review
            };

            // Post to multiple platforms
            await _socialMediaService.PostToFacebookAsync(socialPost);
            await _socialMediaService.PostToInstagramAsync(socialPost);
            await _socialMediaService.PostToTwitterAsync(socialPost);
        }

        public async Task ProcessAbandonedInquiryAsync(VehicleInquiryId inquiryId)
        {
            var inquiry = await _inquiryRepository.GetByIdAsync(inquiryId);
            
            // Send follow-up email sequence
            var followUpSequence = new[]
            {
                new { DelayHours = 2, TemplateId = "inquiry_followup_immediate" },
                new { DelayHours = 24, TemplateId = "inquiry_followup_day1" },
                new { DelayHours = 72, TemplateId = "inquiry_followup_day3" },
                new { DelayHours = 168, TemplateId = "inquiry_followup_week1" }
            };

            foreach (var followUp in followUpSequence)
            {
                await _schedulerService.ScheduleEmailAsync(new ScheduledEmailRequest
                {
                    RecipientEmail = inquiry.Buyer.Email,
                    TemplateId = followUp.TemplateId,
                    SendAt = DateTime.UtcNow.AddHours(followUp.DelayHours),
                    Data = new
                    {
                        BuyerName = inquiry.Buyer.FirstName,
                        VehicleInfo = inquiry.VehicleListing.Vehicle,
                        DealerInfo = inquiry.VehicleListing.Dealer,
                        InquiryId = inquiry.Id
                    }
                });
            }
        }

        public async Task ProcessRetargetingCampaignAsync(BuyerId buyerId, List<VehicleListingId> viewedListings)
        {
            var buyer = await _buyerRepository.GetByIdAsync(buyerId);
            var viewedVehicles = await _listingRepository.GetByIdsAsync(viewedListings);
            
            // Create retargeting audience segments
            var segments = await _segmentationService.CreateRetargetingSegmentsAsync(buyer, viewedVehicles);
            
            // Launch Google Ads retargeting campaign
            await _googleAdsService.CreateRetargetingCampaignAsync(new RetargetingCampaignRequest
            {
                BuyerId = buyerId,
                TargetVehicles = viewedVehicles.Select(v => new TargetVehicle
                {
                    Make = v.Vehicle.Brand.Name,
                    Model = v.Vehicle.Model,
                    PriceRange = GetPriceRange(v.Price),
                    BodyType = v.Vehicle.BodyType
                }).ToList(),
                Budget = CalculateRetargetingBudget(viewedVehicles),
                Duration = TimeSpan.FromDays(30)
            });
        }
    }
}
```
        </TouchableOpacity>
      </View>

      <FlatList
        data={data?.searchVehicles?.items || []}
        renderItem={renderVehicleItem}
        keyExtractor={(item) => item.id}
        refreshControl={
          <RefreshControl refreshing={loading} onRefresh={refetch} />
        }
        showsVerticalScrollIndicator={false}
      />
    </View>
  );
};
```

### Dealer Mobile App Features

```typescript
// DealerDashboardScreen.tsx
export const DealerDashboardScreen: React.FC = () => {
  const { data: performance } = useQuery(GET_DEALER_PERFORMANCE_QUERY, {
    variables: {
      fromDate: startOfMonth(new Date()),
      toDate: endOfMonth(new Date())
    }
  });

  const { data: leads } = useQuery(GET_DEALER_LEADS_QUERY, {
    variables: {
      filter: { status: 'NEW' },
      page: 1,
      pageSize: 10
    }
  });

  return (
    <ScrollView style={styles.container}>
      <View style={styles.metricsContainer}>
        <MetricCard 
          title="Active Listings" 
          value={performance?.activeListings || 0} 
          icon="car"
        />
        <MetricCard 
          title="New Leads" 
          value={leads?.items?.length || 0} 
          icon="users"
        />
        <MetricCard 
          title="This Month Sales" 
          value={performance?.salesCount || 0} 
          icon="trending-up"
        />
        <MetricCard 
          title="Revenue" 
          value={`€${performance?.totalRevenue?.toLocaleString() || 0}`} 
          icon="euro-sign"
        />
      </View>

      <QuickActionsPanel />
      <RecentLeadsSection leads={leads?.items || []} />
      <InventoryOverviewSection />
    </ScrollView>
  );
};

// LeadManagementScreen.tsx
export const LeadManagementScreen: React.FC = () => {
  const [updateLeadStatus] = useMutation(UPDATE_LEAD_STATUS_MUTATION);
  
  const handleStatusUpdate = async (leadId: string, newStatus: LeadStatus) => {
    try {
      await updateLeadStatus({
        variables: { leadId, status: newStatus }
      });
    } catch (error) {
      Alert.alert('Error', 'Failed to update lead status');
    }
  };

  return (
    <View style={styles.container}>
      <LeadFilters />
      <LeadsList onStatusUpdate={handleStatusUpdate} />
    </View>
  );
};
```

            var result = await _mediator.Send(command);
            return result.IsSuccess ? NoContent() : BadRequest(result.Error);
        }
    }

    [ApiController]
    [Route("api/marketplace/v1/[controller]")]
    [Authorize(Policy = "DealerOnly")]
    public class SalesContractsController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<SalesContractDto>> CreateContract(CreateSalesContractRequest request)
        {
            var command = new CreateSalesContractCommand(
                _tenantContext.TenantId,
                User.GetDealerId(),
                new VehicleListingId(request.ListingId),
                new SalesLeadId(request.LeadId),
                request.Buyer,
                request.SalePrice,
                request.Terms
            );

            var result = await _mediator.Send(command);
            return result.IsSuccess ? CreatedAtAction(nameof(GetContract), new { id = result.Value }, result.Value)
                : BadRequest(result.Error);
        }

        [HttpPost("{id}/payments")]
        public async Task<ActionResult> ProcessPayment(Guid id, ProcessPaymentRequest request)
        {
            var command = new ProcessPaymentCommand(
                _tenantContext.TenantId,
                new SalesContractId(id),
                request.Amount,
                request.Method,
                request.TransactionId
            );

            var result = await _mediator.Send(command);
            return result.IsSuccess ? NoContent() : BadRequest(result.Error);
        }
    }
}
```

            // Integration with multiple insurance providers
            var providers = new[] { "StateFearm", "Geico", "Progressive", "AllState" };

            var tasks = providers.Select(async provider =>
            {
                try
                {
                    var quote = await GetQuoteFromProviderAsync(provider, request);
                    return quote;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get quote from {Provider}", provider);
                    return null;
                }
            });

            var results = await Task.WhenAll(tasks);
            quotes.AddRange(results.Where(q => q != null));

            return quotes.OrderBy(q => q.Premium).ToList();
        }
    }

    // CRM Integration (Salesforce/HubSpot)
    public class CrmIntegrationService : ICrmIntegrationService
    {
        public async Task SyncLeadToCrmAsync(SalesLead lead)
        {
            var crmLead = new
            {
                FirstName = lead.Buyer.FirstName,
                LastName = lead.Buyer.LastName,
                Email = lead.Buyer.Email,
                Phone = lead.Buyer.Phone,
                Company = "Individual",
                LeadSource = "Marketplace Auto",
                Status = "New",
                VehicleInterest = $"{lead.VehicleListing.Vehicle.Brand.Name} {lead.VehicleListing.Vehicle.Model}",
                Budget = lead.OfferedPrice,
                Notes = lead.Notes,
                DealerId = lead.DealerId.Value,
                ListingId = lead.VehicleListingId.Value
            };

            await _salesforceClient.CreateLeadAsync(crmLead);
        }

        public async Task UpdateLeadStatusAsync(SalesLeadId leadId, LeadStatus status)
        {
            await _salesforceClient.UpdateLeadAsync(leadId.Value, new { Status = status.ToString() });
        }
    }
}
```

            // Boost recently listed vehicles
            shouldQueries.Add(new DecayQuery
            {
                Field = "listedAt",
                DecayFunction = new DateDecayFunction
                {
                    Origin = DateTime.Now,
                    Scale = TimeSpan.FromDays(7),
                    Decay = 0.5
                }
            });

            // Filter by active status
            filterQueries.Add(new TermQuery { Field = "status", Value = (int)ListingStatus.Active });

            return new BoolQuery
            {
                Must = mustQueries,
                Should = shouldQueries,
                Filter = filterQueries,
                MinimumShouldMatch = 1
            };
        }
    }

    public class VehicleIndexingService : IVehicleIndexingService
    {
        private readonly IElasticsearchClient _elasticsearchClient;
        private readonly IVehicleListingRepository _listingRepository;

        public async Task IndexVehicleListingAsync(VehicleListingId listingId)
        {
            var listing = await _listingRepository.GetByIdWithDetailsAsync(listingId);
            if (listing == null) return;

            var document = new VehicleListingDocument
            {
                Id = listing.Id.Value,
                TenantId = listing.TenantId.Value,
                DealerId = listing.DealerId.Value,
                Price = listing.Price,
                Description = listing.Description,
                Status = (int)listing.Status,
                ListedAt = listing.ListedAt,
                ViewCount = listing.ViewCount,
                Vehicle = new VehicleDocument
                {
                    Id = listing.Vehicle.Id.Value,
                    Make = listing.Vehicle.Brand.Name,
                    Model = listing.Vehicle.Model,
                    Year = listing.Vehicle.Year,
                    Mileage = listing.Vehicle.Mileage,
                    Color = listing.Vehicle.Color,
                    BodyType = listing.Vehicle.BodyType,
                    FuelType = listing.Vehicle.FuelType,
                    Transmission = listing.Vehicle.Transmission
                },
                Dealer = new DealerDocument
                {
                    Id = listing.Dealer.Id.Value,
                    Name = listing.Dealer.DealerName,
                    Location = new GeoLocation(
                        listing.Dealer.BusinessAddress.Latitude,
                        listing.Dealer.BusinessAddress.Longitude
                    )
                },
                Photos = listing.Photos.Select(p => new PhotoDocument
                {
                    Id = p.Id.Value,
                    Url = p.Url,
                    IsPrimary = p.IsPrimary
                }).ToList()
            };

            await _elasticsearchClient.IndexDocumentAsync(document);
        }

        public async Task RemoveVehicleListingAsync(VehicleListingId listingId)
        {
            await _elasticsearchClient.DeleteAsync<VehicleListingDocument>(listingId.Value);
        }
    }
}
```
                Field = "status",
                Value = (int)ListingStatus.Active
            });

            return new BoolQuery { Must = queries };
        }
    }

    public class GetMarketplaceAnalyticsHandler : IQueryHandler<GetMarketplaceAnalyticsQuery, Result<MarketplaceAnalyticsDto>>
    {
        private readonly IMarketplaceAnalyticsRepository _analyticsRepository;
        private readonly IMarketplaceAnalyticsService _analyticsService;

        public async Task<Result<MarketplaceAnalyticsDto>> Handle(GetMarketplaceAnalyticsQuery request, CancellationToken cancellationToken)
        {
            var analytics = await _analyticsService.GenerateMarketplaceAnalyticsAsync(
                request.TenantId,
                request.FromDate,
                request.ToDate,
                request.Granularity
            );

            return Result.Success(analytics);
        }
    }
}
```
                return Result.Failure<SalesContractId>("Dealer not found");

            // Calculate commission based on dealer's rate
            var commissionRate = dealer.CommissionRate;
            
            var contract = SalesContract.Create(
                request.TenantId,
                request.DealerId,
                request.ListingId,
                request.LeadId,
                request.Buyer,
                request.SalePrice,
                commissionRate
            );

            await _contractRepository.AddAsync(contract);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(contract.Id);
        }
    }
}
```
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id),
    period_start DATE NOT NULL,
    period_end DATE NOT NULL,
    total_listings INTEGER NOT NULL DEFAULT 0,
    active_listings INTEGER NOT NULL DEFAULT 0,
    sales_count INTEGER NOT NULL DEFAULT 0,
    total_revenue DECIMAL(15,2) NOT NULL DEFAULT 0,
    commission_earned DECIMAL(12,2) NOT NULL DEFAULT 0,
    average_sale_price DECIMAL(12,2),
    average_days_to_sell INTEGER,
    lead_conversion_rate DECIMAL(5,4),
    customer_satisfaction DECIMAL(3,2), -- Average rating
    performance_metrics JSONB, -- Additional performance data
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, dealer_id, period_start, period_end)
);

-- Inventory metrics
CREATE TABLE marketplace.inventory_metrics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID REFERENCES marketplace.marketplace_dealers(id),
    analysis_date DATE NOT NULL,
    total_inventory_value DECIMAL(15,2) NOT NULL DEFAULT 0,
    inventory_turnover_rate DECIMAL(5,4),
    average_inventory_age INTEGER, -- Days
    slow_moving_inventory INTEGER, -- Count of vehicles > 90 days
    brand_performance JSONB, -- Performance by vehicle brand
    price_range_performance JSONB, -- Performance by price range
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
```
    sold_at TIMESTAMP WITH TIME ZONE,
    expires_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Consignment agreements
CREATE TABLE marketplace.consignment_agreements (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id),
    deposit_sale_id UUID NOT NULL REFERENCES marketplace.deposit_sales(id),
    agreement_terms JSONB NOT NULL,
    signed_document_id UUID, -- References files.files(id)
    status INTEGER NOT NULL DEFAULT 1, -- Active, Completed, Terminated
    signed_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Sales commissions
CREATE TABLE marketplace.sales_commissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    dealer_id UUID NOT NULL REFERENCES marketplace.marketplace_dealers(id),
    contract_id UUID NOT NULL REFERENCES marketplace.sales_contracts(id),
    commission_type INTEGER NOT NULL, -- 1: Sale, 2: Consignment, 3: Premium Feature
    commission_amount DECIMAL(12,2) NOT NULL,
    commission_rate DECIMAL(5,4) NOT NULL,
    sale_amount DECIMAL(12,2) NOT NULL,
    status INTEGER NOT NULL DEFAULT 1, -- 1: Pending, 2: Processed, 3: Paid
    processed_at TIMESTAMP WITH TIME ZONE,
    paid_at TIMESTAMP WITH TIME ZONE,
    payment_reference VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Contract payments
CREATE TABLE marketplace.contract_payments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    contract_id UUID NOT NULL REFERENCES marketplace.sales_contracts(id),
    payment_amount DECIMAL(12,2) NOT NULL,
    payment_method INTEGER NOT NULL, -- 1: Cash, 2: Financing, 3: Credit Card, 4: Bank Transfer
    transaction_id VARCHAR(255),
    payment_processor VARCHAR(100),
    status INTEGER NOT NULL DEFAULT 1, -- 1: Pending, 2: Completed, 3: Failed, 4: Refunded
    processed_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
```
    inventory_status INTEGER NOT NULL DEFAULT 1, -- Available, Reserved, Sold
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Listing photos and media
CREATE TABLE marketplace.listing_photos (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    listing_id UUID NOT NULL REFERENCES marketplace.vehicle_listings(id) ON DELETE CASCADE,
    file_id UUID NOT NULL, -- References files.files(id)
    display_order INTEGER NOT NULL DEFAULT 0,
    is_primary BOOLEAN NOT NULL DEFAULT FALSE,
    caption VARCHAR(255),
    photo_type INTEGER NOT NULL DEFAULT 1, -- 1: Exterior, 2: Interior, 3: Engine, 4: Detail
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    INDEX idx_listing_photos_listing (listing_id, display_order)
);

-- Virtual tours
CREATE TABLE marketplace.virtual_tours (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    listing_id UUID NOT NULL REFERENCES marketplace.vehicle_listings(id) ON DELETE CASCADE,
    tour_type INTEGER NOT NULL, -- 1: 360° Photos, 2: Video Tour, 3: Interactive
    tour_data JSONB NOT NULL, -- Tour configuration and data
    provider VARCHAR(100), -- External provider if applicable
    external_tour_id VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Price history tracking
CREATE TABLE marketplace.pricing_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    listing_id UUID NOT NULL REFERENCES marketplace.vehicle_listings(id) ON DELETE CASCADE,
    old_price DECIMAL(12,2) NOT NULL,
    new_price DECIMAL(12,2) NOT NULL,
    change_reason INTEGER NOT NULL, -- 1: Market Adjustment, 2: Promotion, 3: Negotiation
    change_reason_notes TEXT,
    changed_by UUID NOT NULL, -- User who made the change
    changed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);
```
        public decimal TradeInValue { get; private set; }
        public decimal RetailValue { get; private set; }
        public ConditionFactor ConditionAdjustment { get; private set; }
        public MileageFactor MileageAdjustment { get; private set; }
        public DateTime ValuationDate { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public bool IsActive { get; private set; }

        public void UpdateValuation(decimal newEstimatedValue, decimal newMarketValue, ValuationSource source)
        {
            EstimatedValue = newEstimatedValue;
            MarketValue = newMarketValue;
            Source = source;
            ValuationDate = DateTime.UtcNow;
            ExpiresAt = DateTime.UtcNow.AddDays(30); // Valuations expire after 30 days
            AddDomainEvent(new VehicleValuationUpdatedEvent(TenantId, VehicleId, Id, newEstimatedValue));
        }
    }
}
```
        public DepositSaleId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public MarketplaceDealerId DealerId { get; private set; }
        public VehicleId VehicleId { get; private set; }
        public VehicleOwnerProfile Owner { get; private set; }
        public decimal ReservePrice { get; private set; }
        public decimal CommissionRate { get; private set; }
        public DepositSaleStatus Status { get; private set; }
        public DateTime DepositedAt { get; private set; }
        public DateTime? SoldAt { get; private set; }
        public string? SpecialInstructions { get; private set; }
        
        private readonly List<ConsignmentDocument> _documents = new();
        public IReadOnlyCollection<ConsignmentDocument> Documents => _documents.AsReadOnly();

        public void UpdateReservePrice(decimal newPrice, string reason)
        {
            var oldPrice = ReservePrice;
            ReservePrice = newPrice;
            AddDomainEvent(new ReservePriceUpdatedEvent(TenantId, DealerId, Id, oldPrice, newPrice, reason));
        }

        public void MarkAsSold(SalesContractId contractId, decimal salePrice)
        {
            Status = DepositSaleStatus.Sold;
            SoldAt = DateTime.UtcNow;
            AddDomainEvent(new DepositSaleSoldEvent(TenantId, DealerId, Id, contractId, salePrice));
        }

        public void AddDocument(DocumentType documentType, FileId fileId)
        {
            var document = new ConsignmentDocument(TenantId, Id, documentType, fileId);
            _documents.Add(document);
            AddDomainEvent(new ConsignmentDocumentAddedEvent(TenantId, DealerId, Id, documentType, fileId));
        }
    }
}
```
            AddDomainEvent(new VehiclePriceUpdatedEvent(TenantId, DealerId, Id, oldPrice, newPrice, reason));
        }

        public void AddPhoto(FileId photoFileId, int displayOrder, bool isPrimary = false)
        {
            var photo = new VehiclePhoto(TenantId, Id, photoFileId, displayOrder, isPrimary);
            _photos.Add(photo);
            AddDomainEvent(new VehiclePhotoAddedEvent(TenantId, DealerId, Id, photoFileId));
        }

        public void MarkAsSold(SalesContract contract)
        {
            Status = ListingStatus.Sold;
            SoldAt = DateTime.UtcNow;
            AddDomainEvent(new VehicleSoldEvent(TenantId, DealerId, Id, contract.Id));
        }

        public void RecordView(UserId? viewerId = null)
        {
            ViewCount++;
            AddDomainEvent(new VehicleViewedEvent(TenantId, DealerId, Id, viewerId));
        }
    }
}
```
```
