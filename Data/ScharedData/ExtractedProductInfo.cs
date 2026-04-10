namespace ManagerApp.Data.ScharedData
{
    public class ExtractedProductInfo
    {
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string MeasureId { get; set; }
        public string MeasureSymbol { get; set; }
        public string MeasureName { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }  // ДОБАВЬТЕ ЭТО ПОЛЕ

        public ExtractedProductInfo()
        {
            Quantity = 1;
            MeasureSymbol = "шт";
            MeasureName = "Штука";
        }
    }
}