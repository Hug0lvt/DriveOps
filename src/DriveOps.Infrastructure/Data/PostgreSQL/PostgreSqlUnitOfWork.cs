using DriveOps.Infrastructure.Data.PostgreSQL.Features.Sample;

namespace DriveOps.Infrastructure.Data.PostgreSQL;

public class PostgreSqlUnitOfWork
{
    private readonly PostgreSqlContext _context;
    private SampleRepository? _sampleRepository;

    public PostgreSqlUnitOfWork(PostgreSqlContext context)
    {
        _context = context;
    }

    public SampleRepository SampleRepository => 
        _sampleRepository ??= new SampleRepository(_context);

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