using ECommerceCsvImport.Core.Models;

namespace ECommerceCsvImport.Core.Services
{
    public interface IProductImportService
    {
        Task<ImportResult> ImportFromCsvAsync(string filePath);
        Task<ImportResult> ImportFromLinesAsync(IEnumerable<string> lines);
    }
}
