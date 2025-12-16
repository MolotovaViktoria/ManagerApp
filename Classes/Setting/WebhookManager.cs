using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ManagerApp.Classes.Setting
{
    public static class WebhookManager
    {
        private const string WebhookFileName = "webhook.txt";
        private const string DefaultWebhook = "";

        // Свойство для получения и установки вебхука
        public static string Webhook
        {
            get
            {
                return GetWebhookFromFile();
            }
            set
            {
                SaveWebhookToFile(value);
            }
        }

        // Получение вебхука из файла
        public static string GetWebhookFromFile()
        {
            try
            {
                if (File.Exists(WebhookFileName))
                {
                    string webhook = File.ReadAllText(WebhookFileName);
                    return string.IsNullOrWhiteSpace(webhook) ? DefaultWebhook : webhook.Trim();
                }
                return DefaultWebhook;
            }
            catch (Exception)
            {
                return DefaultWebhook;
            }
        }

        // Сохранение вебхука в файл
        public static void SaveWebhookToFile(string webhook)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webhook))
                {
                    webhook = DefaultWebhook;
                }
                else
                {
                    webhook = webhook.Trim();
                }

                File.WriteAllText(WebhookFileName, webhook);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Ошибка сохранения вебхука: {ex.Message}");
            }
        }

        // Проверка валидности URL вебхука
        public static bool IsValidWebhookUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true; // Пустая строка допустима

            // Убираем пробелы
            url = url.Trim();

            // Проверяем, что URL начинается с https://
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;

            // Проверяем, что URL содержит bitrix24
            if (url.IndexOf("bitrix24", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            // Проверяем, что URL содержит rest
            if (url.IndexOf("/rest/", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            // Общая проверка URL
            string pattern = @"^https://[a-zA-Z0-9\-\.]+\.bitrix24\.(ru|com|by|kz|ua)/rest/\d+/[a-zA-Z0-9]+/?$";
            return Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase);
        }

        // Тестовая функция для проверки подключения к Bitrix24
        public static async Task<bool> TestConnectionAsync()
        {
            try
            {
                string webhook = GetWebhookFromFile();
                if (string.IsNullOrWhiteSpace(webhook))
                    return false;

                // Здесь будет код для тестирования подключения к Bitrix24 API
                // Пока возвращаем заглушку
                return await Task.FromResult(true);
            }
            catch
            {
                return false;
            }
        }

        // Получение информации о вебхуке (для отображения в UI)
        public static string GetWebhookInfo()
        {
            string webhook = GetWebhookFromFile();
            if (string.IsNullOrWhiteSpace(webhook))
                return "Вебхук не настроен";

            try
            {
                // Извлекаем домен из URL
                Uri uri = new Uri(webhook);
                return $"Bitrix24: {uri.Host}";
            }
            catch
            {
                return "Вебхук настроен";
            }
        }
    }
}