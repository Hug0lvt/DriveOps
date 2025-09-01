# DriveOps - DIAGNOSTIC IA + OBD Commercial Module

## Overview

The DIAGNOSTIC IA + OBD module is a revolutionary automotive diagnostic system that combines IoT hardware (OBD WiFi devices) with artificial intelligence to assist mechanics in real-time diagnostics. This module represents a cutting-edge approach to vehicle diagnostics, integrating hardware sales, software subscriptions, and professional services into a comprehensive commercial offering.

### Key Value Propositions

- **Hardware + Software Model**: Recurring revenue from software subscriptions combined with one-time hardware sales
- **AI as a Service**: Premium pricing for AI-powered diagnostic assistance and predictive maintenance
- **Data Monetization**: Anonymized diagnostic data creates valuable industry insights
- **Training & Certification**: Professional services for mechanic AI training and certification
- **Predictive Analytics**: Proactive maintenance recommendations drive service revenue

### Business Model Innovation

This module introduces several innovative revenue streams:
- **OBD Hardware Sales**: DriveOps OBD devices (150€-300€) with WiFi 6 and multi-protocol support
- **Diagnostic Software Subscriptions**: Basic (49€/month) and IA Premium (+29€/month) tiers
- **Professional Services**: Implementation, training, custom integrations (500€-10,000€)
- **Data Analytics**: Industry insights and manufacturer partnerships
- **Predictive Maintenance**: AI-driven service scheduling and cost optimization

---

## Module Scope and Features

### Core Diagnostic Capabilities

#### IoT Hardware Integration
- **DriveOps OBD WiFi Devices**: Multi-vehicle support with WiFi 6 and Bluetooth 5.0
- **Multi-Protocol Support**: CAN, K-Line, J1850, ISO protocols for universal compatibility
- **Real-Time Streaming**: 10Hz+ data transmission with low-power optimization
- **Security**: Encrypted communication with device authentication and OTA updates

#### Real-Time Diagnostics
- **Instant Vehicle Analysis**: Live vehicle health monitoring and fault detection
- **Comprehensive Fault Codes**: Deep integration with manufacturer diagnostic databases
- **Live Data Streaming**: Real-time sensor data visualization and analysis
- **Historical Data**: Trend analysis and pattern recognition for diagnostic insights

#### AI-Powered Assistance
- **Machine Learning Recommendations**: AI-driven repair and maintenance suggestions
- **Fault Classification**: Deep learning for accurate error code interpretation
- **Anomaly Detection**: Unsupervised learning for unusual pattern identification
- **Natural Language Processing**: AI-enhanced troubleshooting and repair procedures

#### Predictive Maintenance
- **Component Life Prediction**: AI-driven failure forecasting based on vehicle data patterns
- **Maintenance Scheduling**: Optimal service timing recommendations
- **Cost Optimization**: Budget planning and maintenance expense prediction
- **Performance Monitoring**: Vehicle efficiency and health tracking

#### Mobile Diagnostics
- **Technician Mobile Apps**: Offline-capable diagnostic interface with AR guidance
- **Customer Mobile Apps**: Real-time vehicle health and maintenance alerts
- **Voice Commands**: Hands-free operation for technicians
- **Digital Documentation**: Before/after photos and digital signatures

#### Knowledge Base Integration
- **AI-Enhanced Troubleshooting**: Interactive diagnostic trees and repair procedures
- **Technical Documentation**: Context-aware search and retrieval
- **Parts Identification**: Image recognition for component identification
- **Training Content**: Personalized learning recommendations

#### Fleet Monitoring
- **Multi-Vehicle Dashboards**: Fleet health overview and performance analytics
- **Centralized Management**: Multiple devices per garage/technician coordination
- **Compliance Monitoring**: Emissions and safety regulation tracking
- **Cost Analytics**: Fleet maintenance budget optimization and ROI calculation

---

## Business Domain Model

### Hardware Domain

```csharp
namespace DriveOps.Diagnostics.Domain.Hardware
{
    // OBD Device aggregate root
    public class OBDDevice : AggregateRoot
    {
        public OBDDeviceId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public string SerialNumber { get; private set; }
        public DeviceModel Model { get; private set; }
        public FirmwareVersion FirmwareVersion { get; private set; }
        public DeviceStatus Status { get; private set; }
        public DeviceConfiguration Configuration { get; private set; }
        public UserId? AssignedTechnicianId { get; private set; }
        public DateTime LastHeartbeat { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }

        private readonly List<VehicleConnection> _connections = new();
        public IReadOnlyCollection<VehicleConnection> Connections => _connections.AsReadOnly();

        private readonly List<DeviceAlert> _alerts = new();
        public IReadOnlyCollection<DeviceAlert> Alerts => _alerts.AsReadOnly();

        public void AssignToTechnician(UserId technicianId, UserId assignedBy)
        {
            AssignedTechnicianId = technicianId;
            AddDomainEvent(new DeviceAssignedEvent(TenantId, Id, technicianId, assignedBy));
        }

        public void UpdateConfiguration(DeviceConfiguration newConfig, UserId updatedBy)
        {
            Configuration = newConfig;
            AddDomainEvent(new DeviceConfigurationUpdatedEvent(TenantId, Id, newConfig, updatedBy));
        }

        public void RecordHeartbeat(DeviceHealthMetrics metrics)
        {
            LastHeartbeat = DateTime.UtcNow;
            Status = Status.UpdateWithMetrics(metrics);
            
            if (metrics.HasCriticalIssues())
            {
                AddAlert(new DeviceAlert(AlertType.Critical, metrics.GetCriticalIssues()));
            }
        }

        public VehicleConnection ConnectToVehicle(VehicleId vehicleId, OBDProtocol protocol)
        {
            var connection = new VehicleConnection(Id, vehicleId, protocol);
            _connections.Add(connection);
            AddDomainEvent(new VehicleConnectedEvent(TenantId, Id, vehicleId, protocol));
            return connection;
        }
    }

    // Vehicle connection value object
    public class VehicleConnection : ValueObject
    {
        public OBDDeviceId DeviceId { get; }
        public VehicleId VehicleId { get; }
        public OBDProtocol Protocol { get; }
        public ConnectionStatus Status { get; private set; }
        public DateTime ConnectedAt { get; }
        public DateTime? DisconnectedAt { get; private set; }
        public TimeSpan? SessionDuration => DisconnectedAt?.Subtract(ConnectedAt);

        public VehicleConnection(OBDDeviceId deviceId, VehicleId vehicleId, OBDProtocol protocol)
        {
            DeviceId = deviceId;
            VehicleId = vehicleId;
            Protocol = protocol;
            Status = ConnectionStatus.Establishing;
            ConnectedAt = DateTime.UtcNow;
        }

        public void EstablishConnection()
        {
            Status = ConnectionStatus.Connected;
        }

        public void Disconnect(DisconnectReason reason)
        {
            Status = ConnectionStatus.Disconnected;
            DisconnectedAt = DateTime.UtcNow;
        }
    }

    // Sensor data value object
    public class SensorData : ValueObject
    {
        public SensorType Type { get; }
        public string Name { get; }
        public decimal Value { get; }
        public string Unit { get; }
        public SensorDataQuality Quality { get; }
        public DateTime Timestamp { get; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Type;
            yield return Name;
            yield return Value;
            yield return Unit;
            yield return Timestamp;
        }
    }

    // Enums and value objects
    public enum DeviceModel
    {
        OBDBasic = 1,
        OBDPro = 2,
        OBDEnterprise = 3
    }

    public enum OBDProtocol
    {
        CAN = 1,
        KLine = 2,
        J1850PWM = 3,
        J1850VPW = 4,
        ISO9141 = 5,
        ISO14230 = 6
    }

    public enum ConnectionStatus
    {
        Disconnected = 0,
        Establishing = 1,
        Connected = 2,
        Error = 3
    }
}
```

### Diagnostic Domain

```csharp
namespace DriveOps.Diagnostics.Domain.Diagnostics
{
    // Diagnostic session aggregate root
    public class DiagnosticSession : AggregateRoot
    {
        public DiagnosticSessionId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public VehicleId VehicleId { get; private set; }
        public OBDDeviceId DeviceId { get; private set; }
        public UserId TechnicianId { get; private set; }
        public SessionStatus Status { get; private set; }
        public DateTime StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public TimeSpan? Duration => CompletedAt?.Subtract(StartedAt);

        private readonly List<FaultCode> _faultCodes = new();
        public IReadOnlyCollection<FaultCode> FaultCodes => _faultCodes.AsReadOnly();

        private readonly List<Symptom> _symptoms = new();
        public IReadOnlyCollection<Symptom> Symptoms => _symptoms.AsReadOnly();

        private readonly List<SensorReading> _sensorReadings = new();
        public IReadOnlyCollection<SensorReading> SensorReadings => _sensorReadings.AsReadOnly();

        private readonly List<Diagnosis> _diagnoses = new();
        public IReadOnlyCollection<Diagnosis> Diagnoses => _diagnoses.AsReadOnly();

        public void AddFaultCode(string code, string description, FaultSeverity severity)
        {
            var faultCode = new FaultCode(code, description, severity, DateTime.UtcNow);
            _faultCodes.Add(faultCode);
            AddDomainEvent(new FaultCodeDetectedEvent(TenantId, Id, VehicleId, faultCode));
        }

        public void AddSymptom(string description, SymptomType type, SymptomSeverity severity)
        {
            var symptom = new Symptom(description, type, severity, DateTime.UtcNow);
            _symptoms.Add(symptom);
        }

        public void RecordSensorReading(SensorType sensorType, decimal value, string unit)
        {
            var reading = new SensorReading(sensorType, value, unit, DateTime.UtcNow);
            _sensorReadings.Add(reading);
        }

        public void AddDiagnosis(string description, DiagnosisConfidence confidence, 
            List<RepairRecommendation> recommendations)
        {
            var diagnosis = new Diagnosis(description, confidence, recommendations, DateTime.UtcNow);
            _diagnoses.Add(diagnosis);
            AddDomainEvent(new DiagnosisCompletedEvent(TenantId, Id, VehicleId, diagnosis));
        }

        public void CompleteSession(SessionOutcome outcome)
        {
            Status = SessionStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            AddDomainEvent(new DiagnosticSessionCompletedEvent(TenantId, Id, VehicleId, outcome, Duration.Value));
        }
    }

    // Fault code value object
    public class FaultCode : ValueObject
    {
        public string Code { get; }
        public string Description { get; }
        public FaultSeverity Severity { get; }
        public DateTime DetectedAt { get; }
        public bool IsActive { get; }
        public string? ManufacturerSpecific { get; }

        public FaultCode(string code, string description, FaultSeverity severity, DateTime detectedAt)
        {
            Code = code;
            Description = description;
            Severity = severity;
            DetectedAt = detectedAt;
            IsActive = true;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Code;
            yield return Severity;
            yield return DetectedAt;
        }
    }

    // Diagnosis entity
    public class Diagnosis : Entity
    {
        public DiagnosisId Id { get; private set; }
        public string Description { get; private set; }
        public DiagnosisConfidence Confidence { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public UserId? ReviewedBy { get; private set; }
        public DateTime? ReviewedAt { get; private set; }

        private readonly List<RepairRecommendation> _recommendations = new();
        public IReadOnlyCollection<RepairRecommendation> Recommendations => _recommendations.AsReadOnly();

        public void AddRecommendation(RepairRecommendation recommendation)
        {
            _recommendations.Add(recommendation);
        }

        public void MarkAsReviewed(UserId reviewerId)
        {
            ReviewedBy = reviewerId;
            ReviewedAt = DateTime.UtcNow;
        }
    }

    // Repair recommendation value object
    public class RepairRecommendation : ValueObject
    {
        public string Action { get; }
        public RepairPriority Priority { get; }
        public decimal EstimatedCost { get; }
        public TimeSpan EstimatedDuration { get; }
        public List<string> RequiredParts { get; }
        public string? SpecialTools { get; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Action;
            yield return Priority;
            yield return EstimatedCost;
        }
    }

    // Enums
    public enum SessionStatus
    {
        Initiated = 0,
        InProgress = 1,
        Completed = 2,
        Cancelled = 3,
        Error = 4
    }

    public enum FaultSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }

    public enum DiagnosisConfidence
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Confirmed = 3
    }

    public enum RepairPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Urgent = 3
    }
}
```

