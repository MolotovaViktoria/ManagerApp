using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ManagerApp.Classes.Invoicing
{
    // Реквизиты одной из наших компаний (продавец в счёте).
    public class CompanyRequisites
    {
        public string Code { get; set; }           // "SPK" или "NVR"
        public string Title { get; set; }           // ООО "..."
        public string Inn { get; set; }
        public string Kpp { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }

        public string BankName { get; set; }
        public string Bik { get; set; }
        public string CorrAccount { get; set; }
        public string SettlementAccount { get; set; }

        public string Director { get; set; }
        public string Accountant { get; set; }
        public string Manager { get; set; }

        public string DeliveryTerms { get; set; }   // текст "Условия доставки" по умолчанию
        public bool ShowValidityNotice { get; set; } // "Внимание! Счет действителен до..."
        public bool ShowNumberedTerms { get; set; }  // блок из 4 пронумерованных пунктов

        public bool HasBankDetails =>
            !string.IsNullOrWhiteSpace(Bik) && !string.IsNullOrWhiteSpace(SettlementAccount);
    }

    public static class CompanyRequisitesProvider
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ManagerApp", "Cache", "company_requisites.json");

        private static Dictionary<string, CompanyRequisites> _cache;

        public static CompanyRequisites Get(string code)
        {
            EnsureLoaded();
            return _cache.TryGetValue(code, out var req) ? req : null;
        }

        public static List<CompanyRequisites> GetAll()
        {
            EnsureLoaded();
            return new List<CompanyRequisites>(_cache.Values);
        }

        // Позволяет обновить реквизиты (например, банковские данные НВР) без пересборки приложения.
        public static void Save(CompanyRequisites requisites)
        {
            EnsureLoaded();
            _cache[requisites.Code] = requisites;
            WriteFile();
        }

        private static void EnsureLoaded()
        {
            if (_cache != null) return;

            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath, System.Text.Encoding.UTF8);
                    var list = JsonConvert.DeserializeObject<List<CompanyRequisites>>(json);
                    if (list != null && list.Count > 0)
                    {
                        _cache = new Dictionary<string, CompanyRequisites>();
                        foreach (var r in list) _cache[r.Code] = r;
                        return;
                    }
                }
            }
            catch
            {
                // повреждённый файл - используем встроенные значения по умолчанию
            }

            _cache = DefaultRequisites();
            WriteFile();
        }

        private static void WriteFile()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonConvert.SerializeObject(new List<CompanyRequisites>(_cache.Values), Formatting.Indented);
                File.WriteAllText(FilePath, json, System.Text.Encoding.UTF8);
            }
            catch
            {
                // не критично - в следующий раз просто снова используем значения по умолчанию
            }
        }

        private static Dictionary<string, CompanyRequisites> DefaultRequisites()
        {
            var spk = new CompanyRequisites
            {
                Code = "SPK",
                Title = "ООО \"Системы промышленной комплектации\"",
                Inn = "6686170491",
                Kpp = "668601001",
                Address = "620135, Свердловская область, г Екатеринбург, ул Индустрии, д. 104, кв. 133",
                Phone = "+7 912 250-18-28",
                BankName = "УРАЛЬСКИЙ БАНК ПАО СБЕРБАНК г. Екатеринбург",
                Bik = "046577674",
                CorrAccount = "30101810500000000674",
                SettlementAccount = "40702810616750004459",
                Director = "Заболоцкая О. А.",
                Accountant = "",
                Manager = "Юлия Кагарманова",
                DeliveryTerms = "самовывоз со склада Поставщика г. Екатеринбург, ул. Мартовская, д.8: с 9 ч. 00 мин. до 18 ч. 00 мин. по местному времени в рабочие дни",
                ShowValidityNotice = true,
                ShowNumberedTerms = true
            };

            var nvr = new CompanyRequisites
            {
                Code = "NVR",
                Title = "ООО ТД МК «НВР»",
                Inn = "6659154839",
                Kpp = "665801001",
                Address = "ДОЛОРЕС ИБАРРУРИ УЛ, СТР. 2/1, ОФИС 113, ЕКАТЕРИНБУРГ, СВЕРДЛОВСКАЯ ОБЛАСТЬ, Россия, 620028",
                Phone = "",
                // TODO: банковские реквизиты ООО ТД МК «НВР» не были найдены ни в одном
                // из присланных файлов (шаблон/пример) - заполнить, когда будут получены.
                BankName = "",
                Bik = "",
                CorrAccount = "",
                SettlementAccount = "",
                Director = "",
                Accountant = "",
                Manager = "",
                DeliveryTerms = "",
                ShowValidityNotice = false,
                ShowNumberedTerms = false
            };

            return new Dictionary<string, CompanyRequisites> { ["SPK"] = spk, ["NVR"] = nvr };
        }
    }
}
