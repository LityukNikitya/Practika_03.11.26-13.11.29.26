using ECommerceCsvImport.Core.Models;
using ECommerceCsvImport.Core.Services;

namespace ECommerceCsvImport.Tests
{
    public class RepositoryTests
    {
        private static Product CreateProduct(string sku = "1001")
        {
            return new Product
            {
                Sku = sku,
                Name = "Ноутбук Lenovo IdeaPad 3",
                Price = 45990m,
                Brand = "Lenovo"
            };
        }

        [Fact]
        public async Task Add_Product_GetAllContainsIt()
        {
            var repository = new InMemoryProductRepository();

            await repository.AddAsync(CreateProduct());

            var all = repository.GetAll();

            Assert.Single(all);
            Assert.Equal("1001", all[0].Sku);
            Assert.Equal(1, repository.Count);
        }

        [Fact]
        public void EmptyRepository_NewInstance_HasNoProducts()
        {
            var repository = new InMemoryProductRepository();

            Assert.Equal(0, repository.Count);
            Assert.Empty(repository.GetAll());
            Assert.Null(repository.GetBySku("1001"));
        }

        [Fact]
        public async Task Add_NullProduct_ThrowsArgumentNullException()
        {
            var repository = new InMemoryProductRepository();

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await repository.AddAsync(null!));
        }

        [Fact]
        public async Task GetBySku_ExistingAndUnknownSku_ReturnsProductOrNull()
        {
            var repository = new InMemoryProductRepository();
            var product = CreateProduct("AB-1");
            await repository.AddAsync(product);

            Assert.Same(product, repository.GetBySku("AB-1"));
            Assert.Same(product, repository.GetBySku(" ab-1 "));
            Assert.Null(repository.GetBySku("9999"));
        }

        [Fact]
        public async Task CountAndClear_AfterSeveralAdds_CountsAndEmptiesRepository()
        {
            var repository = new InMemoryProductRepository();
            await repository.AddAsync(CreateProduct("1001"));
            await repository.AddAsync(CreateProduct("1002"));
            await repository.AddAsync(CreateProduct("1003"));

            Assert.Equal(3, repository.Count);

            repository.Clear();

            Assert.Equal(0, repository.Count);
            Assert.Empty(repository.GetAll());
            Assert.Null(repository.GetBySku("1001"));
        }
    }
}
