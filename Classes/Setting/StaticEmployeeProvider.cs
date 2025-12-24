using ManagerApp.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Classes.Setting
{
    public static class StaticEmployeeProvider
    {
        // Статичный список сотрудников (без администраторов)
        private static readonly List<BitrixUser> _staticEmployees = new List<BitrixUser>
        {
            // Исключены администраторы: Алексей Неустроев (id:9), Павел Коростелев (id:1)
            // и другие сотрудники из предоставленного списка
            new BitrixUser { id = 10, name = "Алена Юрьева", work_position = "Отдел снабжения", Initials = "АЮ" },
            new BitrixUser { id = 241, name = "Виктория Молотова", work_position = "ТД МК «НАВИГАТОР»", Initials = "ВМ" },
            new BitrixUser { id = 11, name = "Максим Ермаков", work_position = "Отдел продаж", Initials = "МЕ" },
            new BitrixUser { id = 13, name = "Юлия Кагарманова", work_position = "Отдел продаж", Initials = "ЮК" },
            new BitrixUser { id = 162, name = "Алена Мелехова", work_position = "Отдел продаж", Initials = "АМ" },
            new BitrixUser { id = 163, name = "Светлана Зыкова", work_position = "Бухгалтерия", Initials = "СЗ" },
            new BitrixUser { id = 166, name = "Юлия Кирилина", work_position = "Бухгалтерия", Initials = "ЮК" },
            new BitrixUser { id = 235, name = "Равиль Зарипов", work_position = "Отдел продаж", Initials = "РЗ" },
            new BitrixUser { id = 237, name = "Склад Кирилин", work_position = "Склад", Initials = "СК" }
        };

        // Получить всех статичных сотрудников
        public static List<BitrixUser> GetAllEmployees()
        {
            return _staticEmployees.OrderBy(e => e.name).ToList();
        }

        // Получить сотрудника по ID
        public static BitrixUser GetEmployeeById(int id)
        {
            return _staticEmployees.FirstOrDefault(e => e.id == id);
        }

        // Проверить, существует ли сотрудник с таким ID
        public static bool EmployeeExists(int id)
        {
            return _staticEmployees.Any(e => e.id == id);
        }

        // Получить список ID всех сотрудников
        public static List<int> GetAllEmployeeIds()
        {
            return _staticEmployees.Select(e => e.id).ToList();
        }
    }
}