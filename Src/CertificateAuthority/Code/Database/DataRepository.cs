using System;
using System.Linq;
using CertificateAuthority.Code.Models;
using LiteDB;
using NLog;

namespace CertificateAuthority.Code.Database
{
    public class DataRepository
    {
        private readonly NLog.Logger logger = LogManager.GetCurrentClassLogger();

        public readonly LiteDatabase Database;

        private const string certificateInfosKey = "certInfos";

        private const string accountsKey = "accounts";

        public DataRepository(Settings settings)
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

            LiteCollection<AccountModel> accountsCollection = this.GetAccountsCollection();

            if (accountsCollection.Count() == 0 && settings.CreateAdminAccountOnCleanStart)
            {
                AccountAccessFlags adminAttrs = AccountAccessFlags.AccessAccountInfo;

                foreach (AccountAccessFlags attr in DataHelper.AllAccessFlags)
                    adminAttrs |= attr;

                // Add Admin.
                accountsCollection.Insert(new AccountModel()
                {
                    Name = Settings.AdminName,
                    PasswordHash = settings.DefaultAdminPasswordHash,
                    AccessInfo = adminAttrs,

                    // Will set below.
                    CreatorId = 1
                });

                this.logger.Info("Default Admin account was added.");
            }
        }

        public LiteCollection<CertificateInfoModel> GetCertificatesCollection()
        {
            LiteCollection<CertificateInfoModel> collection = this.Database.GetCollection<CertificateInfoModel>(certificateInfosKey);

            return collection;
        }

        public LiteCollection<AccountModel> GetAccountsCollection()
        {
            LiteCollection<AccountModel> collection = this.Database.GetCollection<AccountModel>(accountsKey);

            return collection;
        }
    }
}