### AI Domain

```csharp
namespace DriveOps.Diagnostics.Domain.AI
{
    // AI model aggregate root
    public class AIModel : AggregateRoot
    {
        public AIModelId Id { get; private set; }
        public string Name { get; private set; }
        public ModelType Type { get; private set; }
        public ModelVersion Version { get; private set; }
        public ModelStatus Status { get; private set; }
        public DateTime TrainedAt { get; private set; }
        public decimal Accuracy { get; private set; }
        public ModelMetrics Metrics { get; private set; }
        public string ConfigurationJson { get; private set; }

        private readonly List<TrainingDataset> _trainingData = new();
        public IReadOnlyCollection<TrainingDataset> TrainingData => _trainingData.AsReadOnly();

        private readonly List<PredictionResult> _predictions = new();
        public IReadOnlyCollection<PredictionResult> Predictions => _predictions.AsReadOnly();

        public void UpdateModel(ModelVersion newVersion, decimal accuracy, ModelMetrics metrics)
        {
            Version = newVersion;
            Accuracy = accuracy;
            Metrics = metrics;
            TrainedAt = DateTime.UtcNow;
            AddDomainEvent(new AIModelUpdatedEvent(Id, newVersion, accuracy));
        }

        public PredictionResult MakePrediction(PredictionInput input)
        {
            var result = new PredictionResult(Id, input, DateTime.UtcNow);
            _predictions.Add(result);
            return result;
        }

        public void AddTrainingData(TrainingDataset dataset)
        {
            _trainingData.Add(dataset);
        }
    }

    // Predictive analysis aggregate root
    public class PredictiveAnalysis : AggregateRoot
    {
        public PredictiveAnalysisId Id { get; private set; }
        public TenantId TenantId { get; private set; }
        public VehicleId VehicleId { get; private set; }
        public AnalysisType Type { get; private set; }
        public DateTime AnalyzedAt { get; private set; }
        public PredictionTimeframe Timeframe { get; private set; }

        private readonly List<ComponentPrediction> _componentPredictions = new();
        public IReadOnlyCollection<ComponentPrediction> ComponentPredictions => _componentPredictions.AsReadOnly();

        private readonly List<MaintenanceRecommendation> _maintenanceRecommendations = new();
        public IReadOnlyCollection<MaintenanceRecommendation> MaintenanceRecommendations => _maintenanceRecommendations.AsReadOnly();

        public void AddComponentPrediction(string componentName, FailureProbability probability, 
            DateTime estimatedFailureDate, decimal replacementCost)
        {
            var prediction = new ComponentPrediction(componentName, probability, estimatedFailureDate, replacementCost);
            _componentPredictions.Add(prediction);
        }

        public void AddMaintenanceRecommendation(MaintenanceType type, DateTime recommendedDate, 
            string description, decimal estimatedCost)
        {
            var recommendation = new MaintenanceRecommendation(type, recommendedDate, description, estimatedCost);
            _maintenanceRecommendations.Add(recommendation);
        }
    }

    // Knowledge base entry entity
    public class KnowledgeBaseEntry : Entity
    {
        public KnowledgeEntryId Id { get; private set; }
        public string Title { get; private set; }
        public string Content { get; private set; }
        public EntryType Type { get; private set; }
        public List<string> Tags { get; private set; }
        public decimal Relevance { get; private set; }
        public UserId AuthorId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public void UpdateRelevance(decimal newRelevance)
        {
            Relevance = newRelevance;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddTag(string tag)
        {
            if (!Tags.Contains(tag))
            {
                Tags.Add(tag);
                UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    // Value objects
    public class ComponentPrediction : ValueObject
    {
        public string ComponentName { get; }
        public FailureProbability Probability { get; }
        public DateTime EstimatedFailureDate { get; }
        public decimal ReplacementCost { get; }
        public string? WarningMessage { get; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return ComponentName;
            yield return Probability;
            yield return EstimatedFailureDate;
        }
    }

    public class MaintenanceRecommendation : ValueObject
    {
        public MaintenanceType Type { get; }
        public DateTime RecommendedDate { get; }
        public string Description { get; }
        public decimal EstimatedCost { get; }
        public RecommendationPriority Priority { get; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Type;
            yield return RecommendedDate;
            yield return Description;
        }
    }

    // Enums
    public enum ModelType
    {
        FaultClassification = 1,
        AnomalyDetection = 2,
        PredictiveMaintenance = 3,
        RepairRecommendation = 4,
        CostEstimation = 5
    }

    public enum AnalysisType
    {
        ComponentHealth = 1,
        MaintenanceScheduling = 2,
        PerformanceOptimization = 3,
        CostPrediction = 4
    }

    public enum FailureProbability
    {
        VeryLow = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        VeryHigh = 4,
        Imminent = 5
    }

    public enum MaintenanceType
    {
        Preventive = 1,
        Predictive = 2,
        Corrective = 3,
        Emergency = 4
    }
}
```

### Vehicle Integration Domain

```csharp
namespace DriveOps.Diagnostics.Domain.VehicleIntegration
{
    // Vehicle profile aggregate root
    public class VehicleProfile : AggregateRoot
    {
        public VehicleProfileId Id { get; private set; }
        public VehicleId VehicleId { get; private set; }
        public TenantId TenantId { get; private set; }
        public ManufacturerData ManufacturerData { get; private set; }
        public TechnicalSpecification TechnicalSpec { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private readonly List<ServiceRecord> _serviceHistory = new();
        public IReadOnlyCollection<ServiceRecord> ServiceHistory => _serviceHistory.AsReadOnly();

        private readonly List<DiagnosticConfiguration> _diagnosticConfigs = new();
        public IReadOnlyCollection<DiagnosticConfiguration> DiagnosticConfigurations => _diagnosticConfigs.AsReadOnly();

        public void AddServiceRecord(ServiceRecord record)
        {
            _serviceHistory.Add(record);
            UpdatedAt = DateTime.UtcNow;
            AddDomainEvent(new ServiceRecordAddedEvent(TenantId, VehicleId, record));
        }

        public void UpdateDiagnosticConfiguration(OBDProtocol protocol, Dictionary<string, string> parameters)
        {
            var config = new DiagnosticConfiguration(protocol, parameters);
            _diagnosticConfigs.Add(config);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    // Manufacturer data value object
    public class ManufacturerData : ValueObject
    {
        public string ManufacturerName { get; }
        public string Brand { get; }
        public string Model { get; }
        public int Year { get; }
        public string EngineCode { get; }
        public string TransmissionType { get; }
        public List<SupportedProtocol> SupportedProtocols { get; }
        public Dictionary<string, string> SpecificParameters { get; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return ManufacturerName;
            yield return Brand;
            yield return Model;
            yield return Year;
            yield return EngineCode;
        }
    }

    // Technical specification value object
    public class TechnicalSpecification : ValueObject
    {
        public EngineSpecification Engine { get; }
        public TransmissionSpecification Transmission { get; }
        public ElectricalSpecification Electrical { get; }
        public EmissionSpecification Emission { get; }
        public List<SensorSpecification> Sensors { get; }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Engine;
            yield return Transmission;
            yield return Electrical;
            yield return Emission;
        }
    }

    // Service record entity
    public class ServiceRecord : Entity
    {
        public ServiceRecordId Id { get; private set; }
        public DateTime ServiceDate { get; private set; }
        public ServiceType Type { get; private set; }
        public string Description { get; private set; }
        public int MileageAtService { get; private set; }
        public decimal Cost { get; private set; }
        public string ServiceProvider { get; private set; }
        public List<string> PartsReplaced { get; private set; }
        public List<string> WorkPerformed { get; private set; }

        public void UpdateRecord(string description, decimal cost, List<string> partsReplaced)
        {
            Description = description;
            Cost = cost;
            PartsReplaced = partsReplaced;
        }
    }
}
```

---

## Complete C# Architecture

### Domain Layer - Core Entities and Value Objects

```csharp
// Strong-typed IDs
namespace DriveOps.Diagnostics.Domain.Common
{
    public record OBDDeviceId(Guid Value) : EntityId(Value);
    public record DiagnosticSessionId(Guid Value) : EntityId(Value);
    public record AIModelId(Guid Value) : EntityId(Value);
    public record PredictiveAnalysisId(Guid Value) : EntityId(Value);
    public record VehicleProfileId(Guid Value) : EntityId(Value);
    public record DiagnosisId(Guid Value) : EntityId(Value);
    public record KnowledgeEntryId(Guid Value) : EntityId(Value);
    public record ServiceRecordId(Guid Value) : EntityId(Value);

    // Base domain events
    public abstract record DiagnosticDomainEvent : DomainEvent
    {
        protected DiagnosticDomainEvent(TenantId tenantId) : base(tenantId) { }
    }

    // Device events
    public record DeviceAssignedEvent(
        TenantId TenantId,
        OBDDeviceId DeviceId,
        UserId TechnicianId,
        UserId AssignedBy
    ) : DiagnosticDomainEvent(TenantId);

    public record VehicleConnectedEvent(
        TenantId TenantId,
        OBDDeviceId DeviceId,
        VehicleId VehicleId,
        OBDProtocol Protocol
    ) : DiagnosticDomainEvent(TenantId);

    public record FaultCodeDetectedEvent(
        TenantId TenantId,
        DiagnosticSessionId SessionId,
        VehicleId VehicleId,
        FaultCode FaultCode
    ) : DiagnosticDomainEvent(TenantId);

    public record DiagnosisCompletedEvent(
        TenantId TenantId,
        DiagnosticSessionId SessionId,
        VehicleId VehicleId,
        Diagnosis Diagnosis
    ) : DiagnosticDomainEvent(TenantId);

    public record AIModelUpdatedEvent(
        AIModelId ModelId,
        ModelVersion Version,
        decimal Accuracy
    ) : DomainEvent;

    public record PredictiveMaintenanceAlertEvent(
        TenantId TenantId,
        VehicleId VehicleId,
        ComponentPrediction Prediction
    ) : DiagnosticDomainEvent(TenantId);
}
```

