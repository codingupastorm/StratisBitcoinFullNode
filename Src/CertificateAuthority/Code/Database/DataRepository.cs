using System.Linq;
using CertificateAuthority.Code.Models;
using LiteDB;
using NLog;

namespace CertificateAuthority.Code.Database
{
    public class DataRepository
    {
        private readonly NLog.Logger logger = LogManager.GetCurrentClassLogger();

        private readonly LiteDbContext context;

        private const string certificateInfosKey = "certInfos";

        private const string accountsKey = "accounts";

        public DataRepository(LiteDbContext context, Settings settings)
        {
            this.context = context;

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
            LiteCollection<CertificateInfoModel> collection = context.Database.GetCollection<CertificateInfoModel>(certificateInfosKey);

            return collection;
        }

        public LiteCollection<AccountModel> GetAccountsCollection()
        {
            LiteCollection<AccountModel> collection = context.Database.GetCollection<AccountModel>(accountsKey);

            return collection;
        }
    }
}
