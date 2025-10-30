using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ManagerApp.Classes.ModelsStudy
{
    public class ProductMLClassifier
    {
        private MLModel _model;
        private List<TrainingExample> _trainingData;

        public ProductMLClassifier()
        {
            _model = new MLModel();
            _trainingData = new List<TrainingExample>();
            LoadOrInitializeModel();
        }

        public class TrainingExample
        {
            public string Text { get; set; }
            public string Category { get; set; }
            public bool IsProduct { get; set; }
        }

        public class MLModel
        {
            public Dictionary<string, Dictionary<string, double>> Features { get; set; }
            public Dictionary<string, double> CategoryWeights { get; set; }
            public Dictionary<string, int> TrainingCounts { get; set; }

            public MLModel()
            {
                Features = new Dictionary<string, Dictionary<string, double>>();
                CategoryWeights = new Dictionary<string, double>();
                TrainingCounts = new Dictionary<string, int>();
            }
        }

        public class ClassificationResult
        {
            public string Category { get; set; }
            public double Confidence { get; set; }
            public bool IsProduct { get; set; }
        }

        // Обучение модели
        public void Train(List<TrainingExample> examples)
        {
            foreach (var example in examples)
            {
                var words = Tokenize(example.Text);

                if (!_model.CategoryWeights.ContainsKey(example.Category))
                {
                    _model.CategoryWeights[example.Category] = 0;
                    _model.TrainingCounts[example.Category] = 0;
                }

                _model.CategoryWeights[example.Category] += 1;
                _model.TrainingCounts[example.Category] += 1;

                foreach (var word in words)
                {
                    if (!_model.Features.ContainsKey(word))
                    {
                        _model.Features[word] = new Dictionary<string, double>();
                    }

                    if (!_model.Features[word].ContainsKey(example.Category))
                    {
                        _model.Features[word][example.Category] = 0;
                    }

                    _model.Features[word][example.Category] += 1;
                }
            }

            // Нормализуем веса
            NormalizeWeights();
            SaveModel();
        }

        // Классификация текста
        public ClassificationResult Classify(string text)
        {
            var words = Tokenize(text);
            var scores = new Dictionary<string, double>();

            foreach (var category in _model.CategoryWeights.Keys)
            {
                scores[category] = _model.CategoryWeights[category];

                foreach (var word in words)
                {
                    if (_model.Features.ContainsKey(word) && _model.Features[word].ContainsKey(category))
                    {
                        scores[category] += _model.Features[word][category];
                    }
                }
            }

            if (scores.Count == 0)
            {
                return new ClassificationResult
                {
                    Category = "UNKNOWN",
                    Confidence = 0,
                    IsProduct = false
                };
            }

            var bestCategory = scores.OrderByDescending(x => x.Value).First();
            var isProduct = bestCategory.Key != "NOT_PRODUCT";
            var confidence = words.Count > 0 ? bestCategory.Value / words.Count : 0;

            return new ClassificationResult
            {
                Category = bestCategory.Key,
                Confidence = confidence,
                IsProduct = isProduct
            };
        }

        // Разбивка текста на слова
        private List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            // Убираем спецсимволы, оставляем только слова
            var cleanText = Regex.Replace(text, @"[^\w\sа-яёА-ЯЁ]", " ");
            cleanText = Regex.Replace(cleanText, @"\s+", " ").Trim().ToLower();

            return cleanText.Split(' ')
                          .Where(word => word.Length > 2) // Игнорируем короткие слова
                          .Distinct()
                          .ToList();
        }

        private void NormalizeWeights()
        {
            var totalExamples = _model.TrainingCounts.Values.Sum();

            if (totalExamples == 0) return;

            foreach (var category in _model.CategoryWeights.Keys.ToList())
            {
                _model.CategoryWeights[category] = _model.TrainingCounts[category] / (double)totalExamples;
            }

            foreach (var word in _model.Features.Keys)
            {
                var totalWordCount = _model.Features[word].Values.Sum();
                if (totalWordCount > 0)
                {
                    foreach (var category in _model.Features[word].Keys.ToList())
                    {
                        _model.Features[word][category] /= totalWordCount;
                    }
                }
            }
        }

        // Сохранение и загрузка модели (ИСПРАВЛЕНО)
        public void SaveModel()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_model, Formatting.Indented);
                File.WriteAllText("product_model.json", json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения модели: {ex.Message}");
            }
        }

        private void LoadOrInitializeModel()
        {
            try
            {
                if (File.Exists("product_model.json"))
                {
                    var json = File.ReadAllText("product_model.json", Encoding.UTF8);
                    _model = JsonConvert.DeserializeObject<MLModel>(json);

                    if (_model == null)
                        _model = new MLModel();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки модели: {ex.Message}");
                _model = new MLModel();
            }
        }

        // Метод для начального обучения модели
        public void InitializeWithDefaultData()
        {
            var defaultExamples = new List<TrainingExample>
            {
                // Примеры товаров
                new TrainingExample () { Text = "Блок розеток 220 В 19 1U 8 розеток", Category = "ELECTRICAL", IsProduct = true },
                new TrainingExample() { Text = "Вилка евро прямая TDM 16А 250В каучуковая", Category = "ELECTRICAL", IsProduct = true },
                new TrainingExample() { Text = "Выключатель Schneider Electric Рондо 10А", Category = "ELECTRICAL", IsProduct = true },
                new TrainingExample() { Text = "Коробка распределительная Hegel IP55", Category = "ELECTRICAL", IsProduct = true },
                new TrainingExample() { Text = "Розетка компьютерная DKC Brava RJ45", Category = "ELECTRICAL", IsProduct = true },
                new TrainingExample() { Text = "Патрон для ламп накаливания керамический", Category = "ELECTRICAL", IsProduct = true },
                new TrainingExample() { Text = "Стартер IEK LS 111M 4-65ВТ 220-240В", Category = "ELECTRICAL", IsProduct = true },
                new TrainingExample() { Text = "Штанга заземляющая переносная ШЗП-15", Category = "ELECTRICAL", IsProduct = true },
                
                // Примеры НЕ товаров
                new TrainingExample() { Text = "Российская Федерация РФ Россия", Category = "NOT_PRODUCT", IsProduct = false },
                new TrainingExample() { Text = "Нет в наличии", Category = "NOT_PRODUCT", IsProduct = false },
                new TrainingExample() { Text = "Страна происхождения", Category = "NOT_PRODUCT", IsProduct = false },
                new TrainingExample() { Text = "Максимальная цена поставщиков", Category = "NOT_PRODUCT", IsProduct = false },
                new TrainingExample() { Text = "ООО ТД МК НВР", Category = "NOT_PRODUCT", IsProduct = false },
                new TrainingExample() { Text = "Ценовой запрос №829183", Category = "NOT_PRODUCT", IsProduct = false },
                new TrainingExample() { Text = "ГАЗПРОМ ТРАНСГАЗ СУРГУТ", Category = "NOT_PRODUCT", IsProduct = false },
            };

            Train(defaultExamples);
        }

        // Метод для получения информации о модели (для отладки)
        public string GetModelInfo()
        {
            return $"Категории: {_model.CategoryWeights.Count}, Слова: {_model.Features.Count}, Примеров: {_model.TrainingCounts.Values.Sum()}";
        }
    }
}