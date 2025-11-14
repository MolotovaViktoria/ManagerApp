using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.Office.Interop.Word;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(filePath, false))
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

        private void ProcessElement(DocumentFormat.OpenXml.OpenXmlElement element, StringBuilder result)
        {
            foreach (var child in element.ChildElements)
            {
                if (child is DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
                {
                    var text = GetParagraphText(paragraph);
                    if (!string.IsNullOrEmpty(text))
                    {
                        result.AppendLine(text);
                    }
                }
                else if (child is DocumentFormat.OpenXml.Wordprocessing.Table table)
                {
                    ProcessTable(table, result);
                }
                else if (child is DocumentFormat.OpenXml.Wordprocessing.SdtBlock sdtBlock) // Content control
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

        private void ProcessTable(DocumentFormat.OpenXml.Wordprocessing.Table table, StringBuilder result)
        {
            foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
            {
                var rowText = new StringBuilder();
                foreach (var cell in row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
                {
                    var cellText = new StringBuilder();
                    ProcessElement(cell, cellText);
                    rowText.Append(cellText.ToString().Trim() + "\t");
                }
                result.AppendLine(rowText.ToString().Trim());
            }
        }

        private string GetParagraphText(DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
        {
            var text = new StringBuilder();

            foreach (var run in paragraph.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>())
            {
                foreach (var textElement in run.Elements<DocumentFormat.OpenXml.Wordprocessing.Text>())
                {
                    text.Append(textElement.Text);
                }

                // Обрабатываем разрывы строк внутри Run
                foreach (var breakElement in run.Elements<DocumentFormat.OpenXml.Wordprocessing.Break>())
                {
                    text.AppendLine();
                }

                // Обрабатываем табуляции
                foreach (var tab in run.Elements<DocumentFormat.OpenXml.Wordprocessing.TabChar>())
                {
                    text.Append("\t");
                }
            }

            return text.ToString();
        }
        public string ReadPdfFile(string filePath)
        {
            List<string> allLines = new List<string>();

            using (PdfReader reader = new PdfReader(filePath))
            {
                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    string pageText = PdfTextExtractor.GetTextFromPage(reader, i);

                    // Умное разделение с сохранением структуры
                    var pageLines = SplitPdfTextIntoLines(pageText);
                    allLines.AddRange(pageLines);

                    // Можно добавить маркер страницы
                    allLines.Add($"=== Страница {i} ===");
                }
            }

            return string.Join(Environment.NewLine, allLines);
        }

        private List<string> SplitPdfTextIntoLines(string pageText)
        {
            var lines = new List<string>();

            // Разбиваем по символам новой строки
            var rawLines = pageText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in rawLines)
            {
                string trimmedLine = rawLine.Trim();

                // Пропускаем пустые строки и слишком короткие (если нужно)
                if (!string.IsNullOrEmpty(trimmedLine) && trimmedLine.Length > 1)
                {
                    lines.Add(trimmedLine);
                }
            }

            return lines;
        }



        public string ReadExcelFile(string filePath)
        {
            List<string> allLines = new List<string>();

            // Всегда используем ACE провайдер
            string connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={filePath};Extended Properties='Excel 12.0 Xml;HDR=NO;IMEX=1;'";

            try
            {
                using (OleDbConnection connection = new OleDbConnection(connectionString))
                {
                    connection.Open();

                    // Получаем список листов
                    System.Data.DataTable sheets = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);

                    if (sheets.Rows.Count == 0)
                        return "В файле нет листов";

                    // Обрабатываем все листы
                    foreach (System.Data.DataRow sheet in sheets.Rows)
                    {
                        string sheetName = sheet["TABLE_NAME"].ToString();


                        try
                        {
                            var sheetLines = ReadSheetData(connection, sheetName);
                            allLines.AddRange(sheetLines);
                        }
                        catch (Exception ex)
                        {
                            allLines.Add($"Ошибка чтения листа: {ex.Message}");
                        }

                        allLines.Add(""); // Пустая строка между листами
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Ошибка чтения файла: {ex.Message}";
            }

            return string.Join(Environment.NewLine, allLines);
        }

        private string CleanSheetName(string sheetName)
        {
            return sheetName.Replace("$", "").Replace("'", "").Trim();
        }

        private List<string> ReadSheetData(OleDbConnection connection, string sheetName)
        {
            var lines = new List<string>();

            using (OleDbCommand command = new OleDbCommand($"SELECT * FROM [{sheetName}]", connection))
            using (OleDbDataReader reader = command.ExecuteReader())
            {
                int rowNumber = 1;

                while (reader.Read())
                {


                    bool hasData = false;
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string value = reader[i].ToString().Trim();
                        if (!string.IsNullOrEmpty(value))
                        {
                            lines.Add($"{value}");
                            hasData = true;
                        }
                    }

                    // Если в строке нет данных, добавляем отметку
                    if (!hasData)
                    {

                    }

                    lines.Add(""); // Пустая строка между строками
                    rowNumber++;
                }

                if (rowNumber == 1)
                {

                }
            }

            return lines;
        }


    }


}