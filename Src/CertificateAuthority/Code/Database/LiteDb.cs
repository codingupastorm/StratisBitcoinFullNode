using System;
using LiteDB;
using NLog;

namespace CertificateAuthority.Code.Database
{
    public class LiteDbContext
    {
        public readonly LiteDatabase Database;

        private readonly NLog.Logger logger = LogManager.GetCurrentClassLogger();

        public LiteDbContext(Settings settings)
        {
            try
            {
                this.Database = new LiteDatabase(settings.LiteDbPath);

                this.logger.Debug("Database initialized at {0}.", settings.LiteDbPath);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex);
                throw new Exception("Can find or create LiteDb database.", ex);
            }
        }
    }
}
