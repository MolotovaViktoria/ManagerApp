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

                // 2. Инициализация кэша
                UpdateStatus("Инициализация кэша данных...", 10);
                await InitializeCacheWithProgress();

                // 3. Проверка загруженных данных
                UpdateStatus("Проверка данных...", 95);
                await CheckCacheData();

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

        private async Task InitializeCacheWithProgress()
        {
            try
            {
                // Запускаем инициализацию кэша
                var cacheTask = BitrixCache.InitializeAsync();

                // Запускаем анимацию прогресса
                StartProgressAnimation(10, 80, 30000); // 30 секунд на загрузку

                // Ждем завершения инициализации кэша
                await cacheTask;

                // Останавливаем анимацию
                StopProgressAnimation();

                // Устанавливаем точное значение
                progressBar.Value = 80;
            }
            catch (Exception ex)
            {
                StopProgressAnimation();
                throw new Exception($"Ошибка инициализации кэша: {ex.Message}", ex);
            }
        }

        private async Task CheckCacheData()
        {
            try
            {
                // Проверяем, загружены ли данные
                if (!BitrixCache.IsCacheReady())
                {
                    UpdateStatus("Повторная загрузка данных...", 85);
                    await BitrixCache.RefreshCacheAsync();
                }

                // Получаем статистику
                var stats = BitrixCache.GetCacheStats();
                Console.WriteLine($"[SplashScreen] Статистика кэша: {stats}");

                // Плавное завершение прогресса
                await SmoothProgressTo(95, 1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SplashScreen] Ошибка проверки данных: {ex.Message}");
                // Продолжаем работу даже если проверка не удалась
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

        private async Task SmoothProgressTo(int targetValue, int durationMilliseconds)
        {
            if (progressBar.Value >= targetValue)
                return;

            double startValue = progressBar.Value;
            double steps = 20;
            double delay = durationMilliseconds / steps;
            double increment = (targetValue - startValue) / steps;

            for (int i = 0; i < steps; i++)
            {
                if (!_isLoading) break;

                progressBar.Value += increment;
                await Task.Delay((int)delay);
            }

            progressBar.Value = targetValue;
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