using ECommerceCsvImport.Core.Models;
using ECommerceCsvImport.Models;

namespace ECommerceCsvImport.Tests
{
    public class ViewModelTests
    {
        [Fact]
        public void ViewModels_ComputedProperties_AreCalculated()
        {
            var model = new ProductsIndexViewModel
            {
                Products = new List<Product>
                {
                    new Product { Sku = "1", Price = 100.50m },
                    new Product { Sku = "2", Price = 200m }
                }
            };

            Assert.Equal(2, model.TotalCount);
            Assert.Equal(300.50m, model.TotalValue);
            Assert.False(new ErrorViewModel { RequestId = null }.ShowRequestId);
            Assert.True(new ErrorViewModel { RequestId = "abc" }.ShowRequestId);
        }
    }
}
