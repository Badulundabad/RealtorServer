using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Operations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtorServer.Model.NET
{
    class Requesting : OperationHandling
    {
        public Requesting(Operation operation) {
            this.operation = operation;
        }
        //private byte[] HandleLocationRequest() {
        //    LocationOptions lists = new LocationOptions();
        //    using (RealtyContext context = new RealtyContext()) {
        //        if (context.Cities.Local.Count > 0)
        //            lists.Cities = new ObservableCollection<City>(context.Cities.Local);
        //        else lists.Cities = new ObservableCollection<City>();
        //        if (context.Districts.Local.Count > 0)
        //            lists.Districts = new ObservableCollection<District>(context.Districts.Local);
        //        else lists.Districts = new ObservableCollection<District>();
        //        if (context.Streets.Local.Count > 0)
        //            lists.Streets = new ObservableCollection<Street>(context.Streets.Local);
        //        else lists.Streets = new ObservableCollection<Street>();
        //    }
        //}
        //public override byte[] Handle() {
        //    if (operation.Target == Target.Locations) {
        //        return HandleLocationRequest();
        //    } else {
        //        throw new NotImplementedException();
        //    }
        //}

    }
}
