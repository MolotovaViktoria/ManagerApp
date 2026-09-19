using System;
using System.Text;

namespace ManagerApp.Classes.Invoicing
{
    // Переводит сумму в рублях в текстовое представление "прописью" для счетов.
    public static class NumberToWordsRu
    {
        private static readonly string[][] OnesForms =
        {
            new[] {"", ""}, new[] {"один", "одна"}, new[] {"два", "две"}, new[] {"три", "три"},
            new[] {"четыре", "четыре"}, new[] {"пять", "пять"}, new[] {"шесть", "шесть"},
            new[] {"семь", "семь"}, new[] {"восемь", "восемь"}, new[] {"девять", "девять"}
        };

        private static readonly string[] Teens =
        {
            "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать",
            "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать"
        };

        private static readonly string[] Tens =
        {
            "", "", "двадцать", "тридцать", "сорок", "пятьдесят",
            "шестьдесят", "семьдесят", "восемьдесят", "девяносто"
        };

        private static readonly string[] Hundreds =
        {
            "", "сто", "двести", "триста", "четыреста", "пятьсот",
            "шестьсот", "семьсот", "восемьсот", "девятьсот"
        };

        // {единственное, родительный ед., родительный мн.}, признак женского рода
        private static readonly (string one, string few, string many, bool female)[] Scales =
        {
            ("", "", "", false),
            ("тысяча", "тысячи", "тысяч", true),
            ("миллион", "миллиона", "миллионов", false),
            ("миллиард", "миллиарда", "миллиардов", false),
        };

        public static string ConvertRubles(decimal amount)
        {
            amount = Math.Abs(amount);
            long rubles = (long)Math.Floor(amount);
            int kopecks = (int)Math.Round((amount - rubles) * 100);
            if (kopecks == 100) { kopecks = 0; rubles++; }

            string rublesText = rubles == 0 ? "ноль" : ConvertInteger(rubles);
            string rublesWord = PluralForm(rubles, "рубль", "рубля", "рублей");

            string kopecksWord = PluralForm(kopecks, "копейка", "копейки", "копеек");

            var sb = new StringBuilder();
            sb.Append(Capitalize(rublesText)).Append(' ').Append(rublesWord);
            sb.Append(' ').Append(kopecks.ToString("D2")).Append(' ').Append(kopecksWord);
            return sb.ToString();
        }

        private static string ConvertInteger(long number)
        {
            if (number == 0) return "ноль";

            var groups = new long[4];
            for (int i = 0; i < 4 && number > 0; i++)
            {
                groups[i] = number % 1000;
                number /= 1000;
            }

            var parts = new System.Collections.Generic.List<string>();
            for (int scale = 3; scale >= 0; scale--)
            {
                long group = groups[scale];
                if (group == 0) continue;

                bool female = Scales[scale].female;
                string groupText = ConvertGroup(group, female);
                parts.Add(groupText);

                if (scale > 0)
                {
                    parts.Add(PluralForm(group, Scales[scale].one, Scales[scale].few, Scales[scale].many));
                }
            }

            return string.Join(" ", parts).Trim();
        }

        private static string ConvertGroup(long group, bool female)
        {
            var parts = new System.Collections.Generic.List<string>();

            int hundreds = (int)(group / 100);
            int rest = (int)(group % 100);

            if (hundreds > 0) parts.Add(Hundreds[hundreds]);

            if (rest >= 10 && rest < 20)
            {
                parts.Add(Teens[rest - 10]);
            }
            else
            {
                int tens = rest / 10;
                int ones = rest % 10;
                if (tens > 0) parts.Add(Tens[tens]);
                if (ones > 0) parts.Add(OnesForms[ones][female ? 1 : 0]);
            }

            return string.Join(" ", parts);
        }

        // склонение по числу: 1 - one, 2-4 - few, 5-0/11-14 - many
        private static string PluralForm(long number, string one, string few, string many)
        {
            long n = Math.Abs(number) % 100;
            long n1 = n % 10;
            if (n >= 11 && n <= 14) return many;
            if (n1 == 1) return one;
            if (n1 >= 2 && n1 <= 4) return few;
            return many;
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
