using NLog;
using System;
using System.Linq;
using System.Collections.Generic;
using CertificateAuthority.Models;
using NBitcoin;

namespace CertificateAuthority.Database
{
    public class DataCacheLayer
    {
        public Dictionary<string, CertificateStatus> CertStatusesByThumbprint;

        public HashSet<string> RevokedCertificates;

        public HashSet<string> PublicKeys;

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly Settings settings;

        public DataCacheLayer(Settings settings)
        {
            this.settings = settings;

            using (CADbContext dbContext = this.CreateContext())
            {
                dbContext.Database.EnsureCreated();

                if (!dbContext.Accounts.Any() && settings.CreateAdminAccountOnCleanStart)
                {
                    AccountAccessFlags adminAttrs = AccountAccessFlags.AccessAccountInfo;

                    foreach (AccountAccessFlags attr in DataHelper.AllAccessFlags)
                        adminAttrs |= attr;

                    // Create Admin.
                    var admin = new AccountModel()
                    {
                        Name = Settings.AdminName,
                        PasswordHash = settings.DefaultAdminPasswordHash,
                        AccessInfo = adminAttrs,

                        // Will set below.
                        CreatorId = 1
                    };

                    dbContext.Accounts.Add(admin);
                    dbContext.SaveChanges();

                    this.logger.Info("Default Admin account was added.");
                }
            }
        }

        private CADbContext CreateContext()
        {
            return new CADbContext(settings);
        }

