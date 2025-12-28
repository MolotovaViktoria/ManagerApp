namespace ManagerApp.Classes.Read.ReadPicture
{
    public static class SimpleExtensions
    {
        // Повторяем символ несколько раз
        public static string RepeatChar(this char ch, int count)
        {
            return new string(ch, count);
        }

        // Проверяем, пустая ли строка или содержит только пробелы
        public static bool IsReallyEmpty(this string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }
    }
}