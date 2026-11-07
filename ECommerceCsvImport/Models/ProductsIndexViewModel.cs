using ECommerceCsvImport.Core.Models;

namespace ECommerceCsvImport.Models
{
    public class ProductsIndexViewModel
    {
        public IReadOnlyList<Product> Products { get; set; } = new List<Product>();

        public string? SearchString { get; set; }

        public string? CategoryFilter { get; set; }

        public string? SortOrder { get; set; }

        public List<string> Categories { get; set; } = new();

        public int AllProductsCount { get; set; }

        public decimal AllProductsTotalValue { get; set; }

        public int TotalCount => Products.Count;

        public decimal TotalValue => Products.Sum(p => p.Price);
    }
}
