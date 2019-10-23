using System;
using System.Collections.Generic;
using System.Linq;
using CertificateAuthority.Code.Models;
using LiteDB;
using NLog;
using Logger = LiteDB.Logger;

namespace CertificateAuthority.Code.Database
{
    public class DataCacheLayer
    {
        public Dictionary<string, CertificateStatus> CertStatusesByThumbprint;

        public HashSet<string> RevokedCertificates;

        private readonly DataRepository repository;

        private LiteCollection<CertificateInfoModel> storeCollection;

        private readonly object storeCollectionLocker;

        private readonly LiteCollection<AccountModel> accountsCollection;

        /// <summary>Protects all access to <see cref="accountsCollection"/>.</summary>
        private readonly object accountsCollectionLocker;

        private readonly NLog.Logger logger = LogManager.GetCurrentClassLogger();

        public DataCacheLayer(DataRepository repository)
        {
            this.repository = repository;
            this.storeCollectionLocker = new object();
            this.accountsCollection = this.repository.GetAccountsCollection();
            this.accountsCollectionLocker = new object();
        }

        public void Initialize()
        {
            // Fill cache.
            this.CertStatusesByThumbprint = new Dictionary<string, CertificateStatus>();
            this.RevokedCertificates = new HashSet<string>();

            this.storeCollection = this.repository.GetCertificatesCollection();

            foreach (CertificateInfoModel info in this.storeCollection.FindAll())
            {
                this.CertStatusesByThumbprint.Add(info.Thumbprint, info.Status);

                if (info.Status == CertificateStatus.Revoked)
                    RevokedCertificates.Add(info.Thumbprint);
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

            this.VerifyCredentialsAndAccessLevel(model, out AccountModel caller);

            if (this.GetCertificateStatus(thumbprint) != CertificateStatus.Good)
                return false;

            CertificateInfoModel certToEdit;

            lock (this.storeCollectionLocker)
            {
                this.CertStatusesByThumbprint[thumbprint] = CertificateStatus.Revoked;

                certToEdit = this.storeCollection.Find(x => x.Thumbprint == thumbprint).Single();

                certToEdit.Status = CertificateStatus.Revoked;
                certToEdit.RevokerAccountId = caller.Id;
                this.storeCollection.Update(certToEdit);
            }

            this.RevokedCertificates.Add(thumbprint);

            this.logger.Info("Certificate id {0}, thumbprint {1} was revoked.", certToEdit.Id, certToEdit.Thumbprint);
            return true;
        }

        /// <summary>Adds new certificate to the certificate collection.</summary>
        public void AddNewCertificate(CertificateInfoModel certificate)
        {
            lock (this.storeCollectionLocker)
            {
                this.storeCollection.Insert(certificate);

                this.CertStatusesByThumbprint.Add(certificate.Thumbprint, certificate.Status);
            }

            this.logger.Info("Certificate id {0}, thumbprint {1} was added.");
        }

        /// <summary>Finds issued certificate by thumbprint and returns it or null if it wasn't found.</summary>
        public CertificateInfoModel GetCertificateByThumbprint(CredentialsAccessWithModel<CredentialsModelWithThumbprintModel> model)
        {
            this.VerifyCredentialsAndAccessLevel(model, out AccountModel account);

            lock (this.storeCollectionLocker)
            {
                return this.storeCollection.FindOne(x => x.Thumbprint == model.Model.Thumbprint);
            }
        }

        /// <summary>Provides collection of all issued certificates.</summary>
        public List<CertificateInfoModel> GetAllCertificates(CredentialsAccessModel accessModelInfo)
        {
            this.VerifyCredentialsAndAccessLevel(accessModelInfo, out AccountModel account);

            lock (this.storeCollectionLocker)
            {
                return this.storeCollection.FindAll().ToList();
            }
        }

        /// <summary>Provides collection of all certificates issued by account with specified id.</summary>
        public List<CertificateInfoModel> GetCertificatesIssuedByAccountId(CredentialsAccessWithModel<CredentialsModelWithTargetId> accessWithModel)
        {
            this.VerifyCredentialsAndAccessLevel(accessWithModel, out AccountModel account);

            lock (this.storeCollectionLocker)
            {
                return this.storeCollection.Find(x => x.IssuerAccountId == accessWithModel.Model.TargetAccountId).ToList();
            }
        }

        #endregion

        #region accounts

        /// <summary>Provides account information of the account with id specified.</summary>
        public AccountInfo GetAccountInfoById(CredentialsAccessWithModel<CredentialsModelWithTargetId> accessWithModel)
        {
            lock (this.accountsCollectionLocker)
            {
                this.VerifyCredentialsAndAccessLevelLocked(accessWithModel, out AccountModel account);

                AccountInfo targetAccount = this.accountsCollection.FindById(accessWithModel.Model.TargetAccountId);

                return targetAccount;
            }
        }

        /// <summary>Provides collection of all existing accounts.</summary>
        public List<AccountInfo> GetAllAccounts(CredentialsAccessModel accessModelInfo)
        {
            lock (this.accountsCollectionLocker)
            {
                this.VerifyCredentialsAndAccessLevelLocked(accessModelInfo, out AccountModel account);

                return this.accountsCollection.FindAll().Select(x => x as AccountInfo).ToList();
            }
        }

        /// <summary>Creates new account.</summary>
        /// <returns>Wrapper with new account's id in it.'</returns>
        public int CreateAccount(CredentialsAccessWithModel<CreateAccount> accessWithModel)
        {
            lock (this.accountsCollectionLocker)
            {
                this.VerifyCredentialsAndAccessLevelLocked(accessWithModel, out AccountModel account);

                if (this.accountsCollection.FindOne(x => x.Name == accessWithModel.Model.NewAccountName) != null)
                    throw new Exception("That name is already taken!");

                AccountAccessFlags newAccountAccessLevel = (AccountAccessFlags)accessWithModel.Model.NewAccountAccess | AccountAccessFlags.BasicAccess;

                if (!DataHelper.IsCreatorHasGreaterOrEqualAccess(account.AccessInfo, newAccountAccessLevel))
                    throw new Exception("You can't create an account with access level higher than yours!");

                AccountModel newAccount = new AccountModel()
                {
                    Name = accessWithModel.Model.NewAccountName,
                    PasswordHash = accessWithModel.Model.NewAccountPasswordHash,
                    AccessInfo = newAccountAccessLevel,
                    CreatorId = account.Id
                };

                this.accountsCollection.Insert(newAccount);

                this.logger.Info("Account was created: '{0}', creator: '{1}'.", newAccount, account);
                return newAccount.Id;
            }
        }

        /// <summary>Deletes existing account with id specified.</summary>
        public void DeleteAccount(CredentialsAccessWithModel<CredentialsModelWithTargetId> accessWithModel)
        {
            lock (this.accountsCollectionLocker)
            {
                this.VerifyCredentialsAndAccessLevelLocked(accessWithModel, out AccountModel caller);

                int accountId = accessWithModel.Model.TargetAccountId;

                AccountModel accountToDelete = this.accountsCollection.FindById(accountId);

                if (accountToDelete == null)
                    throw new Exception("Account not found.");

                if (accountToDelete.Name == Settings.AdminName)
                    throw new Exception("You can't delete Admin account!");

                this.accountsCollection.Delete(accountId);

                this.logger.Info("Account with id {0} was deleted by: '{1}'.", accountId, caller);
            }
        }

        /// <summary>Sets account access level to a provided one.</summary>
        public void ChangeAccountAccessLevel(CredentialsAccessWithModel<ChangeAccountAccessLevel> accessWithModel)
        {
            lock (this.accountsCollectionLocker)
            {
                this.VerifyCredentialsAndAccessLevelLocked(accessWithModel, out AccountModel caller);

                int accountId = accessWithModel.Model.TargetAccountId;

                if (caller.Id == accountId)
                    throw new Exception("You can't change your own access level!");

                AccountModel accountToEdit = this.accountsCollection.FindById(accountId);

                if (accountToEdit == null)
                    throw new Exception("Account not found.");

                if (accountToEdit.Name == Settings.AdminName)
                    throw new Exception("Admin's access level can't be changed.");

                AccountAccessFlags newAccountAccessLevel = (AccountAccessFlags)accessWithModel.Model.AccessFlags;

                if (!DataHelper.IsCreatorHasGreaterOrEqualAccess(caller.AccessInfo, newAccountAccessLevel))
                    throw new Exception("You can't set access level to be higher than yours!");

                AccountAccessFlags oldAccessInfo = accountToEdit.AccessInfo;
                accountToEdit.AccessInfo = newAccountAccessLevel;

                this.accountsCollection.Update(accountToEdit);

                this.logger.Info("Account with id {0} access level was changed from {1} to {2} by account with id {3}.",
                    accountId, oldAccessInfo, accountToEdit.AccessInfo, caller.Id);
            }
        }

        #endregion

        public void VerifyCredentialsAndAccessLevel(CredentialsAccessModel credentialsAccessModel, out AccountModel account)
        {
            lock (this.accountsCollectionLocker)
            {
                this.VerifyCredentialsAndAccessLevelLocked(credentialsAccessModel, out account);
            }
        }

        public void VerifyCredentialsAndAccessLevel<T>(CredentialsAccessWithModel<T> credentialsAccessWithModel, out AccountModel account) where T : CredentialsModel
        {
            lock (this.accountsCollectionLocker)
            {
                this.VerifyCredentialsAndAccessLevelLocked(credentialsAccessWithModel, out account);
            }
        }

        /// <summary>Checks account's password and access attributes for particular action.</summary>
        /// <remarks>All access should be protected by <see cref="accountsCollectionLocker"/>.</remarks>
        /// <exception cref="InvalidCredentialsException">Thrown in case credentials are invalid.</exception>
        private void VerifyCredentialsAndAccessLevelLocked(CredentialsAccessModel credentialsAccessModel, out AccountModel account)
        {
            account = this.accountsCollection.FindById(credentialsAccessModel.AccountId);

            if (account == null)
                throw InvalidCredentialsException.FromErrorCode(CredentialsExceptionErrorCodes.AccountNotFound, credentialsAccessModel.RequiredAccess);

            if (!account.VerifyPassword(credentialsAccessModel.Password))
                throw InvalidCredentialsException.FromErrorCode(CredentialsExceptionErrorCodes.InvalidPassword, credentialsAccessModel.RequiredAccess);

            if (!account.HasAttribute(credentialsAccessModel.RequiredAccess))
                throw InvalidCredentialsException.FromErrorCode(CredentialsExceptionErrorCodes.InvalidAccess, credentialsAccessModel.RequiredAccess);
        }
    }
}
