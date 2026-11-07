using System.Text;
using ECommerceCsvImport.Core.Models;
using ECommerceCsvImport.Core.Services;

namespace ECommerceCsvImport.Tests
{
    public class ProductCsvExporterTests
    {
        private const string Header = "sku,name,description,price,currency,category_name,stock,image_url,is_active,brand,warranty_months,model_number";
        private const int BomLength = 3;

        private static Product CreateProduct()
        {
            return new Product
            {
                Sku = "1001",
                Name = "Ноутбук Lenovo IdeaPad 3",
                Description = "Бюджетный ноутбук",
                Price = 45990m,
                Currency = "RUB",
                CategoryName = "Компьютеры",
                StockQuantity = 5,
                ImageUrl = "/images/laptop.jpg",
                IsActive = true,
                Brand = "Lenovo",
                WarrantyMonths = 24,
                ModelNumber = "IdeaPad3-15"
            };
        }

        [Fact]
        public void Export_SingleProduct_ReturnsBomHeaderAndRow()
        {
            var bytes = ProductCsvExporter.Export(new[] { CreateProduct() });

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(BomLength).ToArray());

            var text = Encoding.UTF8.GetString(bytes, BomLength, bytes.Length - BomLength);
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            Assert.Equal(2, lines.Length);
            Assert.Equal(Header, lines[0]);
            Assert.Equal("1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad3-15", lines[1]);
            Assert.Throws<ArgumentNullException>(() => ProductCsvExporter.Export(null!));
        }

        [Fact]
        public void Export_FieldWithCommaAndQuotes_IsQuotedAndEscaped()
        {
            var product = CreateProduct();
            product.Name = "Ноутбук, \"игровой\"";

            var bytes = ProductCsvExporter.Export(new[] { product });
            var text = Encoding.UTF8.GetString(bytes, BomLength, bytes.Length - BomLength);

            Assert.Contains("\"Ноутбук, \"\"игровой\"\"\"", text);
        }
    }
}
