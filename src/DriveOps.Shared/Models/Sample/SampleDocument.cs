namespace DriveOps.Shared.Models.Sample;

public class SampleDocument
{
    public Guid Id { get; set; }
    public Guid SampleId { get; set; }
    public int Version { get; set; }
    public object Data { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool IsActive { get; set; } = true;
}