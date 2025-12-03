using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
//using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;

namespace ManagerApp.Classes.Read
{
    public class ReadRequst
    {
        public string ReadFileAll(string filePath)
        {
            string extension = System.IO.Path.GetExtension(filePath);

            if (FormatLists.WordFormatList.Contains(extension))
            {
                return ReadWordFile(filePath);
            }
            else if (FormatLists.PdfFormatList.Contains(extension))
            {
                return ReadPdfFile(filePath);
            }
            else if (FormatLists.ExcelFormatList.Contains(extension))
            {
                return ReadExcelFile(filePath);
            }
            else
            {
                return "Ошибка формата";
            }
        }

        public string ReadWordFile(string filePath)
        {
            using (var doc = WordprocessingDocument.Open(filePath, false))
            {
                var body = doc.MainDocumentPart.Document.Body;
                var result = new StringBuilder();

                // Обрабатываем основное тело документа
                ProcessElement(body, result);

                // Обрабатываем верхние колонтитулы
                foreach (var headerPart in doc.MainDocumentPart.HeaderParts)
                {
                    ProcessElement(headerPart.Header, result);
                }

                // Обрабатываем нижние колонтитулы
                foreach (var footerPart in doc.MainDocumentPart.FooterParts)
                {
                    ProcessElement(footerPart.Footer, result);
                }

                return result.ToString();
            }
        }

        private void ProcessTable(Table table, StringBuilder result)
        {
            foreach (var row in table.Elements<TableRow>())
            {
                var rowText = new StringBuilder();
                foreach (var cell in row.Elements<TableCell>())
                {
                    var cellText = new StringBuilder();
                    ProcessElement(cell, cellText);
                    rowText.Append(cellText.ToString().Trim() + "\t");
                }
                result.AppendLine(rowText.ToString().Trim());
            }
        }

        private void ProcessElement(OpenXmlElement element, StringBuilder result)
        {
            foreach (var child in element.ChildElements)
            {
                if (child is Paragraph paragraph)
                {
                    var text = GetParagraphText(paragraph);
                    if (!string.IsNullOrEmpty(text))
                    {
                        result.AppendLine(text);
                    }
                }
                else if (child is Table table)
                {
                    ProcessTable(table, result);
                }
                else if (child is SdtBlock sdtBlock) // Content control
                {
                    ProcessElement(sdtBlock, result);
                }
                else if (child is DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline inline)
                {
                    // Обработка inline элементов (могут содержать текст)
                    ProcessElement(inline, result);
                }
                else if (child is DocumentFormat.OpenXml.Vml.TextBox vmlTextBox)
                {
                    // Обработка VML text boxes (устаревший формат)
                    ProcessElement(vmlTextBox, result);
                }
                else
                {
                    // Рекурсивно обрабатываем вложенные элементы
                    ProcessElement(child, result);
                }
            }
        }

        private string GetParagraphText(Paragraph paragraph)
        {
            var text = new StringBuilder();

            foreach (var run in paragraph.Elements<Run>())
            {
                foreach (var textElement in run.Elements<Text>())
                {
                    text.Append(textElement.Text);
                }

                // Обрабатываем разрывы строк внутри Run
                foreach (var breakElement in run.Elements<Break>())
                {
                    text.AppendLine();
                }

                // Обрабатываем табуляции
                foreach (var tab in run.Elements<TabChar>())
                {
                    text.Append("\t");
                }
            }

            return text.ToString();
        }

        public string ReadPdfFile(string filePath)
        {
            StringBuilder text = new StringBuilder();

            using (PdfReader reader = new PdfReader(filePath))
            {
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    text.Append(PdfTextExtractor.GetTextFromPage(reader, i));
                }
            }

            return text.ToString();
        }

        public string ReadExcelFile(string filePath)
        {
            StringBuilder result = new StringBuilder();

            string connectionString = filePath.EndsWith(".xlsx")
                ? $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={filePath};Extended Properties='Excel 12.0 Xml;HDR=YES;'"
                : $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={filePath};Extended Properties='Excel 8.0;HDR=YES;'";

            using (OleDbConnection connection = new OleDbConnection(connectionString))
            {
                connection.Open();

                // Получаем список листов
                System.Data.DataTable sheets = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);

                if (sheets.Rows.Count == 0)
                    return "В файле нет листов";

                string firstSheet = sheets.Rows[0]["TABLE_NAME"].ToString();

                // Читаем данные с первого листа
                using (OleDbCommand command = new OleDbCommand($"SELECT * FROM [{firstSheet}]", connection))
                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            result.Append(reader[i].ToString() + "\t");
                        }
                        result.AppendLine();
                    }
                }
            }

            return result.ToString();
        }
    }
}