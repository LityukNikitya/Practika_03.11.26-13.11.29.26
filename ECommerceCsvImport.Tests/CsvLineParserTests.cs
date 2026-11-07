using ECommerceCsvImport.Core.Services;

namespace ECommerceCsvImport.Tests
{
    public class CsvLineParserTests
    {
        private const int ExpectedFieldCount = 12;

        [Fact]
        public void Parse_PlainFields_ReturnsTwelveTrimmedFields()
        {
            var line = "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24, IdeaPad3-15 ";

            var fields = CsvLineParser.Parse(line);

            Assert.Equal(ExpectedFieldCount, fields.Count);
            Assert.Equal("1001", fields[0]);
            Assert.Equal("IdeaPad3-15", fields[11]);
        }

        [Fact]
        public void Parse_EmptyFields_ReturnsEmptyThirdAndEighthFields()
        {
            var line = "1011,Блок питания Corsair CV550,,4990,RUB,Компьютеры,9,,true,Corsair,60,CV550";

            var fields = CsvLineParser.Parse(line);

            Assert.Equal(ExpectedFieldCount, fields.Count);
            Assert.Equal(string.Empty, fields[2]);
            Assert.Equal(string.Empty, fields[7]);
        }

        [Fact]
        public void Parse_QuotedFields_KeepsCommaAndUnescapesQuotes()
        {
            var line = "1012,\"Ноутбук, игровой\",\"Название \"\"Special\"\"\",59990,RUB,Компьютеры,1,/images/notebook.jpg,true,ASUS,36,VivoBook";

            var fields = CsvLineParser.Parse(line);

            Assert.Equal(ExpectedFieldCount, fields.Count);
            Assert.Equal("Ноутбук, игровой", fields[1]);
            Assert.Equal("Название \"Special\"", fields[2]);
        }

        [Fact]
        public void Parse_SemicolonDelimiter_KeepsDelimiterInsideQuotes()
        {
            var line = "1012;\"Ноутбук; игровой\";Описание;59990;RUB;Компьютеры;1;/images/notebook.jpg;true;ASUS;36;VivoBook";

            var fields = CsvLineParser.Parse(line, ';');

            Assert.Equal(ExpectedFieldCount, fields.Count);
            Assert.Equal("Ноутбук; игровой", fields[1]);
        }
    }
}
