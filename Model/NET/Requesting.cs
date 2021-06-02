using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using RealtyModel.Service;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        private Response GetLocations() {
            LocationOptions lists = new LocationOptions();
            using (RealtyContext context = new RealtyContext()) {
                lists.Cities = new ObservableCollection<City>(context.Cities.Local);
                lists.Districts = new ObservableCollection<District>(context.Districts.Local);
                lists.Streets = new ObservableCollection<Street>(context.Streets.Local);
            }
            Response response = new Response(BinarySerializer.Serialize(lists));
            if (lists.Cities.Count == 0
                && lists.Districts.Count == 0
                && lists.Streets.Count == 0) {
                response.Code = ErrorCode.NoLocations;
            }
            return response;
        }
        private Response GetRealtorObjects() {
            using (RealtyContext realtyContext = new RealtyContext()) {
                Filter filter = BinarySerializer.Deserialize<Filter>(operation.Data);
                Flat[] flats = filter.FilterFlats(realtyContext.Flats.Local.ToArray());
                House[] houses = Array.Empty<House>();
                Tuple<Flat[], House[]> objects = new Tuple<Flat[], House[]>(flats, houses);
                Response response = new Response(BinarySerializer.Serialize(objects), ErrorCode.NoCode);
                return response;
            }
        }
        public override Response Handle() {
            if (operation.Target == Target.Locations) {
                return GetLocations();
            } else if (operation.Target == Target.RealtorObjects) {
                return GetRealtorObjects();
            } else {
                throw new NotImplementedException();
            }
        }

    }
}
