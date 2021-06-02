using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Base;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtorServer.Model.NET
{
    class OperationHandling
    {
        protected Operation operation;
        protected bool FindLocationDuplicate(BaseRealtorObject baseRealtorObject, RealtyContext realtyContext) {
            ObservableCollection<BaseRealtorObject> objects = new ObservableCollection<BaseRealtorObject>(realtyContext.Flats.Local);
            if (baseRealtorObject is House) {
                objects = new ObservableCollection<BaseRealtorObject>(realtyContext.Houses.Local);
            }
            Location location = baseRealtorObject.Location;
            return objects.Any(bro => bro.Location.City == location.City
                && bro.Location.District == location.District
                && bro.Location.Street == location.Street);
        }
        public virtual Response Handle() {
            throw new NotImplementedException();
        }
    }
}
