using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using System.Xml.Schema;
using Ionic.Zip;
using LinqToExcel;
using SelectedHotelsModel;

namespace ImportProducts
{


    class ImportExcelHotels
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void DoImport(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            // Parse list input parameters
            BackgroundWorkParameters param = (BackgroundWorkParameters) e.Argument;
            string _URL = param.Url;
            int categoryId = param.CategoryId;
            int portalId = param.PortalId;
            int vendorId = param.VendorId;
            string countryFilter = param.CountryFilter;
            string cityFilter = param.CityFilter;
            int? stepImport = param.StepImport;
            int feedId = param.FeedId;

            var excel = new ExcelQueryFactory(_URL);
            var xlsHotels = from h in excel.Worksheet<HotelView>("Query")
                where h.Name != String.Empty
                select h;

            // Set step for backgroundWorker
            Form1.activeStep = "Import records..";
            bw.ReportProgress(0); // start new step of background process
            int productCount = xlsHotels.Count();
            try
            {
                int initialStep = 0;
                if (stepImport.HasValue)
                {
                    initialStep = stepImport.Value;
                }

                using (SelectedHotelsEntities db = new SelectedHotelsEntities())
                {
                    int i = 0;
                    foreach (HotelView hotelView in xlsHotels)
                    {
                        if (i < initialStep)
                        {
                            i++;
                            continue;
                        }

#if DEBUG
                        Console.WriteLine(i + " - " + hotelView.Name); // debug print
#endif

                        // create new product record
                        Hotel hotel =
                            db.Products.SingleOrDefault(
                                p => p.ProductTypeId == (int)Enums.ProductTypeEnum.Hotels && p.Categories.Any(c => c.Id == categoryId) && p.Name == hotelView.Name && p.Number == hotelView.Number) as Hotel;
                        if (hotel == null)
                        {
                            hotel = new Hotel();
                            hotel.ProductTypeId = (int) Enums.ProductTypeEnum.Hotels;
                            hotel.Name = hotelView.Name;
                            hotel.Number = hotelView.Number;
                            hotel.UnitCost = hotelView.UnitCost;
                            hotel.Description = hotelView.Description;
                            hotel.URL = hotelView.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                            hotel.Image = hotelView.Image;
                            hotel.Star = hotelView.Star;
                            hotel.CustomerRating = hotelView.CustomerRating;
                            hotel.Rooms = hotelView.Rooms;
                            hotel.Address = hotelView.Address;
                            hotel.PostCode = hotelView.PostCode;
                            hotel.CurrencyCode = hotelView.CurrencyCode;
                            hotel.Lat = hotelView.Lat;
                            hotel.Lon = hotelView.Lon;
                            hotel.HotelTypeId = hotelView.HotelTypeId;
                            hotel.CreatedByUser = vendorId;
                            hotel.CreatedDate = DateTime.Now;
                            hotel.IsDeleted = false;

                            Location location = db.Locations.SingleOrDefault(c => c.Name == hotelView.Location);
                            if (location != null)
                            {
                                var hotelLocation = new HotelLocation
                                {
                                    HotelTypeId = hotelView.HotelTypeId,
                                    LocationId = location.Id
                                };
                                hotel.HotelLocations.Add(hotelLocation);
                                if (location.ParentLocation != null)
                                {
                                    var parentHotelLocation = new HotelLocation
                                    {
                                        HotelTypeId = hotelView.HotelTypeId,
                                        LocationId = location.ParentLocation.Id
                                    };
                                    hotel.HotelLocations.Add(parentHotelLocation);
                                }
                                if (location.ParentLocation.ParentLocation != null)
                                {
                                    var parentParentHotelLocation = new HotelLocation
                                    {
                                        HotelTypeId = hotelView.HotelTypeId,
                                        LocationId = location.ParentLocation.ParentLocation.Id
                                    };
                                    hotel.HotelLocations.Add(parentParentHotelLocation);
                                }
                            }

                            Category category = db.Categories.Find(categoryId);
                            if (category != null)
                            {
                                hotel.Categories.Add(category);
                            }

                            hotel.FeedId = feedId;
                            db.Products.Add(hotel);

                            db.SaveChanges();

                            i++;
                            //Common.UpdateSteps(stepImport: i);
                        }
                        else
                        {
                            // no need to check for null vallue because of previous if
                            bool isChanged = false;
                            decimal? unitCost = hotelView.UnitCost;
                            if (hotel.UnitCost != unitCost)
                            {
                                hotel.UnitCost = unitCost;
                                isChanged = true;
                            }
                            if (hotel.Description != hotelView.Description)
                            {
                                hotel.Description = hotelView.Description;
                                isChanged = true;
                            }
                            if (hotel.URL != hotelView.URL.Replace("[[PARTNERID]]", "2248").Trim(' '))
                            {
                                hotel.URL = hotelView.URL.Replace("[[PARTNERID]]", "2248").Trim(' ');
                                isChanged = true;
                            }
                            if (hotel.Image != (string)hotelView.Image)
                            {
                                hotel.Image = (string)hotelView.Image;
                                isChanged = true;
                            }
                            decimal? star = hotelView.Star;
                            if (hotel.Star != star)
                            {
                                hotel.Star = star;
                                isChanged = true;
                            }
                            decimal? customerRating = hotelView.CustomerRating;
                            if (hotel.CustomerRating != customerRating)
                            {
                                hotel.CustomerRating = customerRating;
                                isChanged = true;
                            }
                            int? rooms = hotelView.Rooms;
                            if (hotel.Rooms != rooms)
                            {
                                hotel.Rooms = rooms;
                                isChanged = true;
                            }
                            if (hotel.Address != hotelView.Address)
                            {
                                hotel.Address = hotelView.Address;
                                isChanged = true;
                            }
                            if (hotel.PostCode != hotelView.PostCode)
                            {
                                hotel.PostCode = hotelView.PostCode;
                                isChanged = true;
                            }
                            if (hotel.CurrencyCode != hotelView.CurrencyCode)
                            {
                                hotel.CurrencyCode = hotelView.CurrencyCode;
                                isChanged = true;
                            }
                            double? lat = hotelView.Lat;
                            if (hotel.Lat != lat)
                            {
                                hotel.Lat = lat;
                                isChanged = true;
                            }
                            double? lon = hotelView.Lon;
                            if (hotel.Lon != lon)
                            {
                                hotel.Lon = lon;
                                isChanged = true;
                            }
                            int hotelTypeId = hotelView.HotelTypeId;
                            if (hotel.HotelTypeId != hotelTypeId)
                            {
                                hotel.HotelTypeId = hotelTypeId;
                            }

#if UPDATELOCATION
                            int? parentId = null;
                            int? locationId = hotel.LocationId;
                            Location country =
                                db.Locations.SingleOrDefault(
                                    c =>
                                    c.Name == product.Country &&
                                    c.ParentId == null);
                            if (country == null)
                            {
                                country = new Location
                                {
                                    Name = product.Country,
                                    IsDeleted = false
                                };
                                db.Locations.Add(country);
                                db.SaveChanges();
                            }
                            if (country != null)
                            {
                                hotel.Location = country;
                                hotel.LocationId = country.Id;
                                hotel.Location.IsDeleted = false;
                                parentId = country.Id;
                            }
                            Location county =
                                db.Locations.SingleOrDefault(
                                    c =>
                                    c.Name == product.County &&
                                    c.ParentId == parentId);
                            if (county == null)
                            {
                                county = new Location
                                {
                                    Name = product.County,
                                    ParentId = parentId,
                                    IsDeleted = false
                                };
                                db.Locations.Add(county);
                                db.SaveChanges();
                            }
                            if (county != null)
                            {
                                hotel.Location = county;
                                hotel.LocationId = county.Id;
                                hotel.Location.IsDeleted = false;
                                parentId = county.Id;
                            }
                            Location city =
                                db.Locations.SingleOrDefault(
                                    c =>
                                    c.Name == product.City &&
                                    c.ParentId == parentId);
                            if (city == null)
                            {
                                city = new Location
                                {
                                    Name = product.City,
                                    ParentId = parentId,
                                    IsDeleted = false
                                };
                                db.Locations.Add(city);
                                db.SaveChanges();
                            }
                            if (city != null)
                            {
                                hotel.Location = city;
                                hotel.LocationId = city.Id;
                                hotel.Location.IsDeleted = false;
                            }

                            if (hotel.LocationId != locationId)
                            {
                                isChanged = true;
                            }
#endif

                            if (isChanged)
                            {
                                db.SaveChanges();
                            }

                            i++;
                            //Common.UpdateSteps(stepImport: i);
                        }

                        if (bw.CancellationPending)
                        {
                            e.Cancel = true;
                            goto Cancelled;
                        }
                        else if (bw.WorkerReportsProgress && i % 100 == 0)
                        {
                            bw.ReportProgress((int) (100*i/productCount));
                        }
                    }
                }

                if (!e.Cancel)
                {
                    Common.UpdateSteps();
                }
            Cancelled:
                ;
            }
            catch (DbEntityValidationException exception)
            {
                foreach (var eve in exception.EntityValidationErrors)
                {
                    log.Error(String.Format("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                        eve.Entry.Entity.GetType().Name, eve.Entry.State), exception);
                    foreach (var ve in eve.ValidationErrors)
                    {
                        log.Error(String.Format("- Property: \"{0}\", Error: \"{1}\"",
                            ve.PropertyName, ve.ErrorMessage), exception);
                    }
                }
                throw;
            }
            catch (Exception ex)
            {
                e.Result = "ERROR:" + ex.Message;
                log.Error("Error error logging", ex);
            }
        }

        private class HotelView
        {
            public string Name { get; set; }
            public string Number { get; set; }
            public decimal? UnitCost { get; set; }
            public string Description { get; set; }
            public string URL { get; set; }
            public string Image { get; set; }
            public string Location { get; set; }
            public int? Rooms { get; set; }
            public decimal? Star { get; set; }
            public decimal? CustomerRating { get; set; }
            public string Address { get; set; }
            public string CurrencyCode { get; set; }
            public double? Lat { get; set; }
            public double? Lon { get; set; }
            public string PostCode { get; set; }
            public int HotelTypeId { get; set; }
        }
    }
}
