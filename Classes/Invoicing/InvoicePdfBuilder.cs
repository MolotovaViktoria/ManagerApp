using System;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace ManagerApp.Classes.Invoicing
{
    // Строит счёт на оплату (.pdf) - гарантированно открывается на любом ПК (Windows/Edge встроенно
    // показывает PDF), даже если на компьютере нет Word/Excel. Использует те же данные, что и docx.
    public static class InvoicePdfBuilder
    {
        public static byte[] Build(InvoiceData data, CompanyRequisites seller)
        {
            var baseFont = ResolveCyrillicFont();
            var fontNormal = new Font(baseFont, 9);
            var fontBold = new Font(baseFont, 9, Font.BOLD);
            var fontTitle = new Font(baseFont, 14, Font.BOLD);
            var fontSmall = new Font(baseFont, 8, Font.ITALIC);

            using (var ms = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 30, 30, 30, 30);
                PdfWriter.GetInstance(document, ms);
                document.Open();

                if (seller.ShowValidityNotice)
                {
                    document.Add(new Paragraph(
                        $"Внимание! Счет действителен до {data.Date.AddDays(5):dd.MM.yyyy}. Оплата данного счета означает согласие с условиями поставки товара.",
                        fontSmall));
                    document.Add(new Paragraph(" ", fontSmall));
                }

                document.Add(BuildPaymentOrderTable(seller, fontNormal, fontBold));
                document.Add(new Paragraph(" ", fontNormal));

                document.Add(new Paragraph($"Счет на оплату № {data.Number} от {data.Date:dd.MM.yyyy} г.", fontTitle));
                document.Add(new Paragraph(" ", fontNormal));

                document.Add(new Paragraph(
                    $"Поставщик: {seller.Title}, ИНН {seller.Inn}, КПП {seller.Kpp}, {seller.Address}" +
                    (string.IsNullOrEmpty(seller.Phone) ? "" : $", тел.: {seller.Phone}"), fontNormal));
                document.Add(new Paragraph(
                    $"Покупатель: {data.BuyerTitle}, ИНН {data.BuyerInn}, КПП {data.BuyerKpp}, {data.BuyerAddress}" +
                    (string.IsNullOrEmpty(data.BuyerPhone) ? "" : $", тел.: {data.BuyerPhone}"), fontNormal));

                if (!string.IsNullOrWhiteSpace(data.OrderTopic))
                    document.Add(new Paragraph($"Основание: {data.OrderTopic}", fontNormal));

                document.Add(new Paragraph(" ", fontNormal));
                document.Add(BuildItemsTable(data, fontNormal, fontBold));
                document.Add(new Paragraph(" ", fontNormal));

                document.Add(new Paragraph($"Итого: {data.TotalWithoutVat:N2} ₽", fontBold));
                document.Add(new Paragraph($"В т.ч. НДС 20%: {data.Vat:N2} ₽", fontNormal));
                document.Add(new Paragraph($"Итого с НДС: {data.TotalWithVat:N2} ₽", fontBold));
                document.Add(new Paragraph($"Всего наименований {data.Items.Count}, на сумму {data.TotalWithVat:N2} ₽", fontNormal));
                document.Add(new Paragraph(NumberToWordsRu.ConvertRubles(data.TotalWithVat), fontSmall));
                document.Add(new Paragraph(" ", fontNormal));

                if (seller.ShowNumberedTerms)
                {
                    document.Add(new Paragraph("1. Поставщик обязуется передать Покупателю, а Покупатель обязуется принять и оплатить товары, указанные в настоящей спецификации.", fontSmall));
                    document.Add(new Paragraph($"2. Срок поставки {(data.DeliveryDays.HasValue ? data.DeliveryDays.Value.ToString() : "-")} календарных дней с момента поступления предоплаты", fontSmall));
                    document.Add(new Paragraph($"3. Условия оплаты: {data.PaymentTerms}", fontSmall));
                    document.Add(new Paragraph($"4. Условия доставки: {data.DeliveryTerms}", fontSmall));
                    document.Add(new Paragraph(" ", fontNormal));
                }

                document.Add(new Paragraph("Поставщик:", fontNormal));
                document.Add(new Paragraph($"Директор {seller.Director}    _______________ /подпись/", fontNormal));
                if (!string.IsNullOrWhiteSpace(seller.Accountant))
                    document.Add(new Paragraph($"Бухгалтер {seller.Accountant}    _______________ /подпись/", fontNormal));
                if (!string.IsNullOrWhiteSpace(seller.Manager))
                    document.Add(new Paragraph($"Менеджер {seller.Manager}    _______________ /подпись/", fontNormal));

                document.Close();
                return ms.ToArray();
            }
        }

        private static PdfPTable BuildPaymentOrderTable(CompanyRequisites seller, Font normal, Font bold)
        {
            var table = new PdfPTable(2) { WidthPercentage = 100 };
            table.SetWidths(new float[] { 1f, 2f });

            AddRow(table, "Банк получателя", seller.HasBankDetails ? seller.BankName : "—", bold, normal);
            AddRow(table, "БИК", seller.HasBankDetails ? seller.Bik : "—", bold, normal);
            AddRow(table, "Корр. счёт", seller.HasBankDetails ? seller.CorrAccount : "—", bold, normal);
            AddRow(table, "Расчётный счёт", seller.HasBankDetails ? seller.SettlementAccount : "—", bold, normal);
            AddRow(table, "Получатель", $"{seller.Title}, ИНН {seller.Inn}, КПП {seller.Kpp}", bold, normal);

            return table;
        }

        private static PdfPTable BuildItemsTable(InvoiceData data, Font normal, Font bold)
        {
            var table = new PdfPTable(6) { WidthPercentage = 100 };
            table.SetWidths(new float[] { 0.4f, 3f, 0.7f, 0.6f, 0.8f, 0.9f });

            foreach (var h in new[] { "№", "Товары (работы, услуги)", "Кол-во", "Ед.", "Цена", "Сумма" })
                table.AddCell(new PdfPCell(new Phrase(h, bold)));

            int n = 1;
            foreach (var item in data.Items)
            {
                table.AddCell(new PdfPCell(new Phrase(n.ToString(), normal)));
                table.AddCell(new PdfPCell(new Phrase(item.Name, normal)));
                table.AddCell(new PdfPCell(new Phrase(item.Quantity.ToString("0.###"), normal)));
                table.AddCell(new PdfPCell(new Phrase(item.Unit, normal)));
                table.AddCell(new PdfPCell(new Phrase(item.Price.ToString("N2"), normal)));
                table.AddCell(new PdfPCell(new Phrase(item.Sum.ToString("N2"), normal)));
                n++;
            }

            return table;
        }

        private static void AddRow(PdfPTable table, string label, string value, Font bold, Font normal)
        {
            table.AddCell(new PdfPCell(new Phrase(label, bold)));
            table.AddCell(new PdfPCell(new Phrase(value, normal)));
        }

        private static BaseFont ResolveCyrillicFont()
        {
            string[] candidates =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts) + "\\arial.ttf",
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts) + "\\calibri.ttf",
                "C:\\Windows\\Fonts\\arial.ttf",
                "C:\\Windows\\Fonts\\calibri.ttf"
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return BaseFont.CreateFont(path, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    }
                    catch
                    {
                        // пробуем следующий шрифт
                    }
                }
            }

            // крайний случай - без кириллицы, но хотя бы не падаем
            return BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
        }
    }
}
