using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;

namespace ManagerApp.Data.GetInfo
{
    public static class CacheFileManager
    {
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ManagerApp",
            "Cache");

        private static readonly string ProductsCacheFile = Path.Combine(CacheDirectory, "products_cache.json");
        private static readonly string MeasuresCacheFile = Path.Combine(CacheDirectory, "measures_cache.json");
        private static readonly string CacheInfoFile = Path.Combine(CacheDirectory, "cache_info.json");

        static CacheFileManager()
        {
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
        }

        // Сохранить продукты в файл
        public static async Task SaveProductsToFile<T>(T data)
        {
            await Task.Run(() =>
            {
                try
                {
                    string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(ProductsCacheFile, json, Encoding.UTF8);
                    Console.WriteLine($"[CacheFileManager] Продукты сохранены в файл: {ProductsCacheFile}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CacheFileManager] Ошибка сохранения продуктов: {ex.Message}");
                }
            });
        }

        // Загрузить продукты из файла
        public static async Task<T> LoadProductsFromFile<T>() where T : class
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(ProductsCacheFile))
                    {
                        Console.WriteLine($"[CacheFileManager] Файл кеша не найден: {ProductsCacheFile}");
                        return null;
                    }

                    string json = File.ReadAllText(ProductsCacheFile, Encoding.UTF8);
                    var data = JsonConvert.DeserializeObject<T>(json);

                    if (data != null)
                    {
                        Console.WriteLine($"[CacheFileManager] Продукты загружены из файла: {ProductsCacheFile}");
                    }

                    return data;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CacheFileManager] Ошибка загрузки продуктов: {ex.Message}");
                    return null;
                }
            });
        }

        // Сохранить единицы измерения в файл
        public static async Task SaveMeasuresToFile<T>(T data)
        {
            await Task.Run(() =>
            {
                try
                {
                    string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(MeasuresCacheFile, json, Encoding.UTF8);
                    Console.WriteLine($"[CacheFileManager] Единицы измерения сохранены в файл: {MeasuresCacheFile}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CacheFileManager] Ошибка сохранения единиц измерения: {ex.Message}");
                }
            });
        }

        // Загрузить единицы измерения из файла
        public static async Task<T> LoadMeasuresFromFile<T>() where T : class
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(MeasuresCacheFile))
                    {
                        Console.WriteLine($"[CacheFileManager] Файл кеша не найден: {MeasuresCacheFile}");
                        return null;
                    }

                    string json = File.ReadAllText(MeasuresCacheFile, Encoding.UTF8);
                    var data = JsonConvert.DeserializeObject<T>(json);

                    if (data != null)
                    {
                        Console.WriteLine($"[CacheFileManager] Единицы измерения загружены из файла: {MeasuresCacheFile}");
                    }

                    return data;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CacheFileManager] Ошибка загрузки единиц измерения: {ex.Message}");
                    return null;
                }
            });
        }

        // Сохранить информацию о кеше
        public static async Task SaveCacheInfo(ManagerApp.Data.StructureList.CacheInfo info)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Обновляем размер кеша перед сохранением
                    info.CacheSizeBytes = GetCacheSizeBytes();

                    string json = JsonConvert.SerializeObject(info, Formatting.Indented);
                    File.WriteAllText(CacheInfoFile, json, Encoding.UTF8);
                    Console.WriteLine($"[CacheFileManager] Информация о кеше сохранена");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CacheFileManager] Ошибка сохранения информации о кеше: {ex.Message}");
                }
            });
        }

        // Загрузить информацию о кеше
        public static async Task<ManagerApp.Data.StructureList.CacheInfo> LoadCacheInfo()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(CacheInfoFile))
                    {
                        Console.WriteLine($"[CacheFileManager] Файл информации о кеше не найден");
                        return new ManagerApp.Data.StructureList.CacheInfo
                        {
                            LastCacheUpdate = DateTime.MinValue,
                            CacheVersion = 1,
                            IsFirstRun = true
                        };
                    }

                    string json = File.ReadAllText(CacheInfoFile, Encoding.UTF8);
                    var info = JsonConvert.DeserializeObject<ManagerApp.Data.StructureList.CacheInfo>(json);

                    if (info != null)
                    {
                        Console.WriteLine($"[CacheFileManager] Информация о кеше загружена");
                    }

                    return info ?? new ManagerApp.Data.StructureList.CacheInfo { LastCacheUpdate = DateTime.MinValue };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CacheFileManager] Ошибка загрузки информации о кеше: {ex.Message}");
                    return new ManagerApp.Data.StructureList.CacheInfo { LastCacheUpdate = DateTime.MinValue };
                }
            });
        }

        // Проверить, нужно ли обновлять кеш (если прошло больше 24 часов)
        public static bool ShouldUpdateCache(DateTime lastUpdate)
        {
            return (DateTime.Now - lastUpdate) > TimeSpan.FromHours(24);
        }

        // Проверить существование кеша
        public static bool CacheExists()
        {
            return File.Exists(ProductsCacheFile) &&
                   File.Exists(MeasuresCacheFile) &&
                   File.Exists(CacheInfoFile);
        }

        // Получить размер кеша в байтах
        private static long GetCacheSizeBytes()
        {
            try
            {
                long totalSize = 0;

                if (File.Exists(ProductsCacheFile))
                    totalSize += new FileInfo(ProductsCacheFile).Length;

                if (File.Exists(MeasuresCacheFile))
                    totalSize += new FileInfo(MeasuresCacheFile).Length;

                if (File.Exists(CacheInfoFile))
                    totalSize += new FileInfo(CacheInfoFile).Length;

                return totalSize;
            }
            catch
            {
                return 0;
            }
        }
        public static void DebugCacheFiles()
        {
            try
            {
                Console.WriteLine($"[CacheFileManager] Проверка файлов кеша:");
                Console.WriteLine($"[CacheFileManager] Директория: {CacheDirectory}");

                if (Directory.Exists(CacheDirectory))
                {
                    var files = Directory.GetFiles(CacheDirectory);
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        Console.WriteLine($"[CacheFileManager] Файл: {fileInfo.Name}, Размер: {fileInfo.Length} байт, Дата: {fileInfo.LastWriteTime}");
                    }
                }
                else
                {
                    Console.WriteLine($"[CacheFileManager] Директория не существует");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CacheFileManager] Ошибка проверки файлов: {ex.Message}");
            }
        }
        // Получить размер кеша в мегабайтах
        public static double GetCacheSizeMB()
        {
            return Math.Round(GetCacheSizeBytes() / (1024.0 * 1024.0), 2);
        }

        // Очистить кеш
        public static void ClearCache()
        {
            try
            {
                if (File.Exists(ProductsCacheFile))
                    File.Delete(ProductsCacheFile);

                if (File.Exists(MeasuresCacheFile))
                    File.Delete(MeasuresCacheFile);

                if (File.Exists(CacheInfoFile))
                    File.Delete(CacheInfoFile);

                Console.WriteLine($"[CacheFileManager] Кеш очищен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CacheFileManager] Ошибка очистки кеша: {ex.Message}");
            }
        }

        // Получить путь к директории кеша
        public static string GetCacheDirectory()
        {
            return CacheDirectory;
        }
    }


}