### Application Layer - CQRS Commands and Queries

```csharp
namespace DriveOps.Diagnostics.Application.Commands
{
    // Device management commands
    public record RegisterOBDDeviceCommand(
        string TenantId,
        string SerialNumber,
        DeviceModel Model,
        string InitialFirmwareVersion
    ) : ICommand<Result<Guid>>;

    public record AssignDeviceToTechnicianCommand(
        string TenantId,
        Guid DeviceId,
        Guid TechnicianId,
        Guid AssignedBy
    ) : ICommand<Result>;

    public record UpdateDeviceConfigurationCommand(
        string TenantId,
        Guid DeviceId,
        DeviceConfiguration Configuration,
        Guid UpdatedBy
    ) : ICommand<Result>;

    // Diagnostic session commands
    public record StartDiagnosticSessionCommand(
        string TenantId,
        Guid VehicleId,
        Guid DeviceId,
        Guid TechnicianId
    ) : ICommand<Result<Guid>>;

    public record RecordFaultCodeCommand(
        string TenantId,
        Guid SessionId,
        string FaultCode,
        string Description,
        FaultSeverity Severity
    ) : ICommand<Result>;

    public record CompleteDiagnosisCommand(
        string TenantId,
        Guid SessionId,
        string DiagnosisDescription,
        DiagnosisConfidence Confidence,
        List<RepairRecommendation> Recommendations
    ) : ICommand<Result>;

    // AI prediction commands
    public record RequestPredictiveAnalysisCommand(
        string TenantId,
        Guid VehicleId,
        AnalysisType Type,
        PredictionTimeframe Timeframe
    ) : ICommand<Result<Guid>>;

    public record TrainAIModelCommand(
        AIModelId ModelId,
        List<TrainingDataPoint> TrainingData,
        ModelTrainingParameters Parameters
    ) : ICommand<Result>;

    // Real-time streaming commands
    public record StartRealTimeStreamingCommand(
        string TenantId,
        Guid SessionId,
        Guid DeviceId,
        List<SensorType> RequestedSensors
    ) : ICommand<Result>;

    public record ProcessSensorDataCommand(
        string TenantId,
        Guid SessionId,
        List<SensorReading> SensorData
    ) : ICommand<Result>;
}

namespace DriveOps.Diagnostics.Application.Queries
{
    // Device queries
    public record GetTechnicianDevicesQuery(
        string TenantId,
        Guid TechnicianId
    ) : IQuery<Result<List<OBDDeviceDto>>>;

    public record GetDeviceStatusQuery(
        string TenantId,
        Guid DeviceId
    ) : IQuery<Result<DeviceStatusDto>>;

    public record GetDeviceUsageAnalyticsQuery(
        string TenantId,
        Guid DeviceId,
        DateOnly FromDate,
        DateOnly ToDate
    ) : IQuery<Result<DeviceUsageAnalyticsDto>>;

    // Diagnostic session queries
    public record GetActiveDiagnosticSessionsQuery(
        string TenantId,
        Guid? TechnicianId = null
    ) : IQuery<Result<List<DiagnosticSessionDto>>>;

    public record GetDiagnosticSessionDetailsQuery(
        string TenantId,
        Guid SessionId
    ) : IQuery<Result<DiagnosticSessionDetailsDto>>;

    public record GetVehicleDiagnosticHistoryQuery(
        string TenantId,
        Guid VehicleId,
        int PageNumber = 1,
        int PageSize = 20
    ) : IQuery<Result<PagedResult<DiagnosticSessionSummaryDto>>>;

    // AI and predictive queries
    public record GetVehiclePredictiveAnalysisQuery(
        string TenantId,
        Guid VehicleId
    ) : IQuery<Result<PredictiveAnalysisDto>>;

    public record GetMaintenanceRecommendationsQuery(
        string TenantId,
        Guid VehicleId,
        RecommendationPriority? MinimumPriority = null
    ) : IQuery<Result<List<MaintenanceRecommendationDto>>>;

    public record SearchKnowledgeBaseQuery(
        string TenantId,
        string SearchTerm,
        EntryType? Type = null,
        List<string>? Tags = null
    ) : IQuery<Result<List<KnowledgeBaseEntryDto>>>;

    // Real-time data queries
    public record GetRealTimeSensorDataQuery(
        string TenantId,
        Guid SessionId,
        List<SensorType> SensorTypes
    ) : IQuery<Result<List<SensorReadingDto>>>;

    public record GetLiveVehicleHealthQuery(
        string TenantId,
        Guid VehicleId
    ) : IQuery<Result<VehicleHealthStatusDto>>;

    // Fleet monitoring queries
    public record GetFleetDiagnosticOverviewQuery(
        string TenantId,
        List<Guid> VehicleIds
    ) : IQuery<Result<FleetDiagnosticOverviewDto>>;

    public record GetFleetHealthAlertsQuery(
        string TenantId,
        AlertSeverity? MinimumSeverity = null
    ) : IQuery<Result<List<FleetHealthAlertDto>>>;
}
```

### Application Layer - Command Handlers

```csharp
namespace DriveOps.Diagnostics.Application.Handlers.Commands
{
    public class StartDiagnosticSessionCommandHandler : CommandHandler<StartDiagnosticSessionCommand, Result<Guid>>
    {
        private readonly IDiagnosticSessionRepository _sessionRepository;
        private readonly IOBDDeviceRepository _deviceRepository;
        private readonly IVehicleRepository _vehicleRepository;
        private readonly IRealTimeStreamingService _streamingService;
        private readonly IDomainEventDispatcher _eventDispatcher;

        public StartDiagnosticSessionCommandHandler(
            ITenantContext tenantContext,
            ILogger<StartDiagnosticSessionCommandHandler> logger,
            IUnitOfWork unitOfWork,
            IDiagnosticSessionRepository sessionRepository,
            IOBDDeviceRepository deviceRepository,
            IVehicleRepository vehicleRepository,
            IRealTimeStreamingService streamingService,
            IDomainEventDispatcher eventDispatcher)
            : base(tenantContext, logger, unitOfWork)
        {
            _sessionRepository = sessionRepository;
            _deviceRepository = deviceRepository;
            _vehicleRepository = vehicleRepository;
            _streamingService = streamingService;
            _eventDispatcher = eventDispatcher;
        }

        protected override async Task<Result<Guid>> HandleCommandAsync(
            StartDiagnosticSessionCommand request,
            CancellationToken cancellationToken)
        {
            // Validate device exists and is available
            var device = await _deviceRepository.GetByIdAsync(
                OBDDeviceId.From(request.DeviceId), cancellationToken);
            
            if (device == null)
                return Result.Failure<Guid>("OBD device not found");

            if (device.Status != DeviceStatus.Available)
                return Result.Failure<Guid>("OBD device is not available");

            // Validate vehicle exists
            var vehicle = await _vehicleRepository.GetByIdAsync(
                VehicleId.From(request.VehicleId), cancellationToken);
            
            if (vehicle == null)
                return Result.Failure<Guid>("Vehicle not found");

            // Check for existing active sessions
            var existingSession = await _sessionRepository.GetActiveSessionForDeviceAsync(
                device.Id, cancellationToken);
            
            if (existingSession != null)
                return Result.Failure<Guid>("Device already has an active diagnostic session");

            // Create new diagnostic session
            var session = new DiagnosticSession(
                TenantId.From(Guid.Parse(request.TenantId)),
                VehicleId.From(request.VehicleId),
                OBDDeviceId.From(request.DeviceId),
                UserId.From(request.TechnicianId)
            );

            // Start real-time streaming
            await _streamingService.StartStreamingAsync(session.Id, device.Id, cancellationToken);

            // Save session
            await _sessionRepository.AddAsync(session, cancellationToken);

            // Dispatch events
            await _eventDispatcher.DispatchEventsAsync(session.DomainEvents, cancellationToken);

            return Result.Success(session.Id.Value);
        }
    }

    public class ProcessSensorDataCommandHandler : CommandHandler<ProcessSensorDataCommand, Result>
    {
        private readonly IDiagnosticSessionRepository _sessionRepository;
        private readonly IAIAnalysisService _aiAnalysisService;
        private readonly IRealTimeNotificationService _notificationService;
        private readonly IFaultCodeDatabaseService _faultCodeService;

        protected override async Task<Result> HandleCommandAsync(
            ProcessSensorDataCommand request,
            CancellationToken cancellationToken)
        {
            var session = await _sessionRepository.GetByIdAsync(
                DiagnosticSessionId.From(request.SessionId), cancellationToken);

            if (session == null)
                return Result.Failure("Diagnostic session not found");

            // Process each sensor reading
            foreach (var reading in request.SensorData)
            {
                session.RecordSensorReading(reading.Type, reading.Value, reading.Unit);

                // Check for fault conditions using AI
                var analysisResult = await _aiAnalysisService.AnalyzeSensorDataAsync(
                    session.VehicleId, reading, cancellationToken);

                if (analysisResult.HasFaults)
                {
                    foreach (var fault in analysisResult.DetectedFaults)
                    {
                        // Get fault code details
                        var faultDetails = await _faultCodeService.GetFaultDetailsAsync(
                            fault.Code, session.VehicleId, cancellationToken);

                        session.AddFaultCode(fault.Code, faultDetails.Description, fault.Severity);

                        // Send real-time notification for critical faults
                        if (fault.Severity >= FaultSeverity.Error)
                        {
                            await _notificationService.SendCriticalFaultAlertAsync(
                                session.TechnicianId, session.VehicleId, fault, cancellationToken);
                        }
                    }
                }

                // Send real-time data to connected clients
                await _notificationService.SendSensorDataUpdateAsync(
                    session.Id, reading, cancellationToken);
            }

            await _sessionRepository.UpdateAsync(session, cancellationToken);

            return Result.Success();
        }
    }

    public class RequestPredictiveAnalysisCommandHandler : CommandHandler<RequestPredictiveAnalysisCommand, Result<Guid>>
    {
        private readonly IPredictiveAnalysisRepository _analysisRepository;
        private readonly IAIModelRepository _modelRepository;
        private readonly IVehicleDataRepository _vehicleDataRepository;
        private readonly IPredictiveAnalysisEngine _analysisEngine;

        protected override async Task<Result<Guid>> HandleCommandAsync(
            RequestPredictiveAnalysisCommand request,
            CancellationToken cancellationToken)
        {
            // Get the appropriate AI model for the analysis type
            var model = await _modelRepository.GetLatestModelAsync(
                GetModelTypeForAnalysis(request.Type), cancellationToken);

            if (model == null)
                return Result.Failure<Guid>("No AI model available for this analysis type");

            // Get historical vehicle data
            var historicalData = await _vehicleDataRepository.GetVehicleHistoryAsync(
                VehicleId.From(request.VehicleId), request.Timeframe, cancellationToken);

            // Create analysis
            var analysis = new PredictiveAnalysis(
                TenantId.From(Guid.Parse(request.TenantId)),
                VehicleId.From(request.VehicleId),
                request.Type,
                request.Timeframe
            );

            // Run AI analysis
            var predictions = await _analysisEngine.AnalyzeAsync(
                model, historicalData, request.Type, cancellationToken);

            // Add predictions to analysis
            foreach (var prediction in predictions.ComponentPredictions)
            {
                analysis.AddComponentPrediction(
                    prediction.ComponentName,
                    prediction.Probability,
                    prediction.EstimatedFailureDate,
                    prediction.ReplacementCost
                );
            }

            foreach (var recommendation in predictions.MaintenanceRecommendations)
            {
                analysis.AddMaintenanceRecommendation(
                    recommendation.Type,
                    recommendation.RecommendedDate,
                    recommendation.Description,
                    recommendation.EstimatedCost
                );
            }

            await _analysisRepository.AddAsync(analysis, cancellationToken);

            return Result.Success(analysis.Id.Value);
        }

        private ModelType GetModelTypeForAnalysis(AnalysisType analysisType)
        {
            return analysisType switch
            {
                AnalysisType.ComponentHealth => ModelType.AnomalyDetection,
                AnalysisType.MaintenanceScheduling => ModelType.PredictiveMaintenance,
                AnalysisType.PerformanceOptimization => ModelType.AnomalyDetection,
                AnalysisType.CostPrediction => ModelType.CostEstimation,
                _ => ModelType.PredictiveMaintenance
            };
        }
    }
}
```

