using System.Globalization;
using System.Text;
using ECommerceCsvImport.Core.Models;

namespace ECommerceCsvImport.Core.Services
{
    public static class ProductCsvExporter
    {
        private const string CsvHeader = "sku,name,description,price,currency,category_name,stock,image_url,is_active,brand,warranty_months,model_number";

        // Экспорт товаров в CSV (UTF-8 с BOM для Excel)
        public static byte[] Export(IEnumerable<Product> products)
        {
            ArgumentNullException.ThrowIfNull(products);

            var builder = new StringBuilder();
            builder.Append(CsvHeader).Append("\r\n");

            foreach (var product in products)
            {
                var fields = new[]
                {
                    product.Sku,
                    product.Name,
                    product.Description,
                    product.Price.ToString(CultureInfo.InvariantCulture),
                    product.Currency,
                    product.CategoryName,
                    product.StockQuantity.ToString(CultureInfo.InvariantCulture),
                    product.ImageUrl,
                    product.IsActive ? "true" : "false",
                    product.Brand,
                    product.WarrantyMonths.ToString(CultureInfo.InvariantCulture),
                    product.ModelNumber
                };

                builder.Append(string.Join(',', fields.Select(EscapeCsvField))).Append("\r\n");
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(builder.ToString());
            var bytes = new byte[preamble.Length + body.Length];
            preamble.CopyTo(bytes, 0);
            body.CopyTo(bytes, preamble.Length);

            return bytes;
        }

        // Экранирует поле CSV: кавычки при наличии запятой, кавычки или перевода строки
        private static string EscapeCsvField(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
