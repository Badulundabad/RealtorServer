using System.Data.Entity;
using RealtyModel.Model;

namespace RealtorServer.Model.DataBase
{
    public class AgentContext : DbContext
    {
        public AgentContext() : base("UserDBConnection")
        {
            Agents.Load();
        }
        public DbSet<Agent> Agents { get; set; }
    }
}
