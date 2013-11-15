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
    }
}
