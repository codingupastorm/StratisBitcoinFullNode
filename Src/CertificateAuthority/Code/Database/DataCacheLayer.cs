using CertificateAuthority.Code.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CertificateAuthority.Code.Database
{
    public class DataCacheLayer
    {
        public Dictionary<string, CertificateStatus> CertStatusesByThumbprint;

        public HashSet<string> RevokedCertificates;

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private Settings settings;

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
                    AccountModel admin = new AccountModel()
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

        public void Initialize()
        {
            // Fill cache.
            this.CertStatusesByThumbprint = new Dictionary<string, CertificateStatus>();
            this.RevokedCertificates = new HashSet<string>();

            using (CADbContext dbContext = this.CreateContext())
            {
                foreach (CertificateInfoModel info in dbContext.Certificates)
                {
                    this.CertStatusesByThumbprint.Add(info.Thumbprint, info.Status);

                    if (info.Status == CertificateStatus.Revoked)
                        RevokedCertificates.Add(info.Thumbprint);
                }
            }

            this.logger.Info("{0} initialized with {1} certificates, {2} of them revoked.", nameof(DataCacheLayer),
                this.CertStatusesByThumbprint.Count, this.RevokedCertificates.Count);
        }

        #region certificates

        /// <summary>
        /// Get's status of the certificate with the provided thumbprint or
        /// returns <see cref="CertificateStatus.Unknown"/> if certificate wasn't found.
        /// </summary>
        public CertificateStatus GetCertificateStatus(string thumbprint)
        {
            if (this.CertStatusesByThumbprint.TryGetValue(thumbprint, out CertificateStatus status))
            {
                return status;
            }

            return CertificateStatus.Unknown;
        }

        /// <summary>
        /// Sets certificate status with the provided thumbprint to <see cref="CertificateStatus.Revoked"/>
        /// if certificate was found and it's status is <see cref="CertificateStatus.Good"/>.
        /// </summary>
        public bool RevokeCertificate(CredentialsAccessWithModel<CredentialsModelWithThumbprintModel> model)
        {
            string thumbprint = model.Model.Thumbprint;

            using (CADbContext dbContext = this.CreateContext())
            {
                this.VerifyCredentialsAndAccessLevel(model, dbContext, out AccountModel caller);

                if (this.GetCertificateStatus(thumbprint) != CertificateStatus.Good)
                    return false;

                this.CertStatusesByThumbprint[thumbprint] = CertificateStatus.Revoked;

                CertificateInfoModel certToEdit = dbContext.Certificates.Single(x => x.Thumbprint == thumbprint);

                certToEdit.Status = CertificateStatus.Revoked;
                certToEdit.RevokerAccountId = caller.Id;

                dbContext.Update(certToEdit);
                dbContext.SaveChanges();

                this.RevokedCertificates.Add(thumbprint);
                this.logger.Info("Certificate id {0}, thumbprint {1} was revoked.", certToEdit.Id, certToEdit.Thumbprint);
            }

            return true;
        }

        /// <summary>Adds new certificate to the certificate collection.</summary>
        public void AddNewCertificate(CertificateInfoModel certificate)
        {
            this.CertStatusesByThumbprint.Add(certificate.Thumbprint, certificate.Status);

            using (CADbContext dbContext = this.CreateContext())
            {
                dbContext.Certificates.Add(certificate);
                dbContext.SaveChanges();
            }

            this.logger.Info("Certificate id {0}, thumbprint {1} was added.");
        }

        /// <summary>Finds issued certificate by thumbprint and returns it or null if it wasn't found.</summary>
        public CertificateInfoModel GetCertificateByThumbprint(CredentialsAccessWithModel<CredentialsModelWithThumbprintModel> model)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.VerifyCredentialsAndAccessLevel(model, dbContext, out AccountModel account);

                return dbContext.Certificates.SingleOrDefault(x => x.Thumbprint == model.Model.Thumbprint);
            }
        }

        /// <summary>Provides collection of all issued certificates.</summary>
        public List<CertificateInfoModel> GetAllCertificates(CredentialsAccessModel accessModelInfo)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.VerifyCredentialsAndAccessLevel(accessModelInfo, dbContext, out AccountModel account);

                return dbContext.Certificates.ToList();
            }
        }

        /// <summary>Provides collection of all certificates issued by account with specified id.</summary>
        public List<CertificateInfoModel> GetCertificatesIssuedByAccountId(CredentialsAccessWithModel<CredentialsModelWithTargetId> accessWithModel)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.VerifyCredentialsAndAccessLevel(accessWithModel, dbContext, out AccountModel account);

                return dbContext.Certificates.Where(x => x.IssuerAccountId == accessWithModel.Model.TargetAccountId).ToList();
            }
        }

        #endregion

        #region accounts

        /// <summary>Provides account information of the account with id specified.</summary>
        public AccountInfo GetAccountInfoById(CredentialsAccessWithModel<CredentialsModelWithTargetId> accessWithModel)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.VerifyCredentialsAndAccessLevel(accessWithModel, dbContext, out AccountModel account);

                return dbContext.Accounts.SingleOrDefault(x => x.Id == accessWithModel.Model.TargetAccountId);
            }
        }

        /// <summary>Provides collection of all existing accounts.</summary>
        public List<AccountModel> GetAllAccounts(CredentialsAccessModel accessModelInfo)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.VerifyCredentialsAndAccessLevel(accessModelInfo, dbContext, out AccountModel account);

                return dbContext.Accounts.ToList();
            }
        }

        /// <summary>Creates new account.</summary>
        /// <returns>Wrapper with new account's id in it.'</returns>
        public int CreateAccount(CredentialsAccessWithModel<CreateAccount> accessWithModel)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.VerifyCredentialsAndAccessLevel(accessWithModel, dbContext, out AccountModel account);

                if (dbContext.Accounts.Any(x => x.Name == accessWithModel.Model.NewAccountName))
                    throw new Exception("That name is already taken!");

                AccountAccessFlags newAccountAccessLevel =
                    (AccountAccessFlags)accessWithModel.Model.NewAccountAccess | AccountAccessFlags.BasicAccess;

                if (!DataHelper.IsCreatorHasGreaterOrEqualAccess(account.AccessInfo, newAccountAccessLevel))
                    throw new Exception("You can't create an account with access level higher than yours!");

                AccountModel newAccount = new AccountModel()
                {
                    Name = accessWithModel.Model.NewAccountName,
                    PasswordHash = accessWithModel.Model.NewAccountPasswordHash,
                    AccessInfo = newAccountAccessLevel,
                    CreatorId = account.Id
                };

                dbContext.Accounts.Add(newAccount);
                dbContext.SaveChanges();

                this.logger.Info("Account was created: '{0}', creator: '{1}'.", newAccount, account);
                return newAccount.Id;
            }
        }

        /// <summary>Deletes existing account with id specified.</summary>
        public void DeleteAccount(CredentialsAccessWithModel<CredentialsModelWithTargetId> accessWithModel)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.VerifyCredentialsAndAccessLevel(accessWithModel, dbContext, out AccountModel caller);

                int accountId = accessWithModel.Model.TargetAccountId;

                AccountModel accountToDelete = dbContext.Accounts.SingleOrDefault(x => x.Id == accountId);

                if (accountToDelete == null)
                    throw new Exception("Account not found.");

                if (accountToDelete.Name == Settings.AdminName)
                    throw new Exception("You can't delete Admin account!");

                dbContext.Accounts.Remove(accountToDelete);
                dbContext.SaveChanges();

                this.logger.Info("Account with id {0} was deleted by: '{1}'.", accountId, caller);
            }
        }

        /// <summary>Sets account access level to a provided one.</summary>
        public void ChangeAccountAccessLevel(CredentialsAccessWithModel<ChangeAccountAccessLevel> accessWithModel)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.VerifyCredentialsAndAccessLevel(accessWithModel, dbContext, out AccountModel caller);

                int accountId = accessWithModel.Model.TargetAccountId;

                if (caller.Id == accountId)
                    throw new Exception("You can't change your own access level!");

                AccountModel accountToEdit = dbContext.Accounts.SingleOrDefault(x => x.Id == accountId);

                if (accountToEdit == null)
                    throw new Exception("Account not found.");

                if (accountToEdit.Name == Settings.AdminName)
                    throw new Exception("Admin's access level can't be changed.");

                AccountAccessFlags newAccountAccessLevel = (AccountAccessFlags)accessWithModel.Model.AccessFlags;

                if (!DataHelper.IsCreatorHasGreaterOrEqualAccess(caller.AccessInfo, newAccountAccessLevel))
                    throw new Exception("You can't set access level to be higher than yours!");

                AccountAccessFlags oldAccessInfo = accountToEdit.AccessInfo;
                accountToEdit.AccessInfo = newAccountAccessLevel;

                dbContext.Accounts.Update(accountToEdit);
                dbContext.SaveChanges();

                this.logger.Info("Account with id {0} access level was changed from {1} to {2} by account with id {3}.",
                    accountId, oldAccessInfo, accountToEdit.AccessInfo, caller.Id);
            }
        }

        #endregion

        /// <summary>Checks account's password and access attributes for particular action.</summary>
        /// <exception cref="InvalidCredentialsException">Thrown in case credentials are invalid.</exception>
        private void VerifyCredentialsAndAccessLevel(CredentialsAccessModel credentialsAccessModel, CADbContext dbContext, out AccountModel account)
        {
            account = dbContext.Accounts.SingleOrDefault(x => x.Id == credentialsAccessModel.AccountId);

            if (account == null)
                throw InvalidCredentialsException.FromErrorCode(CredentialsExceptionErrorCodes.AccountNotFound, credentialsAccessModel.RequiredAccess);

            if (!account.VerifyPassword(credentialsAccessModel.Password))
                throw InvalidCredentialsException.FromErrorCode(CredentialsExceptionErrorCodes.InvalidPassword, credentialsAccessModel.RequiredAccess);

            if (!account.HasAttribute(credentialsAccessModel.RequiredAccess))
                throw InvalidCredentialsException.FromErrorCode(CredentialsExceptionErrorCodes.InvalidAccess, credentialsAccessModel.RequiredAccess);
        }

        /// <summary>Checks account's password and access attributes for particular action.</summary>
        /// <exception cref="InvalidCredentialsException">Thrown in case credentials are invalid.</exception>
        public void VerifyCredentialsAndAccessLevel(CredentialsAccessModel credentialsAccessModel, out AccountModel account)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.VerifyCredentialsAndAccessLevel(credentialsAccessModel, dbContext, out account);
            }
        }
    }
}
