using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Classes.Setting
{
    public static class IDSetting
    {
        private const string EmployeeIdFileName = "setting_id_sotrudmik.txt";

        // Получить ID выбранного сотрудника
        public static int GetSelectedEmployeeId()
        {
            try
            {
                if (File.Exists(EmployeeIdFileName))
                {
                    var content = File.ReadAllText(EmployeeIdFileName);
                    if (int.TryParse(content, out int employeeId))
                    {
                        return employeeId;
                    }
                }
            }
            catch (Exception ex)
            {
                // Логирование ошибки при необходимости
                Console.WriteLine($"Ошибка при чтении ID сотрудника: {ex.Message}");
            }

            return 0; // Значение по умолчанию, если файл не найден или произошла ошибка
        }

        // Сохранить ID выбранного сотрудника
        public static void SaveSelectedEmployeeId(int employeeId)
        {
            try
            {
                File.WriteAllText(EmployeeIdFileName, employeeId.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка сохранения ID сотрудника: {ex.Message}", ex);
            }
        }

        // Проверить, существует ли сохраненный ID
        public static bool HasSelectedEmployeeId()
        {
            try
            {
                return File.Exists(EmployeeIdFileName) &&
                       !string.IsNullOrWhiteSpace(File.ReadAllText(EmployeeIdFileName));
            }
            catch
            {
                return false;
            }
        }

        // Удалить сохраненный ID (сбросить настройку)
        public static void ClearSelectedEmployeeId()
        {
            try
            {
                if (File.Exists(EmployeeIdFileName))
                {
                    File.Delete(EmployeeIdFileName);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка удаления ID сотрудника: {ex.Message}", ex);
            }
        }

        // Получить ID или значение по умолчанию
        public static int GetSelectedEmployeeIdOrDefault(int defaultValue = 0)
        {
            var id = GetSelectedEmployeeId();
            return id > 0 ? id : defaultValue;
        }
    }
}