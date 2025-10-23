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
            using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
            {
                Body body = doc.MainDocumentPart.Document.Body;
                return body.InnerText;
            }
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
