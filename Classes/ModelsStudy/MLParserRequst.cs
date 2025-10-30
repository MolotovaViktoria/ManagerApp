using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ManagerApp.Classes.ModelsStudy
{
    public class MLParserRequst
    {
        private ProductMLClassifier _classifier;

        public MLParserRequst()
        {
            _classifier = new ProductMLClassifier();
        }

        public List<ProductRequst> ParseProducts(string data)
        {
            var products = new List<ProductRequst>();

            if (string.IsNullOrWhiteSpace(data))
                return products;

            // Извлекаем все потенциальные товары
            var potentialProducts = ExtractPotentialProducts(data);

            foreach (var product in potentialProducts)
            {
                // Используем ML для фильтрации
                var result = _classifier.Classify(product.Name);

                if (result.IsProduct && result.Confidence > 0.3)
                {
                    products.Add(new ProductRequst
                    {
                        Name = CleanName(product.Name),
                        Quantity = product.Quantity
                    });
                }
            }

            return products;
        }

        // Метод для извлечения ВСЕХ потенциальных товаров (без фильтрации)
        public List<ProductRequst> ExtractAllPotentialProducts(string data)
        {
            var products = new List<ProductRequst>();

            var matches = Regex.Matches(data, @"([А-ЯA-Z][^0-9]{10,}?)\s+ШТ\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    var name = match.Groups[1].Value.Trim();
                    var quantityStr = match.Groups[2].Value;

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

        // Метод для классификации отдельного текста
        public ProductMLClassifier.ClassificationResult ClassifyText(string text)
        {
            return _classifier.Classify(text);
        }

        // Метод для обучения модели
        public void TrainModel(string text, bool isProduct, string category = "ELECTRICAL")
        {
            var examples = new List<ProductMLClassifier.TrainingExample>
            {
                new ProductMLClassifier.TrainingExample
                {
                    Text = text,
                    Category = isProduct ? category : "NOT_PRODUCT",
                    IsProduct = isProduct
                }
            };

            _classifier.Train(examples);
            _classifier.SaveModel();
        }

        // Инициализация модели начальными данными
        public void InitializeModel()
        {
            _classifier.InitializeWithDefaultData();
        }

        // Получение информации о модели
        public string GetModelInfo()
        {
            return _classifier.GetModelInfo();
        }

        private List<(string Name, decimal Quantity)> ExtractPotentialProducts(string data)
        {
            var result = new List<(string, decimal)>();

            var matches = Regex.Matches(data, @"([А-ЯA-Z][^0-9]{10,}?)\s+ШТ\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    var name = match.Groups[1].Value.Trim();
                    var quantityStr = match.Groups[2].Value;

                    if (decimal.TryParse(quantityStr, out decimal quantity))
                    {
                        result.Add((name, quantity));
                    }
                }
            }

            return result;
        }

        private string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            name = Regex.Replace(name, @"\s+", " ").Trim();
            name = Regex.Replace(name, @"^нет\s*", "", RegexOptions.IgnoreCase);

            // Убираем некоторые служебные слова
            var stopWords = new[] { "российская", "федерация", "рф", "россия", "ооо", "зао", "ао" };
            foreach (var word in stopWords)
            {
                name = Regex.Replace(name, $@"\b{word}\b", "", RegexOptions.IgnoreCase);
            }

            return Regex.Replace(name, @"\s+", " ").Trim();
        }
    }
}
