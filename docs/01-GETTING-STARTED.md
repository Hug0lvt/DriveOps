# Getting Started with DriveOps

This guide will help you set up your development environment and get DriveOps running locally in just a few minutes.

---

## 🎯 Overview

DriveOps is a comprehensive, modular SaaS platform for the automotive industry. This guide covers:
- Environment setup and prerequisites
- Local development setup
- First application run
- Basic configuration
- Sample data loading
- Next steps for development

---

## 📋 Prerequisites

### Required Software
- **.NET 8 SDK** or later ([Download here](https://dotnet.microsoft.com/download))
- **Docker Desktop** ([Download here](https://www.docker.com/products/docker-desktop))
- **PostgreSQL 16+** (via Docker or local installation)
- **MongoDB 7+** (via Docker or local installation)
- **Git** for version control

### Recommended Development Tools
- **Visual Studio 2022** (17.8+) or **Visual Studio Code** with C# extension
- **GitHub Copilot** extension (see our [Copilot Integration Guide](04-COPILOT-INTEGRATION-GUIDE.md))
- **Azure Data Studio** or **pgAdmin** for database management
- **MongoDB Compass** for MongoDB management
- **Postman** or similar for API testing

### System Requirements
- **RAM**: Minimum 8GB, Recommended 16GB+
- **Storage**: At least 10GB free space
- **OS**: Windows 10/11, macOS 10.15+, or Ubuntu 20.04+

---

## 🚀 Quick Start (5 minutes)

### 1. Clone the Repository
```bash
git clone https://github.com/Hug0lvt/DriveOps.git
cd DriveOps
```

### 2. Start Infrastructure Services
```bash
# Start PostgreSQL, MongoDB, and Keycloak
docker-compose up -d postgres mongodb keycloak

# Verify services are running
docker-compose ps
```

### 3. Configure Application Settings
```bash
# Copy the template configuration
cp src/DriveOps.Api/appsettings.Development.template.json src/DriveOps.Api/appsettings.Development.json

# Update connection strings if needed (defaults should work with Docker setup)
```

### 4. Build and Run
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run database migrations
dotnet run --project tools/DatabaseMigrator

# Start the API
dotnet run --project src/DriveOps.Api
```

### 5. Access the Application
- **Web UI**: https://localhost:5001
- **API Documentation**: https://localhost:5001/swagger
- **GraphQL Playground**: https://localhost:5001/graphql

🎉 **Congratulations!** DriveOps is now running locally.

---

## 🔧 Detailed Setup

### Infrastructure Setup with Docker

The DriveOps platform requires several infrastructure components. We provide a Docker Compose configuration for easy local development:

```yaml
# docker-compose.yml (excerpt)
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: driveops_dev
      POSTGRES_USER: driveops
      POSTGRES_PASSWORD: dev_password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  mongodb:
    image: mongo:7
    ports:
      - "27017:27017"
    volumes:
      - mongodb_data:/data/db

  keycloak:
    image: quay.io/keycloak/keycloak:23.0
    environment:
      KEYCLOAK_ADMIN: admin
      KEYCLOAK_ADMIN_PASSWORD: admin
    ports:
      - "8080:8080"
    command: ["start-dev"]
```

#### Start Infrastructure
```bash
# Start all services
docker-compose up -d

# Check service status
docker-compose ps

# View logs if needed
docker-compose logs postgres
docker-compose logs mongodb
docker-compose logs keycloak
```

#### Stop Infrastructure
```bash
# Stop services (data persists)
docker-compose stop

# Stop and remove containers (data persists)
docker-compose down

# Stop and remove everything including data
docker-compose down -v
```

### Database Setup

#### Automatic Migration (Recommended)
```bash
# Run the database migrator tool
dotnet run --project tools/DatabaseMigrator

# This will:
# - Create the database if it doesn't exist
# - Run all pending migrations
# - Set up initial schema
```

#### Manual Migration (Advanced)
```bash
# Generate new migration
dotnet ef migrations add InitialCreate --project src/DriveOps.Infrastructure --startup-project src/DriveOps.Api

# Apply migrations
dotnet ef database update --project src/DriveOps.Infrastructure --startup-project src/DriveOps.Api
```

### Keycloak Configuration

#### Automatic Setup
The DatabaseMigrator tool also configures Keycloak with default realms and clients:

```bash
# This configures:
# - DriveOps realm
# - API client
# - Web client
# - Default roles and permissions
dotnet run --project tools/KeycloakConfigurator
```

#### Manual Setup (if needed)
1. Open Keycloak Admin Console: http://localhost:8080
2. Login with admin/admin
3. Import configuration from `config/keycloak/driveops-realm.json`

---

## 📊 Sample Data

### Load Sample Data
```bash
# Load sample tenants, users, and vehicles
dotnet run --project tools/DataSeeder -- --environment Development

# This creates:
# - Sample tenant: "ACME Garage"
# - Admin user: admin@acme.garage / password
# - Sample vehicles and customers
# - Test interventions
```

### Sample Accounts
After loading sample data, you can log in with:

| Role | Email | Password | Description |
|------|-------|----------|-------------|
| **Super Admin** | admin@driveops.com | admin123 | Platform administration |
| **Garage Owner** | owner@acme.garage | owner123 | Garage management |
| **Mechanic** | mechanic@acme.garage | mech123 | Intervention management |
| **Customer** | customer@acme.garage | cust123 | Customer portal |

---

## 🏗️ Development Environment

### Project Structure
```
DriveOps/
├── src/                          # Source code
│   ├── DriveOps.Api/            # Main API project
│   ├── DriveOps.Web/            # Blazor web application
│   ├── DriveOps.Shared/         # Shared libraries
│   └── modules/                 # Business modules
│       ├── DriveOps.Garage/     # Garage module
│       ├── DriveOps.Breakdown/  # Breakdown module
│       └── ...                  # Other modules
├── tests/                       # Test projects
├── tools/                       # Development tools
├── docs/                        # Documentation
└── docker-compose.yml          # Infrastructure setup
```

### Development Workflow

#### 1. Create a Feature Branch
```bash
git checkout -b feature/your-feature-name
```

#### 2. Make Changes
- Follow our [Development Workflow](03-DEVELOPMENT-WORKFLOW.md)
- Use [GitHub Copilot](04-COPILOT-INTEGRATION-GUIDE.md) for acceleration
- Write tests for new features

#### 3. Test Your Changes
```bash
# Run unit tests
dotnet test

# Run integration tests
dotnet test --filter Category=Integration

# Run specific module tests
dotnet test tests/DriveOps.Garage.Tests/
```

#### 4. Build and Verify
```bash
# Build the solution
dotnet build

# Run code formatting
dotnet format

# Run static analysis
dotnet format --verify-no-changes
```

---

## 🧪 Testing Your Setup

### API Testing
```bash
# Test the health endpoint
curl https://localhost:5001/health

# Test authentication (get token)
curl -X POST https://localhost:5001/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@driveops.com","password":"admin123"}'

# Test a protected endpoint
curl https://localhost:5001/api/tenants \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Database Testing
```bash
# Connect to PostgreSQL
docker exec -it driveops_postgres psql -U driveops -d driveops_dev

# Check tables
\dt

# View sample data
SELECT * FROM tenants LIMIT 5;
```

### MongoDB Testing
```bash
# Connect to MongoDB
docker exec -it driveops_mongodb mongosh

# Switch to DriveOps database
use driveops_dev

# Check collections
show collections

# View sample data
db.files.find().limit(5)
```

---

## 🔧 Configuration

### Application Settings
Key configuration files:

#### `appsettings.Development.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=driveops_dev;Username=driveops;Password=dev_password",
    "MongoConnection": "mongodb://localhost:27017/driveops_dev"
  },
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/driveops",
    "ClientId": "driveops-api",
    "ClientSecret": "your-client-secret"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Environment Variables
```bash
# Database
export POSTGRES_HOST=localhost
export POSTGRES_DB=driveops_dev
export POSTGRES_USER=driveops
export POSTGRES_PASSWORD=dev_password

# MongoDB
export MONGO_HOST=localhost
export MONGO_DB=driveops_dev

# Keycloak
export KEYCLOAK_URL=http://localhost:8080
export KEYCLOAK_REALM=driveops
export KEYCLOAK_CLIENT_ID=driveops-api
```

---

## 🚀 Next Steps

### For New Developers
1. **Read Architecture Documentation**: [Architecture Overview](02-ARCHITECTURE-OVERVIEW.md)
2. **Understand Core Modules**: [Core Architecture](10-CORE-ARCHITECTURE.md)
3. **Learn Development Workflow**: [Development Workflow](03-DEVELOPMENT-WORKFLOW.md)
4. **Set up GitHub Copilot**: [Copilot Integration Guide](04-COPILOT-INTEGRATION-GUIDE.md)

### For Contributors
1. **Review Contribution Guidelines**: [Development Workflow](03-DEVELOPMENT-WORKFLOW.md)
2. **Understand Business Modules**: [Business Modules Overview](20-BUSINESS-MODULES-OVERVIEW.md)
3. **Learn Testing Strategy**: [Testing Strategy](31-TESTING-STRATEGY.md)
4. **Review Security Architecture**: [Security Architecture](13-SECURITY-ARCHITECTURE.md)

### Common Development Tasks
- **Add a new business module**: See module examples in `docs/2x-*-MODULE.md`
- **Create a new API endpoint**: Follow patterns in existing controllers
- **Add database migrations**: Use `dotnet ef migrations add`
- **Implement new UI components**: Use Blazor with Radzen components

---

## 🛠️ Troubleshooting

### Common Issues

#### "Connection refused" errors
```bash
# Check if Docker services are running
docker-compose ps

# Restart services
docker-compose restart

# Check logs
docker-compose logs
```

#### Database connection issues
```bash
# Verify PostgreSQL is accessible
docker exec -it driveops_postgres pg_isready

# Check connection string in appsettings.json
# Ensure database exists
```

#### Keycloak authentication issues
```bash
# Verify Keycloak is running
curl http://localhost:8080/health

# Check realm configuration
# Verify client settings in Keycloak admin console
```

#### Port conflicts
```bash
# Check what's using port 5001
lsof -i :5001

# Stop the process or change ports in launchSettings.json
```

### Getting Help
- **Documentation**: Check relevant documentation in `/docs`
- **Issues**: Create a GitHub issue with error details
- **Discussions**: Use GitHub Discussions for questions
- **Logs**: Check application logs in console output

---

## 📝 Development Checklist

Before you start developing:
- [ ] All infrastructure services are running
- [ ] Database migrations are applied
- [ ] Sample data is loaded
- [ ] Application starts without errors
- [ ] Health endpoints return OK
- [ ] Authentication works with sample users
- [ ] GitHub Copilot is configured (optional but recommended)

---

**You're now ready to start developing with DriveOps! 🚀**

For detailed development workflows and advanced configuration, see our comprehensive documentation in the `/docs` folder.

---

*Last updated: 2024-12-19*  
*Version: 1.0.0*