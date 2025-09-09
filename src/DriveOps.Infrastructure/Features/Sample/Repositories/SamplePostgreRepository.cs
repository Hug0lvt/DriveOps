using Microsoft.EntityFrameworkCore;
using DriveOps.Infrastructure.Data.PostgreSQL;
using SampleModel = DriveOps.Shared.Models.Sample.Sample;
using DriveOps.Shared.Models.Sample;

namespace DriveOps.Infrastructure.Features.Sample.Repositories;

public class SamplePostgreRepository
{
    private readonly PostgreSqlContext _context;

    public SamplePostgreRepository(PostgreSqlContext context)
    {
        _context = context;
    }

    public async Task<SampleModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Samples
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, cancellationToken);
    }

    public async Task<IEnumerable<SampleModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Samples
            .Where(s => !s.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<SampleModel> AddAsync(SampleModel entity, CancellationToken cancellationToken = default)
    {
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        
        _context.Samples.Add(entity);
        return entity;
    }

    public async Task UpdateAsync(SampleModel entity, CancellationToken cancellationToken = default)
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

    public async Task<IEnumerable<SampleModel>> GetByStatusAsync(SampleStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Samples
            .Where(s => s.Status == status && !s.IsDeleted)
            .ToListAsync(cancellationToken);
    }
}