using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImportProducts
{
    class Common
    {
        public static void AddAdvCatDefaultPermissions(SelectedHotelsEntities db, int advCatID)
        {
            var advCatPermission = new AdvCatPermission
            {
                AdvCatID = advCatID,
                PermissionID = 1,
                RoleID = 0,
                AllowAccess = true
            };
            db.AdvCatPermissions.Add(advCatPermission);
            var advCatPermission2 = new AdvCatPermission
            {
                AdvCatID = advCatID,
                PermissionID = 2,
                RoleID = 0,
                AllowAccess = true
            };
            db.AdvCatPermissions.Add(advCatPermission2);
            var advCatPermission3 = new AdvCatPermission
            {
                AdvCatID = advCatID,
                PermissionID = 1,
                RoleID = -1,
                AllowAccess = true
            };
            db.AdvCatPermissions.Add(advCatPermission3);
            db.SaveChanges();
        }
    }
}
