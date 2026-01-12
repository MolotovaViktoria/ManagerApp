using ManagerApp.Data.GetInfo;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ManagerApp.Pages
{
    public partial class SplashScreen : Window
    {
        private DispatcherTimer _progressTimer;
        private bool _isLoading = false;

        public SplashScreen()
        {
            InitializeComponent();
            Loaded += async (s, e) => await InitializeApp();
        }

        private async Task InitializeApp()
        {
            try
            {
                _isLoading = true;

                // 1. Начальная инициализация
                UpdateStatus("Подготовка приложения...", 0);
                await Task.Delay(500);

                // 2. Загрузка кеша
                UpdateStatus("Загрузка кеша данных...", 20);

                // Проверяем, есть ли файловый кеш
                bool cacheExists = CacheFileManager.CacheExists();

                if (cacheExists)
                {
                    UpdateStatus("Загрузка из локального кеша...", 30);

                    // Загружаем из файлового кеша
                    var cacheInfo = await CacheFileManager.LoadCacheInfo();
                    if (cacheInfo != null && !CacheFileManager.ShouldUpdateCache(cacheInfo.LastCacheUpdate))
                    {
                        // Используем существующий кеш
                        UpdateStatus($"Кеш загружен ({cacheInfo.ProductsCount} товаров)...", 70);
                        await Task.Delay(1000);
                    }
                    else
                    {
                        // Кеш устарел, обновляем в фоне
                        UpdateStatus("Обновление кеша...", 40);
                        _ = BackgroundUpdateCacheAsync();
                    }
                }
                else
                {
                    // Кеша нет, загружаем первый раз
                    UpdateStatus("Первоначальная загрузка данных...", 30);
                    await BitrixCache.InitializeAsync();
                }

                // 3. Проверка данных
                UpdateStatus("Проверка данных...", 95);
                await Task.Delay(500);

                // 4. Завершение
                UpdateStatus("Запуск приложения...", 100);
                await Task.Delay(800);

                // 5. Открытие основного окна
                OpenMainWindow();
            }
            catch (Exception ex)
            {
                HandleInitializationError(ex);
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Фоновое обновление кеша
        private async Task BackgroundUpdateCacheAsync()
        {
            try
            {
                Console.WriteLine("[SplashScreen] Фоновое обновление кеша...");

                // Обновляем кеш
                await BitrixCache.InitializeAsync();

                Console.WriteLine("[SplashScreen] Фоновое обновление кеша завершено");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SplashScreen] Ошибка фонового обновления кеша: {ex.Message}");
            }
        }

        private void StartProgressAnimation(int startValue, int endValue, int totalMilliseconds)
        {
            _progressTimer = new DispatcherTimer();
            _progressTimer.Interval = TimeSpan.FromMilliseconds(100);

            int steps = (endValue - startValue);
            if (steps <= 0) return;

            double increment = (double)steps / (totalMilliseconds / 100);
            double currentValue = startValue;

            _progressTimer.Tick += (s, e) =>
            {
                if (!_isLoading || currentValue >= endValue)
                {
                    StopProgressAnimation();
                    return;
                }

                currentValue += increment;
                if (currentValue > endValue)
                    currentValue = endValue;

                progressBar.Value = currentValue;
                txtStatus.Text = $"Загрузка данных... {(int)currentValue}%";
            };

            _progressTimer.Start();
        }

        private void StopProgressAnimation()
        {
            if (_progressTimer != null)
            {
                _progressTimer.Stop();
                _progressTimer = null;
            }
        }

        private void UpdateStatus(string status, int progress)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = status;
                progressBar.Value = progress;
            });
        }

        private void OpenMainWindow()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var mainWindow = new HomeWindows();
                    mainWindow.Show();
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка открытия основного окна: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
            });
        }

        private void HandleInitializationError(Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                string errorMessage = $"Не удалось инициализировать приложение:\n\n{ex.Message}";

                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nДополнительная информация:\n{ex.InnerException.Message}";
                }

                var result = MessageBox.Show(errorMessage + "\n\nПопробовать загрузить данные снова?",
                    "Ошибка инициализации",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                if (result == MessageBoxResult.Yes)
                {
                    // Пробуем снова
                    _isLoading = false;
                    _ = InitializeApp();
                }
                else
                {
                    Application.Current.Shutdown();
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            StopProgressAnimation();
            base.OnClosed(e);
        }
    }
}