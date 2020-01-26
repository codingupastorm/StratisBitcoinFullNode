using System;
using System.Collections.Generic;
using System.Linq;
using CertificateAuthority.Controllers;
using CertificateAuthority.Models;
using NBitcoin.DataEncoders;
using NLog;

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
                    // Create Admin.
                    var admin = new AccountModel()
                    {
                        Name = Settings.AdminName,
                        PasswordHash = settings.DefaultAdminPasswordHash,
                        AccessInfo = AccountAccessFlags.AdminAccess,
                        Approved = true,

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
            return new CADbContext(this.settings);
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
                        this.RevokedCertificates.Add(info.Thumbprint);
                    else if ((info.BlockSigningPubKey?.Length ?? 0) != 0)
                        this.PublicKeys.Add(Encoders.Hex.EncodeData(info.BlockSigningPubKey));
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
            if ((certificate.BlockSigningPubKey?.Length ?? 0) != 0)
                this.PublicKeys.Add(Encoders.Hex.EncodeData(certificate.BlockSigningPubKey));

            using (CADbContext dbContext = this.CreateContext())
            {
                dbContext.Certificates.Add(certificate);
                dbContext.SaveChanges();
            }

            this.logger.Info("Certificate id {0}, thumbprint {1} was added.");
        }

        /// <summary>Provides the certificate issued by the account with the specified id, if any.</summary>
        public CertificateInfoModel GetCertificateIssuedByAccountId(CredentialsAccessWithModel<CredentialsModelWithTargetId> accessWithModel)
        {
            // TODO: We shouldn't need special access rights to retrieve the certificate for the authenticating user, i.e. when Model.AccountId == Model.TargetAccountId
            return ExecuteQuery(accessWithModel, (dbContext) => { return dbContext.Certificates.FirstOrDefault(x => x.AccountId == accessWithModel.Model.TargetAccountId); });
        }

        #endregion

        #region Accounts

        /// <summary>Provides account information of the account with id specified.</summary>
        public AccountInfo GetAccountInfoById(CredentialsAccessWithModel<CredentialsModelWithTargetId> credentialsModel)
        {
            AccountInfo accountInfo = ExecuteQuery<CredentialsAccessWithModel<CredentialsModelWithTargetId>, AccountInfo>(credentialsModel, (dbContext) => dbContext.Accounts.SingleOrDefault(x => x.Id == credentialsModel.Model.TargetAccountId));

            // TODO: Investigate whether there is a more elegant way of handling a lack of permissions
            if (accountInfo.Permissions == null)
                accountInfo.Permissions = new List<Permission>();

            return accountInfo;
        }

        /// <summary>Sets the Approved flag on an account.</summary>
        public AccountInfo ApproveAccount(CredentialsAccessWithModel<CredentialsModelWithTargetId> credentialsModel)
        {
            return ExecuteCommand(credentialsModel, (dbContext, account) =>
            {
                int accountId = credentialsModel.Model.TargetAccountId;

                AccountModel accountToApprove = dbContext.Accounts.SingleOrDefault(x => x.Id == accountId);

                if (accountToApprove.Approved)
                    throw new CertificateAuthorityAccountException("Account already approved!");

                accountToApprove.Approved = true;

                dbContext.Accounts.Update(accountToApprove);
                dbContext.SaveChanges();

                return accountToApprove;
            });
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
                if (dbContext.Accounts.Any(x => x.Name == credentialsModel.Model.CommonName))
                    throw new CertificateAuthorityAccountException("That name is already taken!");

                AccountAccessFlags newAccountAccessLevel = (AccountAccessFlags)credentialsModel.Model.RequestedAccountAccess | AccountAccessFlags.BasicAccess;

                if (!DataHelper.IsCreatorHasGreaterOrEqualAccess(account.AccessInfo, newAccountAccessLevel))
                    throw new CertificateAuthorityAccountException("You can't create an account with an access level higher than yours!");

                if (credentialsModel.Model.RequestedPermissions.Any(permission => !AccountsController.ValidPermissions.Contains(permission.Name)))
                    throw new CertificateAuthorityAccountException("Invalid permission requested!");

                var newAccount = new AccountModel()
                {
                    Name = credentialsModel.Model.CommonName,
                    PasswordHash = credentialsModel.Model.NewAccountPasswordHash,
                    AccessInfo = newAccountAccessLevel,
                    CreatorId = account.Id,
                    OrganizationUnit = credentialsModel.Model.OrganizationUnit,
                    Organization = credentialsModel.Model.Organization,
                    Locality = credentialsModel.Model.Locality,
                    StateOrProvince = credentialsModel.Model.StateOrProvince,
                    EmailAddress = credentialsModel.Model.EmailAddress,
                    Country = credentialsModel.Model.Country,
                    Permissions = credentialsModel.Model.RequestedPermissions,
                    Approved = false
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
                    throw new CertificateAuthorityAccountException("Account not found.");

                if (accountToDelete.Name == Settings.AdminName)
                    throw new CertificateAuthorityAccountException("You can't delete Admin account!");

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
                    throw new CertificateAuthorityAccountException("You can't change your own access level!");

                AccountModel accountToEdit = dbContext.Accounts.SingleOrDefault(x => x.Id == accountId);

                if (accountToEdit == null)
                    throw new CertificateAuthorityAccountException("Account not found.");

                if (accountToEdit.Name == Settings.AdminName)
                    throw new CertificateAuthorityAccountException("Admin's access level can't be changed.");

                var newAccountAccessLevel = (AccountAccessFlags)credentialsModel.Model.AccessFlags;

                if (!DataHelper.IsCreatorHasGreaterOrEqualAccess(account.AccessInfo, newAccountAccessLevel))
                    throw new CertificateAuthorityAccountException("You can't set access level to be higher than yours!");

                AccountAccessFlags oldAccessInfo = accountToEdit.AccessInfo;
                accountToEdit.AccessInfo = newAccountAccessLevel;

                dbContext.Accounts.Update(accountToEdit);
                dbContext.SaveChanges();

                this.logger.Info("Account with id {0} access level was changed from {1} to {2} by account with id {3}.", accountId, oldAccessInfo, accountToEdit.AccessInfo, account.Id);
            });
        }

        public void ChangeAccountPassword(CredentialsAccessWithModel<ChangeAccountPasswordModel> credentialsModel)
        {
            ExecuteCommand(credentialsModel, (dbContext, account) =>
            {
                AccountModel targetAccount = dbContext.Accounts.SingleOrDefault(x => x.Id == credentialsModel.Model.TargetAccountId);

                if (targetAccount == null)
                    throw new Exception($"Target account not found: {credentialsModel.Model.TargetAccountId}");

                // If the account and target account is not the same check if the account is the admin account.
                if (targetAccount.Id != credentialsModel.Model.AccountId)
                {
                    AccountModel adminAccount = dbContext.Accounts.SingleOrDefault(a => a.Id == credentialsModel.Model.AccountId);
                    if (adminAccount == null)
                        throw new Exception($"The credential account does not exist: {credentialsModel.Model.AccountId}");

                    if (adminAccount.Name != Settings.AdminName)
                        throw new Exception("Only you or an admin account can change the password.");
                }
                // If the account is the same as the target account, check the old password.
                else
                {
                    if (!targetAccount.VerifyPassword(credentialsModel.Model.Password))
                        throw new Exception($"The target account's old password is incorrect.");
                }

                targetAccount.PasswordHash = DataHelper.ComputeSha256Hash(credentialsModel.Model.NewPassword);

                dbContext.Accounts.Update(targetAccount);
                dbContext.SaveChanges();

                this.logger.Info("Account Id {0}'s password has been updated.", credentialsModel.Model.TargetAccountId);
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

            if (!account.Approved)
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