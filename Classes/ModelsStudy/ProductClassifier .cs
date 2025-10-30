using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagerApp.Classes.ModelsStudy
{
    public class ML
    {
        public class ProductClassifier
        {
            private MLModel _model;
            private List<TrainingExample> _trainingData;

            public ProductClassifier()
            {
                _model = new MLModel();
                _trainingData = new List<TrainingExample>();
            }

            // Структура для обучающих данных
            public class TrainingExample
            {
                public string Text { get; set; }
                public string Category { get; set; }
                public bool IsProduct { get; set; }
            }

            // Простая ML модель
            public class MLModel
            {
                public Dictionary<string, Dictionary<string, int>> WordWeights { get; set; }
                public Dictionary<string, int> CategoryCounts { get; set; }

                public MLModel()
                {
                    WordWeights = new Dictionary<string, Dictionary<string, int>>();
                    CategoryCounts = new Dictionary<string, int>();
                }
            }
        }
    }
}
