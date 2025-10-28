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

            // Ищем все строки с паттерном: число + название + число (количество)
            var matches = Regex.Matches(data, @"(\d+)[^\w]*(КАБЕЛЬ[^0-9]{10,}?|ПРОВОД[^0-9]{10,}?)(\d+)", RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 4)
                {
                    var name = match.Groups[2].Value.Trim();
                    var quantityStr = match.Groups[3].Value;

                    if (decimal.TryParse(quantityStr, out decimal quantity))
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

        private string CleanName(string name)
        {
            // Убираем лишние пробелы
            name = Regex.Replace(name, @"\s+", " ").Trim();
            return name;
        }

    }
}
