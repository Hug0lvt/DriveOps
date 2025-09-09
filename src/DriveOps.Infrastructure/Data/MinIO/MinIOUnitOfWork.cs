using DriveOps.Infrastructure.Features.Files.Repositories;

namespace DriveOps.Infrastructure.Data.MinIO;

public class MinIOUnitOfWork
{
    private readonly MinIOContext _context;
    private FileMinIORepository? _fileRepository;

    public MinIOUnitOfWork(MinIOContext context)
    {
        _context = context;
    }

    public FileMinIORepository FileRepository =>
        _fileRepository ??= new FileMinIORepository(_context);

    // MinIO n'a pas besoin de SaveChanges comme les bases de données
    // Les opérations sont directement commitées
}