### Infrastructure Layer - Real-Time Communication

```csharp
namespace DriveOps.Diagnostics.Infrastructure.RealTime
{
    public class DiagnosticHub : Hub
    {
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<DiagnosticHub> _logger;

        public DiagnosticHub(ITenantContext tenantContext, ILogger<DiagnosticHub> logger)
        {
            _tenantContext = tenantContext;
            _logger = logger;
        }

        public async Task JoinDiagnosticSession(string sessionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{sessionId}");
            _logger.LogInformation("User joined diagnostic session {SessionId}", sessionId);
        }

        public async Task LeaveDiagnosticSession(string sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session_{sessionId}");
            _logger.LogInformation("User left diagnostic session {SessionId}", sessionId);
        }

        public async Task JoinTechnicianChannel(string technicianId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"technician_{technicianId}");
        }

        public async Task JoinFleetMonitoring(string tenantId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"fleet_{tenantId}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("User disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }

    public interface IRealTimeNotificationService
    {
        Task SendSensorDataUpdateAsync(DiagnosticSessionId sessionId, SensorReading data, CancellationToken cancellationToken = default);
        Task SendFaultCodeAlertAsync(DiagnosticSessionId sessionId, FaultCode faultCode, CancellationToken cancellationToken = default);
        Task SendCriticalFaultAlertAsync(UserId technicianId, VehicleId vehicleId, DetectedFault fault, CancellationToken cancellationToken = default);
        Task SendPredictiveMaintenanceAlertAsync(string tenantId, VehicleId vehicleId, MaintenanceRecommendation recommendation, CancellationToken cancellationToken = default);
        Task SendDeviceStatusUpdateAsync(OBDDeviceId deviceId, DeviceStatus status, CancellationToken cancellationToken = default);
        Task SendFleetHealthUpdateAsync(string tenantId, FleetHealthUpdate update, CancellationToken cancellationToken = default);
    }

    public class RealTimeNotificationService : IRealTimeNotificationService
    {
        private readonly IHubContext<DiagnosticHub> _hubContext;
        private readonly ILogger<RealTimeNotificationService> _logger;

        public RealTimeNotificationService(
            IHubContext<DiagnosticHub> hubContext,
            ILogger<RealTimeNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task SendSensorDataUpdateAsync(DiagnosticSessionId sessionId, SensorReading data, CancellationToken cancellationToken = default)
        {
            try
            {
                await _hubContext.Clients.Group($"session_{sessionId.Value}")
                    .SendAsync("SensorDataUpdate", new
                    {
                        SessionId = sessionId.Value,
                        SensorType = data.Type.ToString(),
                        Value = data.Value,
                        Unit = data.Unit,
                        Timestamp = data.Timestamp,
                        Quality = data.Quality
                    }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send sensor data update for session {SessionId}", sessionId.Value);
            }
        }

        public async Task SendFaultCodeAlertAsync(DiagnosticSessionId sessionId, FaultCode faultCode, CancellationToken cancellationToken = default)
        {
            try
            {
                await _hubContext.Clients.Group($"session_{sessionId.Value}")
                    .SendAsync("FaultCodeDetected", new
                    {
                        SessionId = sessionId.Value,
                        Code = faultCode.Code,
                        Description = faultCode.Description,
                        Severity = faultCode.Severity.ToString(),
                        DetectedAt = faultCode.DetectedAt
                    }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send fault code alert for session {SessionId}", sessionId.Value);
            }
        }

        public async Task SendCriticalFaultAlertAsync(UserId technicianId, VehicleId vehicleId, DetectedFault fault, CancellationToken cancellationToken = default)
        {
            try
            {
                await _hubContext.Clients.Group($"technician_{technicianId.Value}")
                    .SendAsync("CriticalFaultAlert", new
                    {
                        VehicleId = vehicleId.Value,
                        FaultCode = fault.Code,
                        Severity = fault.Severity.ToString(),
                        Message = $"Critical fault detected: {fault.Code}",
                        Timestamp = DateTime.UtcNow
                    }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send critical fault alert to technician {TechnicianId}", technicianId.Value);
            }
        }

        public async Task SendPredictiveMaintenanceAlertAsync(string tenantId, VehicleId vehicleId, MaintenanceRecommendation recommendation, CancellationToken cancellationToken = default)
        {
            try
            {
                await _hubContext.Clients.Group($"fleet_{tenantId}")
                    .SendAsync("PredictiveMaintenanceAlert", new
                    {
                        VehicleId = vehicleId.Value,
                        Type = recommendation.Type.ToString(),
                        RecommendedDate = recommendation.RecommendedDate,
                        Description = recommendation.Description,
                        EstimatedCost = recommendation.EstimatedCost,
                        Priority = recommendation.Priority.ToString()
                    }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send predictive maintenance alert for vehicle {VehicleId}", vehicleId.Value);
            }
        }

        public async Task SendDeviceStatusUpdateAsync(OBDDeviceId deviceId, DeviceStatus status, CancellationToken cancellationToken = default)
        {
            // Implementation for device status updates
        }

        public async Task SendFleetHealthUpdateAsync(string tenantId, FleetHealthUpdate update, CancellationToken cancellationToken = default)
        {
            try
            {
                await _hubContext.Clients.Group($"fleet_{tenantId}")
                    .SendAsync("FleetHealthUpdate", new
                    {
                        VehicleId = update.VehicleId,
                        HealthScore = update.HealthScore,
                        AlertCount = update.AlertCount,
                        LastUpdate = update.Timestamp
                    }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send fleet health update for tenant {TenantId}", tenantId);
            }
        }
    }
}

### Infrastructure Layer - IoT Device Communication

```csharp
namespace DriveOps.Diagnostics.Infrastructure.IoT
{
    public interface IOBDCommunicationService
    {
        Task<Result<DeviceConnection>> ConnectAsync(OBDDeviceId deviceId, CancellationToken cancellationToken = default);
        Task<Result> DisconnectAsync(OBDDeviceId deviceId, CancellationToken cancellationToken = default);
        Task<Result<List<SensorReading>>> ReadSensorDataAsync(OBDDeviceId deviceId, List<SensorType> sensors, CancellationToken cancellationToken = default);
        Task<Result> SendCommandAsync(OBDDeviceId deviceId, OBDCommand command, CancellationToken cancellationToken = default);
        Task<Result<DeviceHealthMetrics>> GetDeviceHealthAsync(OBDDeviceId deviceId, CancellationToken cancellationToken = default);
        Task<Result> StartDataStreamingAsync(OBDDeviceId deviceId, StreamingConfiguration config, CancellationToken cancellationToken = default);
        Task<Result> StopDataStreamingAsync(OBDDeviceId deviceId, CancellationToken cancellationToken = default);
    }

    public class WiFiOBDCommunicationService : IOBDCommunicationService
    {
        private readonly ILogger<WiFiOBDCommunicationService> _logger;
        private readonly IOBDDeviceRepository _deviceRepository;
        private readonly ConcurrentDictionary<OBDDeviceId, WiFiDeviceConnection> _activeConnections = new();
        private readonly IConfiguration _configuration;

        public WiFiOBDCommunicationService(
            ILogger<WiFiOBDCommunicationService> logger,
            IOBDDeviceRepository deviceRepository,
            IConfiguration configuration)
        {
            _logger = logger;
            _deviceRepository = deviceRepository;
            _configuration = configuration;
        }

