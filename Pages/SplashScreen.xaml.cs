using ManagerApp.Data.GetInfo;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace ManagerApp.Pages
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Loaded += async (s, e) => await InitializeApp();
        }

        private async Task InitializeApp()
        {
            var steps = new[]
            {
                ("Инициализация кеша...", 10),
                ("Загрузка категорий...", 20),
                ("Загрузка товаров...", 80),
                ("Завершение...", 100)
            };

            foreach (var (status, progress) in steps)
            {
                txtStatus.Text = status;

                // Для этапа товаров НЕ вызываем SmoothProgressTo, чтобы не прыгал прогресс
                if (!status.Contains("товаров"))
                {
                    await SmoothProgressTo(progress);
                }

                // Выполняем загрузку данных
                if (status.Contains("кеша"))
                {
                    await BitrixCache.InitializeAsync();
                }
                else if (status.Contains("товаров"))
                {
                    // Загружаем товары с прогрессом в реальном времени
                    await LoadProductsWithRealProgress();
                }
                else
                {
                    await Task.Delay(600);
                }
            }

            var mainWindow = new HomeWindows();
            mainWindow.Show();
            this.Close();
        }

        private async Task LoadProductsWithRealProgress()
        {
            try
            {
                // Задачи загрузки обеих категорий
                var loadTask691 = BitrixCache.GetAllProductsSimple();
                //var loadTask692 = BitrixCache.GetProductsByCategory(692);
                //var loadTask685 = BitrixCache.GetProductsByCategory(685);

                // Задача анимации прогресса
                var progressTask = AnimateProgressDuringLoad();

                // Ждём, пока обе загрузки завершатся
                await Task.WhenAll(loadTask691, progressTask);

                // Устанавливаем точное значение 80%
                progressBar.Value = 80;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки: {ex.Message}");
            }
        }


        private async Task AnimateProgressDuringLoad()
        {
            int startProgress = 20; // Начинаем с текущего значения (20%)
            int endProgress = 80;
            int totalSteps = endProgress - startProgress;

            // 40 секунд / 60 шагов = ~667ms на 1%
            int delayPerPercent = 667; // 40 секунд на 60%

            for (int i = 0; i <= totalSteps; i++)
            {
                int currentProgress = startProgress + i;
                progressBar.Value = currentProgress;
                txtStatus.Text = $"Загрузка товаров... {currentProgress}%";
                await Task.Delay(delayPerPercent);
            }
        }

        private async Task SmoothProgressTo(int targetValue)
        {
            if (progressBar.Value >= targetValue) return;

            double duration = 1000;
            double steps = 20;
            double delay = duration / steps;
            double startValue = progressBar.Value;
            double increment = (targetValue - startValue) / steps;

            for (int i = 0; i < steps; i++)
            {
                progressBar.Value += increment;
                await Task.Delay((int)delay);
                await Task.Yield();
            }

            progressBar.Value = targetValue;
        }
    }
}