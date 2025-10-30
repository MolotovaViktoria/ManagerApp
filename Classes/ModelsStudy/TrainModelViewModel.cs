using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ManagerApp.Classes.ModelsStudy
{
    public class TrainModelViewModel : INotifyPropertyChanged
    {
        private MLParserRequst _parser;
        private Queue<string> _textsToClassify;
        private int _totalTexts;
        private int _processedTexts;

        public TrainModelViewModel(List<string> textsToClassify)
        {
            _parser = new MLParserRequst();
            _textsToClassify = new Queue<string>(textsToClassify);
            _totalTexts = textsToClassify.Count;
            _processedTexts = 0;

            // Инициализируем модель если она пустая
            if (string.IsNullOrEmpty(_parser.GetModelInfo()) || _parser.GetModelInfo().Contains("Примеров: 0"))
            {
                _parser.InitializeModel();
            }

            LoadNextText();

            MarkAsProductCommand = new RelayCommand(_ => MarkAsProduct());
            MarkAsNotProductCommand = new RelayCommand(_ => MarkAsNotProduct());
            SkipCommand = new RelayCommand(_ => Skip());

            UpdateStatus();
        }

        private string _currentText;
        public string CurrentText
        {
            get => _currentText;
            set
            {
                _currentText = value;
                OnPropertyChanged(nameof(CurrentText));
                ClassifyCurrentText();
            }
        }

        private string _classificationResult;
        public string ClassificationResult
        {
            get => _classificationResult;
            set
            {
                _classificationResult = value;
                OnPropertyChanged(nameof(ClassificationResult));
            }
        }

        private string _confidenceText;
        public string ConfidenceText
        {
            get => _confidenceText;
            set
            {
                _confidenceText = value;
                OnPropertyChanged(nameof(ConfidenceText));
            }
        }

        private string _modelInfo;
        public string ModelInfo
        {
            get => _modelInfo;
            set
            {
                _modelInfo = value;
                OnPropertyChanged(nameof(ModelInfo));
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        private double _progressPercentage;
        public double ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                _progressPercentage = value;
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        private string _progressText;
        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public ICommand MarkAsProductCommand { get; }
        public ICommand MarkAsNotProductCommand { get; }
        public ICommand SkipCommand { get; }

        private void ClassifyCurrentText()
        {
            if (string.IsNullOrWhiteSpace(CurrentText))
                return;

            var result = _parser.ClassifyText(CurrentText);

            if (result.IsProduct)
            {
                ClassificationResult = $"✅ Модель считает это ТОВАРОМ (Категория: {result.Category})";
            }
            else
            {
                ClassificationResult = $"❌ Модель считает это НЕ товаром";
            }

            ConfidenceText = $"Уверенность модели: {result.Confidence:P1}";
            ModelInfo = _parser.GetModelInfo();
        }

        private void MarkAsProduct()
        {
            if (!string.IsNullOrWhiteSpace(CurrentText))
            {
                _parser.TrainModel(CurrentText, true, "ELECTRICAL");
                SaveAndContinue("✅ Обучено как ТОВАР");
            }
        }

        private void MarkAsNotProduct()
        {
            if (!string.IsNullOrWhiteSpace(CurrentText))
            {
                _parser.TrainModel(CurrentText, false);
                SaveAndContinue("❌ Обучено как НЕ товар");
            }
        }

        private void Skip()
        {
            SaveAndContinue("⏭️ Пропущено");
        }

        private void SaveAndContinue(string message)
        {
            _processedTexts++;
            UpdateProgress();

            // Загружаем следующий текст
            if (_textsToClassify.Count > 0)
            {
                LoadNextText();
                StatusMessage = $"{message}. Осталось: {_textsToClassify.Count} текстов";
            }
            else
            {
                StatusMessage = "🎉 Обучение завершено! Все тексты обработаны.";
                CurrentText = "";
                ClassificationResult = "Обучение завершено";
                ConfidenceText = "Модель сохранена и улучшена";
                ModelInfo = _parser.GetModelInfo();
            }
        }

        private void LoadNextText()
        {
            if (_textsToClassify.Count > 0)
            {
                CurrentText = _textsToClassify.Dequeue();
            }
        }

        private void UpdateStatus()
        {
            StatusMessage = $"Осталось текстов для классификации: {_textsToClassify.Count}";
            ModelInfo = _parser.GetModelInfo();
        }

        private void UpdateProgress()
        {
            if (_totalTexts > 0)
            {
                ProgressPercentage = (_processedTexts / (double)_totalTexts) * 100;
                ProgressText = $"{_processedTexts}/{_totalTexts}";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Реализация RelayCommand для WPF
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}