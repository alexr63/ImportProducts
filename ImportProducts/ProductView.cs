using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace ImportProducts
{
    public class ProductView
    {
        public string Country { get; set; }
        public string County { get; set; }
        public string City { get; set; }
        public string ProductNumber { get; set; }
        public string Name { get; set; }
        public string Image { get; set; }
        public XElement Images { get; set; }
        public string UnitCost { get; set; }
        public string Description { get; set; }
        public string DescriptionHTML { get; set; }
        public string URL { get; set; }
        public string Star { get; set; }
        public string CustomerRating { get; set; }
        public string Rooms { get; set; }
        public string Address { get; set; }
        public string PostCode { get; set; }
        public string Telephone { get; set; }
        public string CurrencyCode { get; set; }
        public string Lat { get; set; }
        public string Long { get; set; }
    }
}
