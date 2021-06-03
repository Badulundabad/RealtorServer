using RealtorServer.Model.DataBase;
using RealtyModel.Model;
using RealtyModel.Model.Base;
using RealtyModel.Model.Derived;
using RealtyModel.Model.Operations;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using NLog;
using System.Diagnostics;

namespace RealtorServer.Model.NET
{
    class OperationHandling
    {
        private Logger logger = LogManager.GetCurrentClassLogger();
        protected Operation operation;

        protected bool FindLocationDuplicate(BaseRealtorObject baseRealtorObject)
        {
            using (RealtyContext context = new RealtyContext())
            {
                Location location = baseRealtorObject.Location;
                if (baseRealtorObject is Flat)
                    return context.Flats.Local.Any(bro => bro.Location.City.Name == location.City.Name
                        && bro.Location.District.Name == location.District.Name
                        && bro.Location.Street.Name == location.Street.Name
                        && bro.Location.HouseNumber == location.HouseNumber 
                        && bro.Location.FlatNumber == location.FlatNumber);
                else
                    return context.Houses.Local.Any(bro => bro.Location.City.Name == location.City.Name
                        && bro.Location.District.Name == location.District.Name
                        && bro.Location.Street.Name == location.Street.Name
                        && bro.Location.HouseNumber == location.HouseNumber);
            }
        }
        public virtual Response Handle()
        {
            throw new NotImplementedException();
        }

        protected void LogInfo(String text)
        {
            Debug.WriteLine($"{DateTime.Now} INFO    {text}");
            logger.Info($"    {text}");
        }
        protected void LogWarn(String text)
        {
            Debug.WriteLine($"{DateTime.Now} WARN    {text}");
            logger.Warn($"    {text}");
        }
        protected void LogError(String text)
        {
            Debug.WriteLine($"\n{DateTime.Now} ERROR    {text}\n");
            logger.Error($"    {text}");
        }
    }
}
