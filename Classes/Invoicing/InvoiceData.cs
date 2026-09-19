using System;
using System.Collections.Generic;
using System.Linq;

namespace ManagerApp.Classes.Invoicing
{
    public class InvoiceLineItem
    {
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public decimal Sum => Math.Round(Quantity * Price, 2);
    }

    public class InvoiceData
    {
        public string SellerCompanyCode { get; set; } // "SPK" or "NVR"
        public string Number { get; set; }
        public DateTime Date { get; set; }

        public string BuyerTitle { get; set; }
        public string BuyerInn { get; set; }
        public string BuyerKpp { get; set; }
        public string BuyerAddress { get; set; }
        public string BuyerPhone { get; set; }

        public string DeliveryTerms { get; set; }   // "Условия доставки"
        public int? DeliveryDays { get; set; }       // "Срок поставки"
        public string PaymentTerms { get; set; }     // "Условия оплаты"
        public string OrderTopic { get; set; }        // "Оплата по заказу клиента №"

        public List<InvoiceLineItem> Items { get; set; } = new List<InvoiceLineItem>();

        // Цены товаров указываются без НДС (как и раньше через Битрикс: taxIncluded = "N").
        public decimal TotalWithoutVat => Items.Sum(i => i.Sum);
        public decimal Vat => Math.Round(TotalWithoutVat * 0.20m, 2);
        public decimal TotalWithVat => TotalWithoutVat + Vat;
    }
}
