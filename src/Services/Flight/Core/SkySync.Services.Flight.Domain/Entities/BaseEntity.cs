namespace SkySync.Services.Flight.Domain.Entities;

public class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime ModifiedTime { get; set; }
    public bool IsDeleted { get; set; }
}