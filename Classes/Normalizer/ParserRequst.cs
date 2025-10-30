using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ManagerApp.Classes.Normalizer
{
    public class ParserRequst
    {
        public List<ProductRequst> ParseProducts(string data)
        {
            var products = new List<ProductRequst>();

            if (string.IsNullOrWhiteSpace(data))
                return products;

            // Улучшенный паттерн - ищем любой товар (не только кабели)
            var matches = Regex.Matches(data, @"(\d+)[^\w]*([А-ЯA-Z][^0-9]{10,}?)(\d+)", RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 4)
                {
                    var name = match.Groups[2].Value.Trim();
                    var quantityStr = match.Groups[3].Value;

                    // Проверяем что это действительно товар, а не мусор
                    if (IsValidProductName(name) && decimal.TryParse(quantityStr, out decimal quantity))
                    {
                        products.Add(new ProductRequst
                        {
                            Name = CleanName(name),
                            Quantity = quantity
                        });
                    }
                }
            }

            return products;
        }

        private bool IsValidProductName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length < 5)
                return false;

            // Ключевые слова, которые указывают на товар
            var productKeywords = new[]
            {
                "блок", "вилка", "выключатель", "коробка", "патрон", "розетка",
                "стартер", "штепсель", "разъем", "переходник", "кабель", "провод",
                "лампа", "предохранитель", "трансформатор", "реле", "диод", "конденсатор"
            };

            return productKeywords.Any(keyword =>
                name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private string CleanName(string name)
        {
            // Убираем лишние пробелы
            name = Regex.Replace(name, @"\s+", " ").Trim();

            // Убираем цифры в начале
            name = Regex.Replace(name, @"^\d+\s*", "");

            return name;
        }
    }
}