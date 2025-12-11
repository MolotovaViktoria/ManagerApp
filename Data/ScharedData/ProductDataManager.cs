using ManagerApp.Pages;
using System.Collections.Generic;

namespace ManagerApp.Data.ScharedData
{
    public static class ProductDataManager
    {
        private static Dictionary<string, BitrixProductViewModel> _selectedProducts = new Dictionary<string, BitrixProductViewModel>();
        private static string _currentOriginalProduct;

        public static void SetOriginalProduct(string originalProduct)
        {
            _currentOriginalProduct = originalProduct;
        }

        public static void SetSelectedProduct(BitrixProductViewModel selectedProduct)
        {
            if (!string.IsNullOrEmpty(_currentOriginalProduct))
            {
                if (!_selectedProducts.ContainsKey(_currentOriginalProduct))
                {
                    _selectedProducts.Add(_currentOriginalProduct, selectedProduct);
                }
                else
                {
                    _selectedProducts[_currentOriginalProduct] = selectedProduct;
                }
            }
        }

        public static BitrixProductViewModel GetSelectedProduct(string originalProduct)
        {
            return _selectedProducts.ContainsKey(originalProduct) ? _selectedProducts[originalProduct] : null;
        }

        public static void Clear()
        {
            _selectedProducts.Clear();
            _currentOriginalProduct = null;
        }
    }
}