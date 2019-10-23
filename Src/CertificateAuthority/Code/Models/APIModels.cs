﻿using Microsoft.AspNetCore.Http;

namespace CertificateAuthority.Code.Models
{
    public class CredentialsModelWithTargetId : CredentialsModel
    {
        /// <summary>Id of an account you want to get information for.</summary>
        public int TargetAccountId { get; set; }

        public CredentialsModelWithTargetId(int targetAccountId, int accountId, string password) : base(accountId, password)
        {
            this.TargetAccountId = targetAccountId;
        }
    }

    #region Models for AccountsController
    public class CreateAccount : CredentialsModel
    {
        /// <summary>Account name for a new account. Can't be a a nickname that is already taken.</summary>
        public string NewAccountName { get; set; }

        /// <summary>Sha256 hash of new account's password.</summary>
        public string NewAccountPasswordHash { get; set; }

        /// <summary>Access level flag for a new account.</summary>
        public int NewAccountAccess { get; set; }

        public CreateAccount(string newAccountName, string newAccountPasswordHash, int newAccountAccess, int accountId, string password) : base(accountId, password)
        {
            this.NewAccountName = newAccountName;
            this.NewAccountPasswordHash = newAccountPasswordHash;
            this.NewAccountAccess = newAccountAccess;
        }
    }

    public class ChangeAccountAccessLevel : CredentialsModelWithTargetId
    {
        /// <summary>New access flags to set.</summary>
        public int AccessFlags { get; set; }

        public ChangeAccountAccessLevel(int accessFlags, int targetAccountId, int accountId, string password) : base(targetAccountId, accountId, password)
        {
            this.AccessFlags = accessFlags;
        }
    }
    #endregion

    #region Models for CertificatesController
    public class CredentialsModelWithThumbprintModel : CredentialsModel
    {
        /// <summary>Certificate's thumbprint.</summary>
        public string Thumbprint { get; set; }

        public CredentialsModelWithThumbprintModel(string thumbprint, int accountId, string password) : base(accountId, password)
        {
            this.Thumbprint = thumbprint;
        }
    }

    /// <summary>Model that combines credentials information and a file sent in request.</summary>
    public class IssueCertificateFromRequestModel : CredentialsModel
    {
        public IFormFile CertificateRequestFile { get; set; }

        public IssueCertificateFromRequestModel(IFormFile certificateRequestFile, int accountId, string password) : base(accountId, password)
        {
            this.CertificateRequestFile = certificateRequestFile;
        }
    }

    public class IssueCertificateFromFileContentsModel : CredentialsModel
    {
        public string CertificateRequestFileContents { get; set; }

        public IssueCertificateFromFileContentsModel(string certificateRequestFileContents, int accountId, string password) : base(accountId, password)
        {
            this.CertificateRequestFileContents = certificateRequestFileContents;
        }
    }

    public class GetCertificateStatusModel
    {
        /// <summary>Certificate's thumbprint.</summary>
        public string Thumbprint { get; set; }

        /// <summary>Set to <c>true</c> for 'Good\Revoked\Unknown' format, or <c>false</c> for '1\2\3' format.</summary>
        public bool AsString { get; set; }

        public GetCertificateStatusModel(string thumbprint, bool asString)
        {
            this.Thumbprint = thumbprint;
            this.AsString = asString;
        }
    }
    #endregion

    #region Models for HelpersController
    public class CredentialsModelWithStringModel : CredentialsModel
    {
        public string Value { get; set; }

        public CredentialsModelWithStringModel(string value, int accountId, string password) : base(accountId, password)
        {
            this.Value = value;
        }
    }
    #endregion
}

