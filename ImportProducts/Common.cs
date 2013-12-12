using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImportProducts
{
    public static class Common
    {
        public static IEnumerable<Hotel> HotelsInLocation(SelectedHotelsEntities db, int locationId)
        {
            IList<Hotel> hotels = (from p in db.Products
                                   where !p.IsDeleted
                                   select p).OfType<Hotel>().ToList();
            var query = from h in hotels
                        where h.LocationId == locationId || h.Location.ParentId == locationId ||
                              (h.Location.ParentLocation != null && h.Location.ParentLocation.ParentId == locationId)
                        select h;
            return query;
        }

        public static void UpdateSteps(int? stepImport = null, int? stepAddToCategories = null, int? stepAddImages = null)
        {
            using (var context = new SelectedHotelsEntities())
            {
                Feed feed = context.Feeds.SingleOrDefault(f => f.Id == 1);
                feed.StepImport = stepImport;
                feed.StepAddToCategories = stepAddToCategories;
                feed.StepAddImages = stepAddImages;
                context.SaveChanges();
            }
        }

        public static void UpdateLocationLeveling(SelectedHotelsEntities db)
        {
            try
            {
                IList<Hotel> hotels = db.Products.OfType<Hotel>().ToList();
                var england = db.Locations.SingleOrDefault(l => l.Name == "England" && l.ParentId == null);
                if (england == null)
                    return;
                foreach (Location location in db.Locations.Where(l => l.ParentId == england.Id))
                {
                    var existingLocation =
                        db.Locations.FirstOrDefault(l => l.Name == location.Name && l.ParentId != england.Id);
                    if (existingLocation != null)
                    {
                        Location location1 = location;
                        var query = from h in hotels
                            where h.LocationId == location1.Id
                            select h;
                        foreach (Hotel hotel in query)
                        {
                            hotel.LocationId = existingLocation.Id;
                            Console.WriteLine("{0}:{1}:{2}", hotel.Id, hotel.Name, hotel.Location.Name);
                        }
                    }
                }
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                ImportTradeDoublerHotels.log.Error("Error error logging", ex);
            }
        }

        public static void DeleteEmptyLocations(SelectedHotelsEntities db)
        {
            foreach (Location location in db.Locations.Where(l => !l.IsDeleted))
            {
                if (!HotelsInLocation(db, location.Id).Any())
                {
                    location.IsDeleted = true;
                }
            }
            db.SaveChanges();
        }
    }
}
