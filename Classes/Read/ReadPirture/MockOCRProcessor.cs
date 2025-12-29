using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManagerApp.Classes.Read.ReadPicture
{
    public class MockOCRProcessor : IOCRProcessor
    {
        public bool IsReady() => true;

        public List<string> ProcessImage(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    return new List<string> { "Файл изображения не найден" };
                }

                Console.WriteLine($"📷 Эмуляция OCR: {Path.GetFileName(imagePath)}");

                // Возвращаем тестовые данные
                return new List<string>
                {
                    "Молоко Простоквашино 2.5%",
                    "Хлеб Бородинский нарезка",
                    "Яйца куриные С1, 10 шт",
                    "Сметана 20% Вкуснотеево",
                    "Сыр Российский 50%",
                    "Колбаса Докторская",
                    "Макароны Макфа",
                    "Сахар песок",
                    "Чай Greenfield",
                    "Кофе Jacobs"
                };
            }
            catch (Exception ex)
            {
                return new List<string> { $"Ошибка: {ex.Message}" };
            }
        }
    }
}