        public async Task<Result<DeviceConnection>> ConnectAsync(OBDDeviceId deviceId, CancellationToken cancellationToken = default)
        {
            try
            {
                var device = await _deviceRepository.GetByIdAsync(deviceId, cancellationToken);
                if (device == null)
                    return Result.Failure<DeviceConnection>("Device not found");

                if (_activeConnections.ContainsKey(deviceId))
                    return Result.Failure<DeviceConnection>("Device already connected");

                var connectionSettings = new WiFiConnectionSettings
                {
                    IPAddress = device.Configuration.WiFiSettings.IPAddress,
                    Port = device.Configuration.WiFiSettings.Port,
                    SecurityKey = device.Configuration.WiFiSettings.SecurityKey,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var wifiConnection = new WiFiDeviceConnection(deviceId, connectionSettings);
                var connectResult = await wifiConnection.ConnectAsync(cancellationToken);

                if (connectResult.IsFailure)
                    return Result.Failure<DeviceConnection>(connectResult.Error);

                _activeConnections.TryAdd(deviceId, wifiConnection);

                // Update device status
                device.RecordHeartbeat(new DeviceHealthMetrics { IsConnected = true, SignalStrength = wifiConnection.SignalStrength });
                await _deviceRepository.UpdateAsync(device, cancellationToken);

                _logger.LogInformation("Successfully connected to OBD device {DeviceId}", deviceId.Value);

                return Result.Success<DeviceConnection>(new DeviceConnection(deviceId, ConnectionType.WiFi, DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to OBD device {DeviceId}", deviceId.Value);
                return Result.Failure<DeviceConnection>($"Connection failed: {ex.Message}");
            }
        }

        public async Task<Result<List<SensorReading>>> ReadSensorDataAsync(OBDDeviceId deviceId, List<SensorType> sensors, CancellationToken cancellationToken = default)
        {
            if (!_activeConnections.TryGetValue(deviceId, out var connection))
                return Result.Failure<List<SensorReading>>("Device not connected");

            try
            {
                var readings = new List<SensorReading>();

                foreach (var sensorType in sensors)
                {
                    var obdCommand = GetOBDCommandForSensor(sensorType);
                    var response = await connection.SendCommandAsync(obdCommand, cancellationToken);

                    if (response.IsSuccess)
                    {
                        var reading = ParseSensorReading(sensorType, response.Value, DateTime.UtcNow);
                        readings.Add(reading);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to read sensor {SensorType} from device {DeviceId}: {Error}", 
                            sensorType, deviceId.Value, response.Error);
                    }
                }

                return Result.Success(readings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read sensor data from device {DeviceId}", deviceId.Value);
                return Result.Failure<List<SensorReading>>($"Sensor reading failed: {ex.Message}");
            }
        }

        public async Task<Result> StartDataStreamingAsync(OBDDeviceId deviceId, StreamingConfiguration config, CancellationToken cancellationToken = default)
        {
            if (!_activeConnections.TryGetValue(deviceId, out var connection))
                return Result.Failure("Device not connected");

            try
            {
                await connection.StartStreamingAsync(config, cancellationToken);
                
                // Start background task for continuous data collection
                _ = Task.Run(async () => await ContinuousDataCollection(deviceId, config, cancellationToken), cancellationToken);

                _logger.LogInformation("Started data streaming for device {DeviceId}", deviceId.Value);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start data streaming for device {DeviceId}", deviceId.Value);
                return Result.Failure($"Streaming failed: {ex.Message}");
            }
        }

        private async Task ContinuousDataCollection(OBDDeviceId deviceId, StreamingConfiguration config, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _activeConnections.ContainsKey(deviceId))
            {
                try
                {
                    var sensorData = await ReadSensorDataAsync(deviceId, config.SensorTypes, cancellationToken);
                    
                    if (sensorData.IsSuccess)
                    {
                        // Send data to processing pipeline
                        await ProcessStreamingData(deviceId, sensorData.Value, cancellationToken);
                    }

                    await Task.Delay(config.SamplingInterval, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in continuous data collection for device {DeviceId}", deviceId.Value);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); // Error backoff
                }
            }
        }

        private async Task ProcessStreamingData(OBDDeviceId deviceId, List<SensorReading> readings, CancellationToken cancellationToken)
        {
            // This would integrate with the command pipeline for real-time processing
            // Implementation would depend on the specific message bus/event system used
        }

        private OBDCommand GetOBDCommandForSensor(SensorType sensorType)
        {
            return sensorType switch
            {
                SensorType.EngineRPM => new OBDCommand("010C", "Engine RPM"),
                SensorType.VehicleSpeed => new OBDCommand("010D", "Vehicle Speed"),
                SensorType.EngineLoad => new OBDCommand("0104", "Engine Load"),
                SensorType.CoolantTemperature => new OBDCommand("0105", "Coolant Temperature"),
                SensorType.IntakeAirTemperature => new OBDCommand("010F", "Intake Air Temperature"),
                SensorType.MAFAirFlowRate => new OBDCommand("0110", "MAF Air Flow Rate"),
                SensorType.ThrottlePosition => new OBDCommand("0111", "Throttle Position"),
                SensorType.FuelPressure => new OBDCommand("010A", "Fuel Pressure"),
                SensorType.ManifoldPressure => new OBDCommand("010B", "Manifold Pressure"),
                _ => throw new ArgumentException($"Unknown sensor type: {sensorType}")
            };
        }

        private SensorReading ParseSensorReading(SensorType sensorType, string rawData, DateTime timestamp)
        {
            // Parse OBD response based on sensor type
            // This is a simplified example - real implementation would handle all OBD protocols
            return sensorType switch
            {
                SensorType.EngineRPM => new SensorReading(sensorType, ParseRPM(rawData), "RPM", SensorDataQuality.Good, timestamp),
                SensorType.VehicleSpeed => new SensorReading(sensorType, ParseSpeed(rawData), "km/h", SensorDataQuality.Good, timestamp),
                SensorType.CoolantTemperature => new SensorReading(sensorType, ParseTemperature(rawData), "°C", SensorDataQuality.Good, timestamp),
                _ => new SensorReading(sensorType, 0, "unknown", SensorDataQuality.Poor, timestamp)
            };
        }

        private decimal ParseRPM(string data)
        {
            // Example parsing for RPM (PID 010C)
            // Real implementation would handle proper OBD response parsing
            if (data.Length >= 4)
            {
                var a = Convert.ToInt32(data.Substring(0, 2), 16);
                var b = Convert.ToInt32(data.Substring(2, 2), 16);
                return (a * 256 + b) / 4;
            }
            return 0;
        }

        private decimal ParseSpeed(string data)
        {
            // Example parsing for Vehicle Speed (PID 010D)
            if (data.Length >= 2)
            {
                return Convert.ToInt32(data.Substring(0, 2), 16);
            }
            return 0;
        }

        private decimal ParseTemperature(string data)
        {
            // Example parsing for Coolant Temperature (PID 0105)
            if (data.Length >= 2)
            {
                return Convert.ToInt32(data.Substring(0, 2), 16) - 40;
            }
            return 0;
        }
    }

    // Supporting classes
    public class WiFiDeviceConnection
    {
        public OBDDeviceId DeviceId { get; }
        public WiFiConnectionSettings Settings { get; }
        public decimal SignalStrength { get; private set; }
        public bool IsConnected { get; private set; }

        private TcpClient? _tcpClient;
        private NetworkStream? _stream;

        public WiFiDeviceConnection(OBDDeviceId deviceId, WiFiConnectionSettings settings)
        {
            DeviceId = deviceId;
            Settings = settings;
        }

        public async Task<Result> ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(Settings.IPAddress, Settings.Port);
                _stream = _tcpClient.GetStream();
                
                // Perform authentication if required
                if (!string.IsNullOrEmpty(Settings.SecurityKey))
                {
                    var authResult = await AuthenticateAsync(Settings.SecurityKey, cancellationToken);
                    if (authResult.IsFailure)
                        return authResult;
                }

                IsConnected = true;
                SignalStrength = 0.85m; // Would be calculated based on actual signal metrics
                
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Connection failed: {ex.Message}");
            }
        }

        public async Task<Result<string>> SendCommandAsync(OBDCommand command, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || _stream == null)
                return Result.Failure<string>("Not connected");

            try
            {
                var commandBytes = Encoding.ASCII.GetBytes(command.Command + "\r");
                await _stream.WriteAsync(commandBytes, 0, commandBytes.Length, cancellationToken);

                var responseBuffer = new byte[1024];
                var bytesRead = await _stream.ReadAsync(responseBuffer, 0, responseBuffer.Length, cancellationToken);
                var response = Encoding.ASCII.GetString(responseBuffer, 0, bytesRead).Trim();

                return Result.Success(response);
            }
            catch (Exception ex)
            {
                return Result.Failure<string>($"Command failed: {ex.Message}");
            }
        }

        public async Task StartStreamingAsync(StreamingConfiguration config, CancellationToken cancellationToken = default)
        {
            // Configure device for continuous streaming
            foreach (var sensorType in config.SensorTypes)
            {
                var command = new OBDCommand($"ATMA{(int)sensorType:X2}", $"Monitor {sensorType}");
                await SendCommandAsync(command, cancellationToken);
            }
        }

        private async Task<Result> AuthenticateAsync(string securityKey, CancellationToken cancellationToken)
        {
            // Implement device authentication protocol
            var authCommand = new OBDCommand($"AUTH{securityKey}", "Authentication");
            var result = await SendCommandAsync(authCommand, cancellationToken);
            
            return result.IsSuccess && result.Value.Contains("OK") 
                ? Result.Success() 
                : Result.Failure("Authentication failed");
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _tcpClient?.Dispose();
            IsConnected = false;
        }
    }

    public class WiFiConnectionSettings
    {
        public string IPAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 35000;
        public string SecurityKey { get; set; } = string.Empty;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    public class StreamingConfiguration
    {
        public List<SensorType> SensorTypes { get; set; } = new();
        public TimeSpan SamplingInterval { get; set; } = TimeSpan.FromMilliseconds(100);
        public int BufferSize { get; set; } = 1000;
        public bool EnableCompression { get; set; } = true;
    }

    public class OBDCommand
    {
        public string Command { get; }
        public string Description { get; }

        public OBDCommand(string command, string description)
        {
            Command = command;
            Description = description;
        }
    }

    public enum SensorType
    {
        EngineRPM = 1,
        VehicleSpeed = 2,
        EngineLoad = 3,
        CoolantTemperature = 4,
        IntakeAirTemperature = 5,
        MAFAirFlowRate = 6,
        ThrottlePosition = 7,
        FuelPressure = 8,
        ManifoldPressure = 9,
        OxygenSensor = 10,
        FuelLevel = 11,
        AmbientAirTemperature = 12
    }

