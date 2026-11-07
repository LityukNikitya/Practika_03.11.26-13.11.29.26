using ECommerceCsvImport.Core.Models;

namespace ECommerceCsvImport.Core.Services
{
    public interface IProductRepository
    {
        Task AddAsync(Product product);
        IReadOnlyList<Product> GetAll();
        Product? GetBySku(string sku);
        int Count { get; }
        void Clear();
    }
}