        private bool IsValidPubKey(string pubKey)
        {
            if (string.IsNullOrEmpty(pubKey))
                return false;

            try
            {
                new PubKey(pubKey);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public void Initialize()
        {
            // Fill cache.
            this.CertStatusesByThumbprint = new Dictionary<string, CertificateStatus>();
            this.RevokedCertificates = new HashSet<string>();
            this.PublicKeys = new HashSet<string>();

            using (CADbContext dbContext = this.CreateContext())
            {
                foreach (CertificateInfoModel info in dbContext.Certificates)
                {
                    this.CertStatusesByThumbprint.Add(info.Thumbprint, info.Status);

                    if (info.Status == CertificateStatus.Revoked)
                        RevokedCertificates.Add(info.Thumbprint);
                    else if (this.IsValidPubKey(info.PubKey))
                        this.PublicKeys.Add(info.PubKey);
                }
            }

            this.logger.Info("{0} initialized with {1} certificates, {2} of them revoked.", nameof(DataCacheLayer),
                this.CertStatusesByThumbprint.Count, this.RevokedCertificates.Count);
        }

        #region Certificates

        /// <summary>Adds new certificate to the certificate collection.</summary>
        public void AddNewCertificate(CertificateInfoModel certificate)
        {
            this.CertStatusesByThumbprint.Add(certificate.Thumbprint, certificate.Status);
            if (this.IsValidPubKey(certificate.PubKey))
                this.PublicKeys.Add(certificate.PubKey);

            using (CADbContext dbContext = this.CreateContext())
            {
                dbContext.Certificates.Add(certificate);
                dbContext.SaveChanges();
            }

            this.logger.Info("Certificate id {0}, thumbprint {1} was added.");
        }

        /// <summary>Provides collection of all certificates issued by account with specified id.</summary>
        public List<CertificateInfoModel> GetCertificatesIssuedByAccountId(CredentialsAccessWithModel<CredentialsModelWithTargetId> accessWithModel)
        {
            return ExecuteQuery(accessWithModel, (dbContext) => { return dbContext.Certificates.Where(x => x.IssuerAccountId == accessWithModel.Model.TargetAccountId).ToList(); });
        }

        #endregion

        #region Accounts

        /// <summary>Provides account information of the account with id specified.</summary>
        public AccountInfo GetAccountInfoById(CredentialsAccessWithModel<CredentialsModelWithTargetId> credentialsModel)
        {
            return ExecuteQuery<CredentialsAccessWithModel<CredentialsModelWithTargetId>, AccountInfo>(credentialsModel, (dbContext) => dbContext.Accounts.SingleOrDefault(x => x.Id == credentialsModel.Model.TargetAccountId));
        }

        /// <summary>Provides collection of all existing accounts.</summary>
        public List<AccountModel> GetAllAccounts(CredentialsAccessModel credentialsModel)
        {
            return ExecuteQuery(credentialsModel, (dbContext) => dbContext.Accounts.ToList());
        }

        /// <summary>Creates a new account.</summary>
        public int CreateAccount(CredentialsAccessWithModel<CreateAccount> credentialsModel)
        {
            return ExecuteQuery(credentialsModel, (dbContext, account) =>
            {
                if (dbContext.Accounts.Any(x => x.Name == credentialsModel.Model.NewAccountName))
                    throw new Exception("That name is already taken!");

                AccountAccessFlags newAccountAccessLevel =
                    (AccountAccessFlags)credentialsModel.Model.NewAccountAccess | AccountAccessFlags.BasicAccess;

                if (!DataHelper.IsCreatorHasGreaterOrEqualAccess(account.AccessInfo, newAccountAccessLevel))
                    throw new Exception("You can't create an account with an access level higher than yours!");

                AccountModel newAccount = new AccountModel()
                {
                    Name = credentialsModel.Model.NewAccountName,
                    PasswordHash = credentialsModel.Model.NewAccountPasswordHash,
                    AccessInfo = newAccountAccessLevel,
                    CreatorId = account.Id
                };

                dbContext.Accounts.Add(newAccount);
                dbContext.SaveChanges();

                this.logger.Info("Account was created: '{0}', creator: '{1}'.", newAccount, account);
                return newAccount.Id;
            });
        }

        /// <summary>Deletes existing account with id specified.</summary>
        public void DeleteAccount(CredentialsAccessWithModel<CredentialsModelWithTargetId> credentialsModel)
        {
            ExecuteCommand(credentialsModel, (dbContext, account) =>
            {
                int accountId = credentialsModel.Model.TargetAccountId;

                AccountModel accountToDelete = dbContext.Accounts.SingleOrDefault(x => x.Id == accountId);

                if (accountToDelete == null)
                    throw new Exception("Account not found.");

                if (accountToDelete.Name == Settings.AdminName)
                    throw new Exception("You can't delete Admin account!");

                dbContext.Accounts.Remove(accountToDelete);
                dbContext.SaveChanges();

                this.logger.Info("Account with id {0} was deleted by: '{1}'.", accountId, account);
            });
        }

        /// <summary>Sets account access level to a provided one.</summary>
        public void ChangeAccountAccessLevel(CredentialsAccessWithModel<ChangeAccountAccessLevel> credentialsModel)
        {
            ExecuteCommand(credentialsModel, (dbContext, account) =>
            {
                int accountId = credentialsModel.Model.TargetAccountId;

                if (account.Id == accountId)
                    throw new Exception("You can't change your own access level!");

                AccountModel accountToEdit = dbContext.Accounts.SingleOrDefault(x => x.Id == accountId);

                if (accountToEdit == null)
                    throw new Exception("Account not found.");

                if (accountToEdit.Name == Settings.AdminName)
                    throw new Exception("Admin's access level can't be changed.");

                AccountAccessFlags newAccountAccessLevel = (AccountAccessFlags)credentialsModel.Model.AccessFlags;

                if (!DataHelper.IsCreatorHasGreaterOrEqualAccess(account.AccessInfo, newAccountAccessLevel))
                    throw new Exception("You can't set access level to be higher than yours!");

                AccountAccessFlags oldAccessInfo = accountToEdit.AccessInfo;
                accountToEdit.AccessInfo = newAccountAccessLevel;

                dbContext.Accounts.Update(accountToEdit);
                dbContext.SaveChanges();

                this.logger.Info("Account with id {0} access level was changed from {1} to {2} by account with id {3}.", accountId, oldAccessInfo, accountToEdit.AccessInfo, account.Id);
            });
        }

        #endregion

        /// <summary>Validate the account's password and access attributes for a particular action.</summary>
        /// <exception cref="InvalidCredentialsException">Thrown in case credentials are invalid.</exception>
        public void VerifyCredentialsAndAccessLevel(CredentialsAccessModel credentialsAccessModel, CADbContext dbContext, out AccountModel account)
        {
            account = dbContext.Accounts.SingleOrDefault(x => x.Id == credentialsAccessModel.AccountId);

            if (account == null)
                throw InvalidCredentialsException.FromErrorCode(CredentialsExceptionErrorCodes.AccountNotFound, credentialsAccessModel.RequiredAccess);

            if (!account.VerifyPassword(credentialsAccessModel.Password))
                throw InvalidCredentialsException.FromErrorCode(CredentialsExceptionErrorCodes.InvalidPassword, credentialsAccessModel.RequiredAccess);

            if (!account.HasAttribute(credentialsAccessModel.RequiredAccess))
                throw InvalidCredentialsException.FromErrorCode(CredentialsExceptionErrorCodes.InvalidAccess, credentialsAccessModel.RequiredAccess);
        }

        /// <summary>Validate the account's password and access attributes for a particular action.</summary>
        /// <exception cref="InvalidCredentialsException">Thrown in case credentials are invalid.</exception>
        public void VerifyCredentialsAndAccessLevel(CredentialsAccessModel credentialsAccessModel, out AccountModel account)
        {
            CADbContext dbContext = this.CreateContext();
            this.VerifyCredentialsAndAccessLevel(credentialsAccessModel, dbContext, out account);
        }

        public TResult ExecuteQuery<TAccessModel, TResult>(TAccessModel accessWithModel, Func<CADbContext, TResult> action) where TAccessModel : CredentialsAccessModel
        {
            CADbContext dbContext = this.CreateContext();
            this.VerifyCredentialsAndAccessLevel(accessWithModel, dbContext, out AccountModel account);
            return action(dbContext);
        }

        public TResult ExecuteQuery<TAccessModel, TResult>(TAccessModel accessWithModel, Func<CADbContext, AccountModel, TResult> action) where TAccessModel : CredentialsAccessModel
        {
            CADbContext dbContext = this.CreateContext();
            this.VerifyCredentialsAndAccessLevel(accessWithModel, dbContext, out AccountModel account);
            return action(dbContext, account);
        }

        public void ExecuteCommand<TAccessModel>(TAccessModel accessWithModel, Action<CADbContext, AccountModel> action) where TAccessModel : CredentialsAccessModel
        {
            CADbContext dbContext = this.CreateContext();
            this.VerifyCredentialsAndAccessLevel(accessWithModel, dbContext, out AccountModel account);
            action(dbContext, account);
        }

        public TResult ExecuteCommand<TAccessModel, TResult>(TAccessModel accessWithModel, Func<CADbContext, AccountModel, TResult> action) where TAccessModel : CredentialsAccessModel
        {
            CADbContext dbContext = this.CreateContext();
            this.VerifyCredentialsAndAccessLevel(accessWithModel, dbContext, out AccountModel account);
            return action(dbContext, account);
        }
    }
}