    public enum SensorDataQuality
    {
        Poor = 0,
        Fair = 1,
        Good = 2,
        Excellent = 3
    }
}

### Infrastructure Layer - AI Services

```csharp
namespace DriveOps.Diagnostics.Infrastructure.AI
{
    public interface IAIAnalysisService
    {
        Task<AIAnalysisResult> AnalyzeSensorDataAsync(VehicleId vehicleId, SensorReading reading, CancellationToken cancellationToken = default);
        Task<FaultClassificationResult> ClassifyFaultCodeAsync(string faultCode, VehicleId vehicleId, CancellationToken cancellationToken = default);
        Task<AnomalyDetectionResult> DetectAnomaliesAsync(VehicleId vehicleId, List<SensorReading> readings, CancellationToken cancellationToken = default);
        Task<RepairRecommendationResult> GetRepairRecommendationsAsync(VehicleId vehicleId, List<FaultCode> faultCodes, CancellationToken cancellationToken = default);
        Task<CostEstimationResult> EstimateRepairCostAsync(VehicleId vehicleId, List<RepairAction> repairActions, CancellationToken cancellationToken = default);
    }

    public interface IPredictiveAnalysisEngine
    {
        Task<PredictiveAnalysisResult> AnalyzeAsync(AIModel model, VehicleHistoricalData data, AnalysisType type, CancellationToken cancellationToken = default);
        Task<ComponentFailurePrediction> PredictComponentFailureAsync(VehicleId vehicleId, string componentName, CancellationToken cancellationToken = default);
        Task<MaintenancePredictionResult> PredictMaintenanceNeedsAsync(VehicleId vehicleId, PredictionTimeframe timeframe, CancellationToken cancellationToken = default);
        Task<PerformanceOptimizationResult> AnalyzePerformanceOptimizationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
    }

    public class MLNetAIAnalysisService : IAIAnalysisService
    {
        private readonly ILogger<MLNetAIAnalysisService> _logger;
        private readonly IAIModelRepository _modelRepository;
        private readonly IVehicleProfileRepository _vehicleProfileRepository;
        private readonly MLContext _mlContext;
        private readonly ConcurrentDictionary<string, ITransformer> _loadedModels = new();

        public MLNetAIAnalysisService(
            ILogger<MLNetAIAnalysisService> logger,
            IAIModelRepository modelRepository,
            IVehicleProfileRepository vehicleProfileRepository)
        {
            _logger = logger;
            _modelRepository = modelRepository;
            _vehicleProfileRepository = vehicleProfileRepository;
            _mlContext = new MLContext(seed: 0);
        }

        public async Task<AIAnalysisResult> AnalyzeSensorDataAsync(VehicleId vehicleId, SensorReading reading, CancellationToken cancellationToken = default)
        {
            try
            {
                var vehicleProfile = await _vehicleProfileRepository.GetByVehicleIdAsync(vehicleId, cancellationToken);
                if (vehicleProfile == null)
                    return AIAnalysisResult.Failed("Vehicle profile not found");

                // Get the anomaly detection model
                var model = await GetOrLoadModelAsync(ModelType.AnomalyDetection, cancellationToken);
                if (model == null)
                    return AIAnalysisResult.Failed("Anomaly detection model not available");

                // Prepare input data
                var inputData = new SensorAnalysisInput
                {
                    SensorType = (float)reading.Type,
                    Value = (float)reading.Value,
                    VehicleBrand = vehicleProfile.ManufacturerData.Brand,
                    VehicleModel = vehicleProfile.ManufacturerData.Model,
                    VehicleYear = vehicleProfile.ManufacturerData.Year,
                    Mileage = vehicleProfile.VehicleId.Value // This would come from vehicle data
                };

                // Make prediction
                var predictionEngine = _mlContext.Model.CreatePredictionEngine<SensorAnalysisInput, AnomalyPrediction>(model);
                var prediction = predictionEngine.Predict(inputData);

                var result = new AIAnalysisResult
                {
                    HasFaults = prediction.IsAnomaly,
                    Confidence = prediction.Score,
                    ProcessingTimeMs = 0 // Would be measured
                };

                if (prediction.IsAnomaly)
                {
                    var fault = await ClassifyAnomalyAsFault(reading, prediction, vehicleProfile, cancellationToken);
                    result.DetectedFaults.Add(fault);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze sensor data for vehicle {VehicleId}", vehicleId.Value);
                return AIAnalysisResult.Failed($"Analysis failed: {ex.Message}");
            }
        }

        public async Task<FaultClassificationResult> ClassifyFaultCodeAsync(string faultCode, VehicleId vehicleId, CancellationToken cancellationToken = default)
        {
            try
            {
                var model = await GetOrLoadModelAsync(ModelType.FaultClassification, cancellationToken);
                if (model == null)
                    return FaultClassificationResult.Failed("Fault classification model not available");

                var vehicleProfile = await _vehicleProfileRepository.GetByVehicleIdAsync(vehicleId, cancellationToken);

                var inputData = new FaultClassificationInput
                {
                    FaultCode = faultCode,
                    VehicleBrand = vehicleProfile?.ManufacturerData.Brand ?? "Unknown",
                    VehicleModel = vehicleProfile?.ManufacturerData.Model ?? "Unknown",
                    VehicleYear = vehicleProfile?.ManufacturerData.Year ?? 2000
                };

                var predictionEngine = _mlContext.Model.CreatePredictionEngine<FaultClassificationInput, FaultClassificationPrediction>(model);
                var prediction = predictionEngine.Predict(inputData);

                return new FaultClassificationResult
                {
                    Classification = prediction.PredictedLabel,
                    Confidence = prediction.Score.Max(),
                    Severity = DetermineSeverityFromClassification(prediction.PredictedLabel),
                    Description = GetFaultDescription(faultCode, prediction.PredictedLabel)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to classify fault code {FaultCode} for vehicle {VehicleId}", faultCode, vehicleId.Value);
                return FaultClassificationResult.Failed($"Classification failed: {ex.Message}");
            }
        }

        public async Task<RepairRecommendationResult> GetRepairRecommendationsAsync(VehicleId vehicleId, List<FaultCode> faultCodes, CancellationToken cancellationToken = default)
        {
            try
            {
                var model = await GetOrLoadModelAsync(ModelType.RepairRecommendation, cancellationToken);
                if (model == null)
                    return RepairRecommendationResult.Failed("Repair recommendation model not available");

                var vehicleProfile = await _vehicleProfileRepository.GetByVehicleIdAsync(vehicleId, cancellationToken);
                var recommendations = new List<AIRepairRecommendation>();

                foreach (var faultCode in faultCodes)
                {
                    var inputData = new RepairRecommendationInput
                    {
                        FaultCode = faultCode.Code,
                        Severity = (float)faultCode.Severity,
                        VehicleBrand = vehicleProfile?.ManufacturerData.Brand ?? "Unknown",
                        VehicleModel = vehicleProfile?.ManufacturerData.Model ?? "Unknown",
                        VehicleYear = vehicleProfile?.ManufacturerData.Year ?? 2000
                    };

                    var predictionEngine = _mlContext.Model.CreatePredictionEngine<RepairRecommendationInput, RepairRecommendationPrediction>(model);
                    var prediction = predictionEngine.Predict(inputData);

                    var recommendation = new AIRepairRecommendation
                    {
                        FaultCode = faultCode.Code,
                        RecommendedAction = prediction.RecommendedAction,
                        Priority = MapPriorityFromScore(prediction.PriorityScore),
                        EstimatedCost = prediction.EstimatedCost,
                        EstimatedDuration = TimeSpan.FromHours(prediction.EstimatedHours),
                        Confidence = prediction.ConfidenceScore,
                        RequiredParts = prediction.RequiredParts?.Split(',').ToList() ?? new List<string>()
                    };

                    recommendations.Add(recommendation);
                }

                return new RepairRecommendationResult
                {
                    Recommendations = recommendations,
                    TotalEstimatedCost = recommendations.Sum(r => r.EstimatedCost),
                    HighestPriority = recommendations.Max(r => r.Priority)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get repair recommendations for vehicle {VehicleId}", vehicleId.Value);
                return RepairRecommendationResult.Failed($"Recommendation failed: {ex.Message}");
            }
        }

        private async Task<ITransformer?> GetOrLoadModelAsync(ModelType modelType, CancellationToken cancellationToken)
        {
            var modelKey = modelType.ToString();
            
            if (_loadedModels.TryGetValue(modelKey, out var cachedModel))
                return cachedModel;

            var aiModel = await _modelRepository.GetLatestModelAsync(modelType, cancellationToken);
            if (aiModel == null)
                return null;

            try
            {
                var modelPath = GetModelFilePath(aiModel);
                var loadedModel = _mlContext.Model.Load(modelPath, out _);
                _loadedModels.TryAdd(modelKey, loadedModel);
                return loadedModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load AI model {ModelType}", modelType);
                return null;
            }
        }

        private async Task<DetectedFault> ClassifyAnomalyAsFault(SensorReading reading, AnomalyPrediction prediction, VehicleProfile vehicleProfile, CancellationToken cancellationToken)
        {
            // This would use additional logic to convert anomalies to specific fault codes
            return new DetectedFault
            {
                Code = GenerateFaultCodeFromAnomaly(reading.Type, prediction),
                Severity = DetermineSeverityFromScore(prediction.Score),
                Description = $"Anomaly detected in {reading.Type}: {reading.Value} {reading.Unit}",
                DetectedAt = reading.Timestamp
            };
        }

        private string GenerateFaultCodeFromAnomaly(SensorType sensorType, AnomalyPrediction prediction)
        {
            // Map sensor anomalies to standard OBD fault codes
            return sensorType switch
            {
                SensorType.EngineRPM => "P0300", // Random/Multiple Cylinder Misfire Detected
                SensorType.CoolantTemperature => "P0217", // Engine Over Temperature Condition
                SensorType.MAFAirFlowRate => "P0100", // Mass or Volume Air Flow Circuit Malfunction
                SensorType.ThrottlePosition => "P0120", // Throttle/Pedal Position Sensor/Switch A Circuit Malfunction
                _ => "P0000" // Generic fault
            };
        }

        private FaultSeverity DetermineSeverityFromScore(float score)
        {
            return score switch
            {
                >= 0.9f => FaultSeverity.Critical,
                >= 0.7f => FaultSeverity.Error,
                >= 0.5f => FaultSeverity.Warning,
                _ => FaultSeverity.Info
            };
        }

        private FaultSeverity DetermineSeverityFromClassification(string classification)
        {
            return classification.ToLower() switch
            {
                "critical" => FaultSeverity.Critical,
                "error" => FaultSeverity.Error,
                "warning" => FaultSeverity.Warning,
                _ => FaultSeverity.Info
            };
        }

        private RepairPriority MapPriorityFromScore(float score)
        {
            return score switch
            {
                >= 0.8f => RepairPriority.Urgent,
                >= 0.6f => RepairPriority.High,
                >= 0.4f => RepairPriority.Medium,
                _ => RepairPriority.Low
            };
        }

        private string GetFaultDescription(string faultCode, string classification)
        {
            // This would integrate with a comprehensive fault code database
            return $"Fault {faultCode} classified as {classification}";
        }

        private string GetModelFilePath(AIModel aiModel)
        {
            // Return path to the model file based on model configuration
            return $"/models/{aiModel.Type}/{aiModel.Version}/model.zip";
        }
    }

    // ML.NET model input/output classes
    public class SensorAnalysisInput
    {
        public float SensorType { get; set; }
        public float Value { get; set; }
        public string VehicleBrand { get; set; } = string.Empty;
        public string VehicleModel { get; set; } = string.Empty;
        public float VehicleYear { get; set; }
        public float Mileage { get; set; }
    }

    public class AnomalyPrediction
    {
        public bool IsAnomaly { get; set; }
        public float Score { get; set; }
    }

    public class FaultClassificationInput
    {
        public string FaultCode { get; set; } = string.Empty;
        public string VehicleBrand { get; set; } = string.Empty;
        public string VehicleModel { get; set; } = string.Empty;
        public float VehicleYear { get; set; }
    }

    public class FaultClassificationPrediction
    {
        public string PredictedLabel { get; set; } = string.Empty;
        public float[] Score { get; set; } = Array.Empty<float>();
    }

    public class RepairRecommendationInput
    {
        public string FaultCode { get; set; } = string.Empty;
        public float Severity { get; set; }
        public string VehicleBrand { get; set; } = string.Empty;
        public string VehicleModel { get; set; } = string.Empty;
        public float VehicleYear { get; set; }
    }

    public class RepairRecommendationPrediction
    {
        public string RecommendedAction { get; set; } = string.Empty;
        public float PriorityScore { get; set; }
        public float EstimatedCost { get; set; }
        public float EstimatedHours { get; set; }
        public float ConfidenceScore { get; set; }
        public string? RequiredParts { get; set; }
    }

    // Result classes
    public class AIAnalysisResult
    {
        public bool HasFaults { get; set; }
        public decimal Confidence { get; set; }
        public long ProcessingTimeMs { get; set; }
        public List<DetectedFault> DetectedFaults { get; set; } = new();

        public static AIAnalysisResult Failed(string error) => new() { HasFaults = false, Confidence = 0 };
    }

    public class DetectedFault
    {
        public string Code { get; set; } = string.Empty;
        public FaultSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
    }

    public class FaultClassificationResult
    {
        public string Classification { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public FaultSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;

        public static FaultClassificationResult Failed(string error) => new() { Classification = "Unknown", Confidence = 0 };
    }

    public class RepairRecommendationResult
    {
        public List<AIRepairRecommendation> Recommendations { get; set; } = new();
        public decimal TotalEstimatedCost { get; set; }
        public RepairPriority HighestPriority { get; set; }

        public static RepairRecommendationResult Failed(string error) => new() { Recommendations = new() };
    }

    public class AIRepairRecommendation
    {
        public string FaultCode { get; set; } = string.Empty;
        public string RecommendedAction { get; set; } = string.Empty;
        public RepairPriority Priority { get; set; }
        public decimal EstimatedCost { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
        public decimal Confidence { get; set; }
        public List<string> RequiredParts { get; set; } = new();
    }
}
```

---

## Database Schema (PostgreSQL)

### Core Diagnostic Tables

```sql
-- Create diagnostics schema
CREATE SCHEMA IF NOT EXISTS diagnostics;

-- OBD devices table
CREATE TABLE diagnostics.obd_devices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id),
    serial_number VARCHAR(100) NOT NULL UNIQUE,
    model INTEGER NOT NULL, -- 1: Basic, 2: Pro, 3: Enterprise
    firmware_version VARCHAR(50) NOT NULL,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Inactive, 1: Available, 2: InUse, 3: Maintenance, 4: Error
    assigned_technician_id UUID REFERENCES users.users(id),
    last_heartbeat TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    activated_at TIMESTAMP WITH TIME ZONE,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Device configurations table
CREATE TABLE diagnostics.device_configurations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id UUID NOT NULL REFERENCES diagnostics.obd_devices(id) ON DELETE CASCADE,
    wifi_ssid VARCHAR(100),
    wifi_ip_address INET,
    wifi_port INTEGER DEFAULT 35000,
    security_key VARCHAR(255),
    supported_protocols JSONB NOT NULL DEFAULT '[]', -- ["CAN", "K-Line", "J1850"]
    sampling_rate_hz INTEGER DEFAULT 10,
    buffer_size INTEGER DEFAULT 1000,
    enable_compression BOOLEAN DEFAULT true,
    custom_settings JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Device assignments table
CREATE TABLE diagnostics.device_assignments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id UUID NOT NULL REFERENCES diagnostics.obd_devices(id),
    technician_id UUID NOT NULL REFERENCES users.users(id),
    assigned_by UUID NOT NULL REFERENCES users.users(id),
    assigned_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    unassigned_at TIMESTAMP WITH TIME ZONE,
    assignment_reason TEXT,
    is_active BOOLEAN NOT NULL DEFAULT true
);

-- Device status history table
CREATE TABLE diagnostics.device_status_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    device_id UUID NOT NULL REFERENCES diagnostics.obd_devices(id),
    status INTEGER NOT NULL,
    battery_level DECIMAL(5,2),
    signal_strength DECIMAL(5,2),
    temperature DECIMAL(5,2),
    connection_type INTEGER, -- 1: WiFi, 2: Bluetooth
    error_message TEXT,
    health_metrics JSONB NOT NULL DEFAULT '{}',
    recorded_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Vehicle profiles table
CREATE TABLE diagnostics.vehicle_profiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    vehicle_id UUID NOT NULL REFERENCES vehicles.vehicles(id) ON DELETE CASCADE,
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id),
    manufacturer_name VARCHAR(100) NOT NULL,
    brand VARCHAR(100) NOT NULL,
    model VARCHAR(100) NOT NULL,
    year INTEGER NOT NULL,
    engine_code VARCHAR(50),
    transmission_type VARCHAR(50),
    supported_protocols JSONB NOT NULL DEFAULT '[]',
    specific_parameters JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Manufacturer data table
CREATE TABLE diagnostics.manufacturer_data (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    manufacturer_name VARCHAR(100) NOT NULL,
    brand VARCHAR(100) NOT NULL,
    model VARCHAR(100) NOT NULL,
    year_from INTEGER NOT NULL,
    year_to INTEGER,
    engine_specifications JSONB NOT NULL DEFAULT '{}',
    transmission_specifications JSONB NOT NULL DEFAULT '{}',
    electrical_specifications JSONB NOT NULL DEFAULT '{}',
    emission_specifications JSONB NOT NULL DEFAULT '{}',
    supported_protocols JSONB NOT NULL DEFAULT '[]',
    diagnostic_parameters JSONB NOT NULL DEFAULT '{}',
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Technical specifications table
CREATE TABLE diagnostics.technical_specifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    vehicle_profile_id UUID NOT NULL REFERENCES diagnostics.vehicle_profiles(id) ON DELETE CASCADE,
    specification_type VARCHAR(50) NOT NULL, -- 'engine', 'transmission', 'electrical', 'emission'
    specification_data JSONB NOT NULL DEFAULT '{}',
    sensors JSONB NOT NULL DEFAULT '[]',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Diagnostic sessions table
CREATE TABLE diagnostics.diagnostic_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id),
    vehicle_id UUID NOT NULL REFERENCES vehicles.vehicles(id),
    device_id UUID NOT NULL REFERENCES diagnostics.obd_devices(id),
    technician_id UUID NOT NULL REFERENCES users.users(id),
    status INTEGER NOT NULL DEFAULT 0, -- 0: Initiated, 1: InProgress, 2: Completed, 3: Cancelled, 4: Error
    started_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMP WITH TIME ZONE,
    duration_seconds INTEGER,
    outcome INTEGER, -- 0: NoIssues, 1: IssuesFound, 2: RepairRequired, 3: FurtherDiagnosisNeeded
    session_notes TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Fault codes table
CREATE TABLE diagnostics.fault_codes (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES diagnostics.diagnostic_sessions(id) ON DELETE CASCADE,
    fault_code VARCHAR(10) NOT NULL,
    description TEXT NOT NULL,
    severity INTEGER NOT NULL, -- 0: Info, 1: Warning, 2: Error, 3: Critical
    is_active BOOLEAN NOT NULL DEFAULT true,
    manufacturer_specific BOOLEAN NOT NULL DEFAULT false,
    detected_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    cleared_at TIMESTAMP WITH TIME ZONE,
    occurrence_count INTEGER NOT NULL DEFAULT 1
);

-- Sensor readings table (time-series optimized)
CREATE TABLE diagnostics.sensor_readings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES diagnostics.diagnostic_sessions(id) ON DELETE CASCADE,
    sensor_type INTEGER NOT NULL, -- 1: EngineRPM, 2: VehicleSpeed, etc.
    sensor_name VARCHAR(100) NOT NULL,
    value DECIMAL(10,3) NOT NULL,
    unit VARCHAR(20) NOT NULL,
    quality INTEGER NOT NULL DEFAULT 2, -- 0: Poor, 1: Fair, 2: Good, 3: Excellent
    timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    -- Partitioning by timestamp for time-series optimization
    CONSTRAINT sensor_readings_timestamp_check CHECK (timestamp >= '2024-01-01' AND timestamp < '2030-01-01')
) PARTITION BY RANGE (timestamp);

-- Create monthly partitions for sensor readings
CREATE TABLE diagnostics.sensor_readings_2024_01 PARTITION OF diagnostics.sensor_readings
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');
CREATE TABLE diagnostics.sensor_readings_2024_02 PARTITION OF diagnostics.sensor_readings
    FOR VALUES FROM ('2024-02-01') TO ('2024-03-01');
-- Continue for all months...

-- Diagnostic results table
CREATE TABLE diagnostics.diagnostic_results (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id UUID NOT NULL REFERENCES diagnostics.diagnostic_sessions(id) ON DELETE CASCADE,
    diagnosis_description TEXT NOT NULL,
    confidence INTEGER NOT NULL, -- 0: Low, 1: Medium, 2: High, 3: Confirmed
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    reviewed_by UUID REFERENCES users.users(id),
    reviewed_at TIMESTAMP WITH TIME ZONE
);

-- Repair recommendations table
CREATE TABLE diagnostics.repair_recommendations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    diagnostic_result_id UUID NOT NULL REFERENCES diagnostics.diagnostic_results(id) ON DELETE CASCADE,
    fault_code_id UUID REFERENCES diagnostics.fault_codes(id),
    recommended_action TEXT NOT NULL,
    priority INTEGER NOT NULL, -- 0: Low, 1: Medium, 2: High, 3: Urgent
    estimated_cost DECIMAL(10,2),
    estimated_duration_hours DECIMAL(5,2),
    required_parts JSONB NOT NULL DEFAULT '[]',
    special_tools TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- AI models table
CREATE TABLE diagnostics.ai_models (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    model_type INTEGER NOT NULL, -- 1: FaultClassification, 2: AnomalyDetection, etc.
    version VARCHAR(50) NOT NULL,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Training, 1: Active, 2: Deprecated, 3: Error
    accuracy DECIMAL(5,4),
    training_data_size INTEGER,
    model_file_path VARCHAR(500),
    configuration_json JSONB NOT NULL DEFAULT '{}',
    metrics JSONB NOT NULL DEFAULT '{}',
    trained_at TIMESTAMP WITH TIME ZONE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    UNIQUE(model_type, version)
);

-- Training datasets table
CREATE TABLE diagnostics.training_datasets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    model_id UUID NOT NULL REFERENCES diagnostics.ai_models(id),
    dataset_name VARCHAR(255) NOT NULL,
    dataset_type VARCHAR(50) NOT NULL, -- 'training', 'validation', 'test'
    data_source VARCHAR(100), -- 'vehicle_data', 'manufacturer', 'manual'
    data_size INTEGER NOT NULL,
    data_file_path VARCHAR(500),
    feature_columns JSONB NOT NULL DEFAULT '[]',
    target_column VARCHAR(100),
    preprocessing_steps JSONB NOT NULL DEFAULT '[]',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Prediction results table
CREATE TABLE diagnostics.prediction_results (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    model_id UUID NOT NULL REFERENCES diagnostics.ai_models(id),
    session_id UUID REFERENCES diagnostics.diagnostic_sessions(id),
    vehicle_id UUID NOT NULL REFERENCES vehicles.vehicles(id),
    input_data JSONB NOT NULL,
    prediction_output JSONB NOT NULL,
    confidence_score DECIMAL(5,4),
    processing_time_ms INTEGER,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Predictive analyses table
CREATE TABLE diagnostics.predictive_analyses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id),
    vehicle_id UUID NOT NULL REFERENCES vehicles.vehicles(id),
    analysis_type INTEGER NOT NULL, -- 1: ComponentHealth, 2: MaintenanceScheduling, etc.
    timeframe INTEGER NOT NULL, -- 1: OneMonth, 2: ThreeMonths, 3: SixMonths, 4: OneYear
    analyzed_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMP WITH TIME ZONE,
    overall_health_score DECIMAL(5,2),
    risk_level INTEGER, -- 0: Low, 1: Medium, 2: High, 3: Critical
    analysis_summary TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Component predictions table
CREATE TABLE diagnostics.component_predictions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    predictive_analysis_id UUID NOT NULL REFERENCES diagnostics.predictive_analyses(id) ON DELETE CASCADE,
    component_name VARCHAR(100) NOT NULL,
    failure_probability INTEGER NOT NULL, -- 0: VeryLow, 1: Low, 2: Medium, 3: High, 4: VeryHigh, 5: Imminent
    estimated_failure_date DATE,
    replacement_cost DECIMAL(10,2),
    warning_message TEXT,
    recommendation_text TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Maintenance recommendations table
CREATE TABLE diagnostics.maintenance_recommendations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    predictive_analysis_id UUID NOT NULL REFERENCES diagnostics.predictive_analyses(id) ON DELETE CASCADE,
    maintenance_type INTEGER NOT NULL, -- 1: Preventive, 2: Predictive, 3: Corrective, 4: Emergency
    recommended_date DATE NOT NULL,
    description TEXT NOT NULL,
    estimated_cost DECIMAL(10,2),
    priority INTEGER NOT NULL, -- 0: Low, 1: Medium, 2: High, 3: Urgent
    required_parts JSONB NOT NULL DEFAULT '[]',
    estimated_duration_hours DECIMAL(5,2),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Knowledge base entries table
CREATE TABLE diagnostics.knowledge_base_entries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title VARCHAR(500) NOT NULL,
    content TEXT NOT NULL,
    entry_type INTEGER NOT NULL, -- 1: RepairProcedure, 2: TroubleshootingGuide, 3: TechnicalBulletin
    tags JSONB NOT NULL DEFAULT '[]',
    vehicle_brands JSONB NOT NULL DEFAULT '[]', -- Applicable vehicle brands
    fault_codes JSONB NOT NULL DEFAULT '[]', -- Related fault codes
    relevance_score DECIMAL(5,4) DEFAULT 1.0,
    view_count INTEGER DEFAULT 0,
    helpful_votes INTEGER DEFAULT 0,
    author_id UUID REFERENCES users.users(id),
    reviewed_by UUID REFERENCES users.users(id),
    reviewed_at TIMESTAMP WITH TIME ZONE,
    is_published BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Service records table
CREATE TABLE diagnostics.service_records (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    vehicle_profile_id UUID NOT NULL REFERENCES diagnostics.vehicle_profiles(id),
    service_date DATE NOT NULL,
    service_type INTEGER NOT NULL, -- 1: Maintenance, 2: Repair, 3: Inspection, 4: Recall
    description TEXT NOT NULL,
    mileage_at_service INTEGER,
    cost DECIMAL(10,2),
    service_provider VARCHAR(255),
    parts_replaced JSONB NOT NULL DEFAULT '[]',
    work_performed JSONB NOT NULL DEFAULT '[]',
    warranty_until DATE,
    invoice_file_id UUID REFERENCES files.files(id),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Fleet monitoring table
CREATE TABLE diagnostics.fleet_monitoring (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id),
    vehicle_id UUID NOT NULL REFERENCES vehicles.vehicles(id),
    health_score DECIMAL(5,2) NOT NULL,
    alert_count INTEGER NOT NULL DEFAULT 0,
    last_diagnostic_session_id UUID REFERENCES diagnostics.diagnostic_sessions(id),
    last_analysis_at TIMESTAMP WITH TIME ZONE,
    next_maintenance_due DATE,
    status INTEGER NOT NULL DEFAULT 0, -- 0: Healthy, 1: Warning, 2: Attention, 3: Critical
    status_message TEXT,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Vehicle groups (for fleet organization)
CREATE TABLE diagnostics.vehicle_groups (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    group_type VARCHAR(50), -- 'fleet', 'department', 'location', 'custom'
    created_by UUID NOT NULL REFERENCES users.users(id),
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Vehicle group memberships table
CREATE TABLE diagnostics.vehicle_group_memberships (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    vehicle_group_id UUID NOT NULL REFERENCES diagnostics.vehicle_groups(id) ON DELETE CASCADE,
    vehicle_id UUID NOT NULL REFERENCES vehicles.vehicles(id) ON DELETE CASCADE,
    added_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    added_by UUID NOT NULL REFERENCES users.users(id),
    
    UNIQUE(vehicle_group_id, vehicle_id)
);

-- Fleet analytics table
CREATE TABLE diagnostics.fleet_analytics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id),
    vehicle_group_id UUID REFERENCES diagnostics.vehicle_groups(id),
    analysis_date DATE NOT NULL,
    total_vehicles INTEGER NOT NULL,
    healthy_vehicles INTEGER NOT NULL,
    vehicles_with_warnings INTEGER NOT NULL,
    vehicles_needing_attention INTEGER NOT NULL,
    critical_vehicles INTEGER NOT NULL,
    average_health_score DECIMAL(5,2),
    total_maintenance_cost DECIMAL(12,2),
    predicted_costs_next_month DECIMAL(12,2),
    uptime_percentage DECIMAL(5,2),
    analytics_data JSONB NOT NULL DEFAULT '{}',
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Performance metrics table
CREATE TABLE diagnostics.performance_metrics (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id),
    vehicle_id UUID REFERENCES vehicles.vehicles(id),
    metric_type VARCHAR(50) NOT NULL, -- 'fuel_efficiency', 'performance', 'emissions'
    metric_name VARCHAR(100) NOT NULL,
    value DECIMAL(10,3) NOT NULL,
    unit VARCHAR(20) NOT NULL,
    benchmark_value DECIMAL(10,3),
    performance_score DECIMAL(5,2), -- How well vs benchmark
    measurement_date DATE NOT NULL,
    data_source VARCHAR(50), -- 'diagnostic_session', 'manual_input', 'telematics'
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- Indexes for performance optimization
CREATE INDEX idx_obd_devices_tenant_status ON diagnostics.obd_devices(tenant_id, status);
CREATE INDEX idx_obd_devices_technician ON diagnostics.obd_devices(assigned_technician_id) WHERE assigned_technician_id IS NOT NULL;
CREATE INDEX idx_device_status_history_device_time ON diagnostics.device_status_history(device_id, recorded_at DESC);

CREATE INDEX idx_diagnostic_sessions_tenant_status ON diagnostics.diagnostic_sessions(tenant_id, status);
CREATE INDEX idx_diagnostic_sessions_vehicle ON diagnostics.diagnostic_sessions(vehicle_id, started_at DESC);
CREATE INDEX idx_diagnostic_sessions_technician ON diagnostics.diagnostic_sessions(technician_id, started_at DESC);
CREATE INDEX idx_diagnostic_sessions_device ON diagnostics.diagnostic_sessions(device_id, started_at DESC);

CREATE INDEX idx_fault_codes_session ON diagnostics.fault_codes(session_id);
CREATE INDEX idx_fault_codes_code_severity ON diagnostics.fault_codes(fault_code, severity);

CREATE INDEX idx_sensor_readings_session_type ON diagnostics.sensor_readings(session_id, sensor_type);
CREATE INDEX idx_sensor_readings_timestamp ON diagnostics.sensor_readings(timestamp DESC);

CREATE INDEX idx_predictive_analyses_tenant_vehicle ON diagnostics.predictive_analyses(tenant_id, vehicle_id);
CREATE INDEX idx_predictive_analyses_type_date ON diagnostics.predictive_analyses(analysis_type, analyzed_at DESC);

CREATE INDEX idx_knowledge_base_type_published ON diagnostics.knowledge_base_entries(entry_type, is_published);
CREATE INDEX idx_knowledge_base_tags ON diagnostics.knowledge_base_entries USING GIN(tags);
CREATE INDEX idx_knowledge_base_fault_codes ON diagnostics.knowledge_base_entries USING GIN(fault_codes);

CREATE INDEX idx_fleet_monitoring_tenant_status ON diagnostics.fleet_monitoring(tenant_id, status);
CREATE INDEX idx_fleet_monitoring_vehicle ON diagnostics.fleet_monitoring(vehicle_id);

CREATE INDEX idx_vehicle_profiles_vehicle ON diagnostics.vehicle_profiles(vehicle_id);
CREATE INDEX idx_vehicle_profiles_tenant ON diagnostics.vehicle_profiles(tenant_id);

CREATE INDEX idx_service_records_profile_date ON diagnostics.service_records(vehicle_profile_id, service_date DESC);

-- Constraints
ALTER TABLE diagnostics.obd_devices ADD CONSTRAINT chk_device_model CHECK (model BETWEEN 1 AND 3);
ALTER TABLE diagnostics.obd_devices ADD CONSTRAINT chk_device_status CHECK (status BETWEEN 0 AND 4);

ALTER TABLE diagnostics.diagnostic_sessions ADD CONSTRAINT chk_session_status CHECK (status BETWEEN 0 AND 4);
ALTER TABLE diagnostics.diagnostic_sessions ADD CONSTRAINT chk_session_outcome CHECK (outcome IS NULL OR outcome BETWEEN 0 AND 3);

ALTER TABLE diagnostics.fault_codes ADD CONSTRAINT chk_fault_severity CHECK (severity BETWEEN 0 AND 3);

ALTER TABLE diagnostics.sensor_readings ADD CONSTRAINT chk_sensor_quality CHECK (quality BETWEEN 0 AND 3);

ALTER TABLE diagnostics.diagnostic_results ADD CONSTRAINT chk_diagnosis_confidence CHECK (confidence BETWEEN 0 AND 3);

ALTER TABLE diagnostics.repair_recommendations ADD CONSTRAINT chk_repair_priority CHECK (priority BETWEEN 0 AND 3);

ALTER TABLE diagnostics.ai_models ADD CONSTRAINT chk_model_type CHECK (model_type BETWEEN 1 AND 5);
ALTER TABLE diagnostics.ai_models ADD CONSTRAINT chk_model_status CHECK (status BETWEEN 0 AND 3);

ALTER TABLE diagnostics.predictive_analyses ADD CONSTRAINT chk_analysis_type CHECK (analysis_type BETWEEN 1 AND 4);
ALTER TABLE diagnostics.predictive_analyses ADD CONSTRAINT chk_timeframe CHECK (timeframe BETWEEN 1 AND 4);
ALTER TABLE diagnostics.predictive_analyses ADD CONSTRAINT chk_risk_level CHECK (risk_level BETWEEN 0 AND 3);

ALTER TABLE diagnostics.component_predictions ADD CONSTRAINT chk_failure_probability CHECK (failure_probability BETWEEN 0 AND 5);

ALTER TABLE diagnostics.maintenance_recommendations ADD CONSTRAINT chk_maintenance_type CHECK (maintenance_type BETWEEN 1 AND 4);
ALTER TABLE diagnostics.maintenance_recommendations ADD CONSTRAINT chk_maintenance_priority CHECK (priority BETWEEN 0 AND 3);

ALTER TABLE diagnostics.knowledge_base_entries ADD CONSTRAINT chk_entry_type CHECK (entry_type BETWEEN 1 AND 3);

ALTER TABLE diagnostics.service_records ADD CONSTRAINT chk_service_type CHECK (service_type BETWEEN 1 AND 4);

ALTER TABLE diagnostics.fleet_monitoring ADD CONSTRAINT chk_fleet_status CHECK (status BETWEEN 0 AND 3);
ALTER TABLE diagnostics.fleet_monitoring ADD CONSTRAINT chk_health_score CHECK (health_score BETWEEN 0 AND 100);

-- Functions for automatic timestamping
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Triggers for automatic timestamping
CREATE TRIGGER update_obd_devices_updated_at BEFORE UPDATE ON diagnostics.obd_devices FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_device_configurations_updated_at BEFORE UPDATE ON diagnostics.device_configurations FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_vehicle_profiles_updated_at BEFORE UPDATE ON diagnostics.vehicle_profiles FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_manufacturer_data_updated_at BEFORE UPDATE ON diagnostics.manufacturer_data FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_diagnostic_sessions_updated_at BEFORE UPDATE ON diagnostics.diagnostic_sessions FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_knowledge_base_entries_updated_at BEFORE UPDATE ON diagnostics.knowledge_base_entries FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_service_records_updated_at BEFORE UPDATE ON diagnostics.service_records FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_vehicle_groups_updated_at BEFORE UPDATE ON diagnostics.vehicle_groups FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
CREATE TRIGGER update_fleet_monitoring_updated_at BEFORE UPDATE ON diagnostics.fleet_monitoring FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
```
```
