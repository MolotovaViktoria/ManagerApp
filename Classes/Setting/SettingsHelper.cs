using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Classes.Setting
{
    public static class SettingsHelper
    {
        private const string SettingsFileName = "settings.txt";
        private const string DefaultVAT = "20";

        public static decimal VAT
        {
            get
            {
                try
                {
                    if (File.Exists(SettingsFileName))
                    {
                        var lines = File.ReadAllLines(SettingsFileName);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("VAT="))
                            {
                                var value = line.Substring(4);
                                if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var vat))
                                {
                                    return vat / 100m; // Возвращаем как коэффициент
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // В случае ошибки возвращаем значение по умолчанию
                }

                return 0.20m; // 20% по умолчанию
            }
        }

        public static string GetVATAsString()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    var lines = File.ReadAllLines(SettingsFileName);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("VAT="))
                        {
                            return line.Substring(4);
                        }
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем значение по умолчанию
            }

            return DefaultVAT;
        }

        // Добавленный метод для получения НДС как десятичного числа
        public static decimal GetVATAsDecimal()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    var lines = File.ReadAllLines(SettingsFileName);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("VAT="))
                        {
                            var value = line.Substring(4);
                            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var vat))
                            {
                                return vat;
                            }
                        }
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем значение по умолчанию
            }

            return decimal.Parse(DefaultVAT);
        }

        public static decimal CalculatePriceWithVAT(decimal priceWithoutVAT)
        {
            return priceWithoutVAT * (1 + VAT);
        }

        public static decimal CalculatePriceWithoutVAT(decimal priceWithVAT)
        {
            return priceWithVAT / (1 + VAT);
        }

        public static decimal GetVATAmount(decimal priceWithoutVAT)
        {
            return priceWithoutVAT * VAT;
        }
    }
}