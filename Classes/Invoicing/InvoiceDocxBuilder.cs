using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ManagerApp.Classes.Invoicing
{
    // Строит счёт на оплату (.docx) полностью локально через OpenXML - без Bitrix и без Word/Interop.
    public static class InvoiceDocxBuilder
    {
        public static byte[] Build(InvoiceData data, CompanyRequisites seller)
        {
            using (var stream = new MemoryStream())
            {
                using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
                {
                    var mainPart = doc.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    var body = mainPart.Document.AppendChild(new Body());

                    body.AppendChild(BuildPageMargins());

                    if (seller.ShowValidityNotice)
                    {
                        body.AppendChild(Para($"Внимание! Счет действителен до {data.Date.AddDays(5):dd.MM.yyyy}. Оплата данного счета означает согласие с условиями поставки товара.",
                            italic: true, size: 16));
                    }

                    body.AppendChild(BuildPaymentOrderTable(seller));
                    body.AppendChild(Para(""));

                    body.AppendChild(Para($"Счет на оплату № {data.Number} от {data.Date:dd.MM.yyyy} г.", bold: true, size: 28));
                    body.AppendChild(Para(""));

                    body.AppendChild(Para(
                        $"Поставщик: {seller.Title}, ИНН {seller.Inn}, КПП {seller.Kpp}, {seller.Address}" +
                        (string.IsNullOrEmpty(seller.Phone) ? "" : $", тел.: {seller.Phone}"), size: 20));

                    body.AppendChild(Para(
                        $"Покупатель: {data.BuyerTitle}, ИНН {data.BuyerInn}, КПП {data.BuyerKpp}, {data.BuyerAddress}" +
                        (string.IsNullOrEmpty(data.BuyerPhone) ? "" : $", тел.: {data.BuyerPhone}"), size: 20));

                    if (!string.IsNullOrWhiteSpace(data.OrderTopic))
                        body.AppendChild(Para($"Основание: {data.OrderTopic}", size: 20));

                    body.AppendChild(Para(""));
                    body.AppendChild(BuildItemsTable(data));
                    body.AppendChild(Para(""));

                    body.AppendChild(Para($"Итого: {data.TotalWithoutVat:N2} ₽", bold: true, size: 20));
                    body.AppendChild(Para($"В т.ч. НДС 20%: {data.Vat:N2} ₽", size: 20));
                    body.AppendChild(Para($"Итого с НДС: {data.TotalWithVat:N2} ₽", bold: true, size: 20));
                    body.AppendChild(Para($"Всего наименований {data.Items.Count}, на сумму {data.TotalWithVat:N2} ₽", size: 20));
                    body.AppendChild(Para(NumberToWordsRu.ConvertRubles(data.TotalWithVat), italic: true, size: 20));
                    body.AppendChild(Para(""));

                    if (seller.ShowNumberedTerms)
                    {
                        body.AppendChild(Para("1. Поставщик обязуется передать Покупателю, а Покупатель обязуется принять и оплатить товары, указанные в настоящей спецификации.", size: 18));
                        body.AppendChild(Para($"2. Срок поставки {(data.DeliveryDays.HasValue ? data.DeliveryDays.Value.ToString() : "-")} календарных дней с момента поступления предоплаты", size: 18));
                        body.AppendChild(Para($"3. Условия оплаты: {data.PaymentTerms}", size: 18));
                        body.AppendChild(Para($"4. Условия доставки: {data.DeliveryTerms}", size: 18));
                        body.AppendChild(Para(""));
                    }

                    body.AppendChild(Para("Поставщик:", size: 20));
                    body.AppendChild(Para($"Директор {seller.Director}    _______________ /подпись/", size: 20));
                    if (!string.IsNullOrWhiteSpace(seller.Accountant))
                        body.AppendChild(Para($"Бухгалтер {seller.Accountant}    _______________ /подпись/", size: 20));
                    if (!string.IsNullOrWhiteSpace(seller.Manager))
                        body.AppendChild(Para($"Менеджер {seller.Manager}    _______________ /подпись/", size: 20));

                    body.AppendChild(new SectionProperties(new PageSize { Width = 11907, Height = 16840 }));
                }

                return stream.ToArray();
            }
        }

        private static PageMargin BuildPageMargins() => new PageMargin
        {
            Top = 700, Right = 700, Bottom = 700, Left = 900
        };

        private static Table BuildPaymentOrderTable(CompanyRequisites seller)
        {
            var table = new Table();
            table.AppendChild(new TableProperties(
                BuildAllBorders(),
                new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }));

            table.AppendChild(Row(Cell("Банк получателя", bold: false), Cell(seller.HasBankDetails ? seller.BankName : "—")));
            table.AppendChild(Row(Cell("БИК"), Cell(seller.HasBankDetails ? seller.Bik : "—")));
            table.AppendChild(Row(Cell("Корр. счёт"), Cell(seller.HasBankDetails ? seller.CorrAccount : "—")));
            table.AppendChild(Row(Cell("Расчётный счёт"), Cell(seller.HasBankDetails ? seller.SettlementAccount : "—")));
            table.AppendChild(Row(Cell("Получатель"), Cell($"{seller.Title}, ИНН {seller.Inn}, КПП {seller.Kpp}")));

            return table;
        }

        private static Table BuildItemsTable(InvoiceData data)
        {
            var table = new Table();
            table.AppendChild(new TableProperties(
                BuildAllBorders(),
                new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }));

            table.AppendChild(Row(
                Cell("№", bold: true), Cell("Товары (работы, услуги)", bold: true), Cell("Кол-во", bold: true),
                Cell("Ед.", bold: true), Cell("Цена", bold: true), Cell("Сумма", bold: true)));

            int n = 1;
            foreach (var item in data.Items)
            {
                table.AppendChild(Row(
                    Cell(n.ToString()), Cell(item.Name), Cell(item.Quantity.ToString("0.###")),
                    Cell(item.Unit), Cell(item.Price.ToString("N2")), Cell(item.Sum.ToString("N2"))));
                n++;
            }

            return table;
        }

        private static TableBorders BuildAllBorders() => new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 });

        private static TableRow Row(params TableCell[] cells)
        {
            var row = new TableRow();
            foreach (var c in cells) row.AppendChild(c);
            return row;
        }

        private static TableCell Cell(string text, bool bold = false)
        {
            var cell = new TableCell();
            cell.AppendChild(new TableCellProperties(new TableCellMargin(
                new TopMargin { Width = "60" }, new BottomMargin { Width = "60" },
                new TableCellLeftMargin { Width = 100 }, new TableCellRightMargin { Width = 100 })));
            cell.AppendChild(Para(text, bold: bold, size: 18));
            return cell;
        }

        private static Paragraph Para(string text, bool bold = false, bool italic = false, int size = 20)
        {
            var run = new Run(new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve });
            var props = new RunProperties { FontSize = new FontSize { Val = size.ToString() } };
            if (bold) props.Bold = new Bold();
            if (italic) props.Italic = new Italic();
            run.PrependChild(props);
            return new Paragraph(run);
        }
    }
}
