namespace TroveKeep.Core.Interfaces.Services;

public interface IBackupService
{
    Task<(byte[] Data, string FileName)> ExportAsync();
    Task ImportAsync(Stream stream);
}
