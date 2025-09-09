using DriveOps.Infrastructure.Features.Sample.Repositories;

namespace DriveOps.Infrastructure.Data.PostgreSQL;

public class PostgreSqlUnitOfWork
{
    private readonly PostgreSqlContext _context;
    private SamplePostgreRepository? _sampleRepository;

    public PostgreSqlUnitOfWork(PostgreSqlContext context)
    {
        _context = context;
    }

    public SamplePostgreRepository SampleRepository => 
        _sampleRepository ??= new SamplePostgreRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.CommitTransactionAsync(cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _context.RollbackTransactionAsync(cancellationToken);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}