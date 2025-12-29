using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ManagerApp.Classes.Read.ReadPicture
{
    public static class OCRSettings
    {
        public static string TesseractDataPath { get; set; }

        public static void SetPath(string path)
        {
            TesseractDataPath = path;
            SavePathToSettings();
        }

        public static void LoadPathFromSettings()
        {
            try
            {
                string settingsFile = "settings.txt";
                if (File.Exists(settingsFile))
                {
                    var lines = File.ReadAllLines(settingsFile);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("OCRPATH="))
                        {
                            TesseractDataPath = line.Substring(8);
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private static void SavePathToSettings()
        {
            try
            {
                string settingsFile = "settings.txt";
                List<string> lines = new List<string>();

                if (File.Exists(settingsFile))
                {
                    lines = File.ReadAllLines(settingsFile).ToList();
                    lines.RemoveAll(line => line.StartsWith("OCRPATH="));
                }

                lines.Add($"OCRPATH={TesseractDataPath}");
                File.WriteAllLines(settingsFile, lines);
            }
            catch { }
        }

        // Метод для получения пути к файлу
        public static string GetRussianFile()
        {
            if (!string.IsNullOrEmpty(TesseractDataPath))
            {
                return Path.Combine(TesseractDataPath, "rus.traineddata");
            }
            return "rus.traineddata";
        }
    }
}