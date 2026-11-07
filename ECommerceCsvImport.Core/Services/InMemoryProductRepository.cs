using ECommerceCsvImport.Core.Models;

namespace ECommerceCsvImport.Core.Services
{
    public class InMemoryProductRepository : IProductRepository
    {
        private readonly List<Product> _products = new();
        private readonly object _lock = new();

        // Количество товаров в репозитории
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _products.Count;
                }
            }
        }

        // Добавляет товар в коллекцию
        public Task AddAsync(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            lock (_lock)
            {
                _products.Add(product);
            }

            return Task.CompletedTask;
        }

        // Возвращает снапшот списка товаров
        public IReadOnlyList<Product> GetAll()
        {
            lock (_lock)
            {
                return _products.ToList();
            }
        }

        // Ищет товар по SKU без учета регистра и пробелов по краям
        public Product? GetBySku(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
                return null;

            lock (_lock)
            {
                return _products.FirstOrDefault(p =>
                    string.Equals(p.Sku, sku.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }

        // Удаляет все товары
        public void Clear()
        {
            lock (_lock)
            {
                _products.Clear();
            }
        }
    }
}
