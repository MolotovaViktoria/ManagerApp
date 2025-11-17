using ManagerApp.Data.GetInfo;
using ManagerApp.Data.StructureList;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Classes.Search
{
    public class SmartProductSearch
    {
        private List<Product> _allProducts;
        private bool _isCacheLoaded = false;

        /// <summary>
        /// Инициализация поиска с предварительной обработкой данных
        /// </summary>
        public async Task InitializeAsync()
        {
            if (!_isCacheLoaded)
            {
                _allProducts = await GetAllCachedProducts();
                _isCacheLoaded = true;
            }
        }

        /// <summary>
        /// Умный поиск товаров с учетом различных форматов написания
        /// </summary>
        public async Task<List<SearchResult>> SmartSearch(string searchQuery, int maxResults = 10)
        {
            await InitializeAsync();

            if (string.IsNullOrWhiteSpace(searchQuery))
                return new List<SearchResult> { new SearchResult { Score = 0, Message = "Введите поисковый запрос" } };

            if (!_allProducts.Any())
                return new List<SearchResult> { new SearchResult { Score = 0, Message = "Кеш товаров пуст" } };

            // Нормализуем поисковый запрос
            var normalizedQuery = NormalizeText(searchQuery);
            var queryWords = Tokenize(normalizedQuery);

            var results = new List<SearchResult>();

            foreach (var product in _allProducts)
            {
                if (product.Name == null) continue;

                var normalizedProductName = NormalizeText(product.Name);
                var productWords = Tokenize(normalizedProductName);

                // Вычисляем несколько метрик схожести
                var score = CalculateSimilarityScore(queryWords, productWords, normalizedQuery, normalizedProductName);

                if (score > 0.1) // Пороговое значение для отсечения совсем непохожих
                {
                    results.Add(new SearchResult
                    {
                        Product = product,
                        Score = score,
                        MatchedWords = GetMatchedWords(queryWords, productWords),
                        Message = FormatProductResult(product)
                    });
                }
            }

            // Сортируем по релевантности и возвращаем топ результатов
            return results
                .OrderByDescending(r => r.Score)
                .Take(maxResults)
                .ToList();
        }

        /// <summary>
        /// Нормализация текста: приведение к нижнему регистру, удаление лишних символов
        /// </summary>
        private string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Приводим к нижнему регистру
            text = text.ToLowerInvariant();

            // Удаляем специальные символы, оставляя только буквы, цифры и пробелы
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Разбивка текста на токены (слова)
        /// </summary>
        private List<string> Tokenize(string text)
        {
            return text.Split(new[] { ' ', '-', ',', '.', ';' }, StringSplitOptions.RemoveEmptyEntries)
                      .Where(word => word.Length > 2) // Игнорируем слишком короткие слова
                      .Distinct()
                      .ToList();
        }

        /// <summary>
        /// Вычисление общей оценки схожести
        /// </summary>
        private double CalculateSimilarityScore(List<string> queryWords, List<string> productWords,
                                              string fullQuery, string fullProductName)
        {
            double totalScore = 0;

            // 1. Полное совпадение (самый высокий вес)
            if (fullProductName.Contains(fullQuery))
                totalScore += 0.4;

            // 2. Совпадение по словам
            var wordMatchScore = CalculateWordMatchScore(queryWords, productWords);
            totalScore += wordMatchScore * 0.4;

            // 3. Схожесть по Левенштейну для всего текста
            var levenshteinScore = CalculateLevenshteinSimilarity(fullQuery, fullProductName);
            totalScore += levenshteinScore * 0.2;

            return Math.Min(totalScore, 1.0);
        }

        /// <summary>
        /// Оценка совпадения по словам
        /// </summary>
        private double CalculateWordMatchScore(List<string> queryWords, List<string> productWords)
        {
            if (!queryWords.Any() || !productWords.Any()) return 0;

            int matches = 0;
            foreach (var queryWord in queryWords)
            {
                // Ищем точное совпадение или частичное вхождение
                if (productWords.Any(productWord =>
                    productWord.Contains(queryWord) || queryWord.Contains(productWord)))
                {
                    matches++;
                }
            }

            return (double)matches / queryWords.Count;
        }

        /// <summary>
        /// Вычисление схожести по алгоритму Левенштейна
        /// </summary>
        private double CalculateLevenshteinSimilarity(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0;

            int distance = ComputeLevenshteinDistance(a, b);
            int maxLength = Math.Max(a.Length, b.Length);

            if (maxLength == 0) return 1.0;

            return 1.0 - (double)distance / maxLength;
        }

        /// <summary>
        /// Вычисление расстояния Левенштейна
        /// </summary>
        private int ComputeLevenshteinDistance(string a, string b)
        {
            var matrix = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
                matrix[i, 0] = i;
            for (int j = 0; j <= b.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[a.Length, b.Length];
        }

        /// <summary>
        /// Получение списка совпавших слов
        /// </summary>
        private List<string> GetMatchedWords(List<string> queryWords, List<string> productWords)
        {
            var matched = new List<string>();
            foreach (var queryWord in queryWords)
            {
                var match = productWords.FirstOrDefault(pw =>
                    pw.Contains(queryWord) || queryWord.Contains(pw));
                if (match != null)
                {
                    matched.Add(match);
                }
            }
            return matched;
        }

        /// <summary>
        /// Упрощенный поиск для быстрого использования
        /// </summary>
        public async Task<string> SearchSimple(string searchQuery)
        {
            var results = await SmartSearch(searchQuery, 5);

            // Фильтруем только валидные результаты с Product != null
            var validResults = results.Where(r => r.Product != null).ToList();

            if (!validResults.Any())
                return $"Товары по запросу '{searchQuery}' не найдены";

            var bestResult = validResults.First();

            if (bestResult.Score > 0.7)
            {
                return $"Найден товар: {bestResult.Product.Name}\nЦена: {FormatPrice((decimal?)bestResult.Product.Price)}\n(Уверенность: {bestResult.Score:P0})";
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Найдено {validResults.Count} похожих товаров:");

                foreach (var result in validResults.Take(3))
                {
                    sb.AppendLine($"• {result.Product.Name} - {FormatPrice((decimal?)result.Product.Price)} (схожесть: {result.Score:P0})");
                }

                return sb.ToString();
            }
        }
        private async Task<List<Product>> GetAllCachedProducts()
        {
            var allProducts = new List<Product>();
            var categoryIds = GetLoadedCategoryIds();

            foreach (var categoryId in categoryIds)
            {
                if (BitrixCache.IsCategoryCached(categoryId))
                {
                    var products = await BitrixCache.GetProductsByCategory(categoryId);
                    allProducts.AddRange(products);
                }
            }

            return allProducts;
        }

        private List<int> GetLoadedCategoryIds()
        {
            return new List<int> { 691 };
        }

        private string FormatProductResult(Product product)
        {
            return $"{product.Name} - {FormatPrice((decimal?)product.Price)}";
        }

        private string FormatPrice(decimal? price)
        {
            return price.HasValue ? $"{price.Value:N2} руб." : "Цена не указана";
        }
    }

    /// <summary>
    /// Результат поиска с дополнительной информацией
    /// </summary>
    public class SearchResult
    {
        public Product Product { get; set; }
        public double Score { get; set; } // 0-1, где 1 - полное совпадение
        public List<string> MatchedWords { get; set; }
        public string Message { get; set; }
    }
}