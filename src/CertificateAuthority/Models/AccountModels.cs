using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace CertificateAuthority.Models
{
    public class Permission
    {
        [JsonIgnore]
        public int Id { get; set; }
        
        /// <summary>The foreign key for the associated <see cref="AccountModel"/> that has this permission.</summary>
        [JsonIgnore]
        public int AccountModelId { get; set; }

        /// <summary>The human-readable name for the permission.</summary>
        public string Name { get; set; }
    }

    /// <summary>General information about user's account.</summary>
    public class AccountInfo
    {
        public string Name { get; set; }

        public AccountAccessFlags AccessInfo { get; set; }

        public int ApproverId { get; set; }

        public string OrganizationUnit { get; set; }

        public string Organization { get; set; }

        public string Locality { get; set; }

        public string StateOrProvince { get; set; }

        public string EmailAddress { get; set; }

        public string Country { get; set; }

        /// <summary>The permissions that this account is approved to have.</summary>
        public List<Permission> Permissions { get; set; }

        /// <summary>Indicates whether or not the administrator has approved the creation of the account.</summary>
        public bool Approved { get; set; }

        public override string ToString()
        {
            return $"{nameof(this.Name)}:{this.Name},{nameof(this.AccessInfo)}:{(int)this.AccessInfo},{nameof(this.ApproverId)}:{this.ApproverId}";
        }
    }

    public class AccountModel : AccountInfo
    {
        public int Id { get; set; }

        public string PasswordHash { get; set; }

        public bool VerifyPassword(string password)
        {
            return DataHelper.ComputeSha256Hash(password) == this.PasswordHash;
        }

        public bool HasAttribute(AccountAccessFlags flag)
        {
            return (this.AccessInfo & flag) == flag;
        }

        public override string ToString()
        {
            return base.ToString() + $",{nameof(this.Id)}:{this.Id},{nameof(this.PasswordHash)}:{this.PasswordHash}";
        }
    }

    [Flags]
    public enum AccountAccessFlags : int
    {
        BasicAccess = 0, // All accounts have this
        DeleteAccounts = 1,
        AccessAccountInfo = 2,
        RevokeCertificates = 4,
        IssueCertificates = 8,
        AccessAnyCertificate = 16,
        ChangeAccountAccessLevel = 32,
        CreateAccounts = 64,
        InitializeCertificateAuthority = 128,
        ApproveAccounts = 256,
        AdminAccess = ~0 // All bits set.
    }

    /// <summary>Credentials needed for API endpoints with restricted access.</summary>
    public class CredentialsModel
    {
        /// <summary>Caller's account Id.</summary>
        public int AccountId { get; set; }

        /// <summary>Caller's password.</summary>
        public string Password { get; set; }

        public CredentialsModel(int accountId, string password)
        {
            this.AccountId = accountId;
            this.Password = password;
        }

        public CredentialsModel()
        {
        }
    }

    public class CredentialsAccessModel : CredentialsModel
    {
        public CredentialsAccessModel(int accountId, string password, AccountAccessFlags requiredAccess) : base(accountId, password)
        {
            this.RequiredAccess = requiredAccess;
        }

        public CredentialsAccessModel()
        {
        }

        public AccountAccessFlags RequiredAccess { get; private set; }
    }

    public class CredentialsAccessWithModel<T> : CredentialsAccessModel where T : CredentialsModel
    {
        public T Model { get; set; }

        public CredentialsAccessWithModel(T model, AccountAccessFlags requiredAccess) : base(model.AccountId, model.Password, requiredAccess)
        {
            this.Model = model;
        }

        public CredentialsAccessWithModel()
        {
        }
    }

    public class InvalidCredentialsException : Exception
    {
        public readonly CredentialsExceptionErrorCodes ErrorCode;

        public static InvalidCredentialsException FromErrorCode(CredentialsExceptionErrorCodes errorCode, AccountAccessFlags requiredAccess)
        {
            string message = string.Empty;

            if (errorCode == CredentialsExceptionErrorCodes.AccountNotFound)
                message = "Account not found.";
            else if (errorCode == CredentialsExceptionErrorCodes.InvalidPassword)
                message = "Password is invalid.";
            else if (errorCode == CredentialsExceptionErrorCodes.InvalidAccess)
                message = $"You cant access this action. {requiredAccess.ToString()} access is required.";

            return new InvalidCredentialsException(errorCode, message);
        }

        private InvalidCredentialsException(CredentialsExceptionErrorCodes errorCode, string message) : base(message)
        {
            this.ErrorCode = errorCode;
        }
    }

    public enum CredentialsExceptionErrorCodes
    {
        AccountNotFound,
        InvalidPassword,
        InvalidAccess
    }
}
