using Microsoft.EntityFrameworkCore;
using DriveOps.Shared.Models.Sample;
using DriveOps.Infrastructure.Data;

namespace DriveOps.Infrastructure.Features.Sample.Repositories;

public class SampleRepository
{
    private readonly PostgreSqlContext _context;

    public SampleRepository(PostgreSqlContext context)
    {
        _context = context;
    }

    public async Task<DriveOps.Shared.Models.Sample.Sample?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Samples
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
    }

    public async Task<IEnumerable<DriveOps.Shared.Models.Sample.Sample>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Samples
            .Where(s => !s.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<DriveOps.Shared.Models.Sample.Sample> AddAsync(DriveOps.Shared.Models.Sample.Sample entity, CancellationToken cancellationToken = default)
    {
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        
        _context.Samples.Add(entity);
        return entity;
    }

    public async Task UpdateAsync(DriveOps.Shared.Models.Sample.Sample entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _context.Samples.Update(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await UpdateAsync(entity, cancellationToken);
        }
    }

    public async Task<IEnumerable<DriveOps.Shared.Models.Sample.Sample>> GetByStatusAsync(SampleStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Samples
            .Where(s => s.Status == status && !s.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<DriveOps.Shared.Models.Sample.Sample?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Samples
            .FirstOrDefaultAsync(s => s.Code == code && !s.IsDeleted, cancellationToken);
    }
}