using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using DriveOps.Shared.Models.Sample;
using DriveOps.Infrastructure.Data.PostgreSQL.Features.Sample;

namespace DriveOps.Infrastructure.Data.PostgreSQL;

public class PostgreSqlContext : DbContext
{
    private IDbContextTransaction? _currentTransaction;

    public PostgreSqlContext(DbContextOptions<PostgreSqlContext> options) : base(options)
    {
    }

    public DbSet<Sample> Samples { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Appliquer les configurations
        modelBuilder.ApplyConfiguration(new SampleConfiguration());
    }

    // Unit of Work pattern methods
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            await _currentTransaction?.CommitAsync(cancellationToken)!;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _currentTransaction?.RollbackAsync(cancellationToken)!;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public override void Dispose()
    {
        _currentTransaction?.Dispose();
        base.Dispose();
    }
}