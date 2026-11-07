using System.Text;

namespace ECommerceCsvImport.Core.Services
{
    public static class CsvLineParser
    {
        // Разбор строки CSV с запятой в качестве разделителя.
        public static IReadOnlyList<string> Parse(string line)
        {
            return Parse(line, ',');
        }

        // Разбор строки CSV с поддержкой кавычек и заданным разделителем.
        public static IReadOnlyList<string> Parse(string line, char delimiter)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString().Trim());

            return fields;
        }
    }
}
