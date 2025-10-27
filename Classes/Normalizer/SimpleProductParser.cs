using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ManagerApp.Classes.Normalizer
{
    public class SimpleProductParser
    {
        public static List<ProductRequst> ExtractProducts(string text)
        {
            var products = new List<ProductRequst>();

            // Ищем все вхождения паттерна "цифра.Название"
            var matches = Regex.Matches(text, @"(\d+)\.\s*([А-Яа-яA-Z].*?)(?=\d+\.|$)", RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var productText = match.Groups[2].Value; // Текст после номера

                // Извлекаем название (до первого числа или служебного слова)
                var name = ExtractProductName(productText);

                // Ищем количество (число перед единицами измерения)
                var quantity = FindQuantity(productText);

                if (!string.IsNullOrEmpty(name) && quantity > 0)
                {
                    products.Add(new ProductRequst
                    {
                        Name = name.Trim(),
                        Quantity = quantity
                    });
                }
            }

            return products;
        }

        private static string ExtractProductName(string text)
        {
            // Ищем конец названия - либо число, либо служебные слова
            var endMatch = Regex.Match(text, @"(\d+)\s*(м|шт|М|ШТ|$)|(Общепром|сейсмос|безоп|Кл\.|Категория)");

            if (endMatch.Success && endMatch.Index > 0)
            {
                return text.Substring(0, endMatch.Index).Trim();
            }

            return text.Trim();
        }

        private static decimal FindQuantity(string text)
        {
            // Ищем паттерны: "900М", "900 М", "900м"
            var match = Regex.Match(text, @"(\d+)\s*(м|М|шт|ШТ)");
            if (match.Success)
            {
                return decimal.Parse(match.Groups[1].Value);
            }

            // Если не нашли с единицами, ищем просто число (последнее в строке)
            var numbers = Regex.Matches(text, @"\d+");
            if (numbers.Count > 0)
            {
                // Берем последнее число (скорее всего это количество)
                return decimal.Parse(numbers[numbers.Count - 1].Value);
            }

            return 0;
        }
    }
}
