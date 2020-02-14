using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace CertificateAuthority.Models
{
    public class CredentialsModelWithTargetId : CredentialsModel
    {
        /// <summary>Id of an account you want to configure or otherwise interact with.</summary>
        public int TargetAccountId { get; set; }

        public CredentialsModelWithTargetId(int targetAccountId, int accountId, string password) : base(accountId, password)
        {
            this.TargetAccountId = targetAccountId;
        }

        public CredentialsModelWithTargetId()
        {
        }
    }

    #region Models for AccountsController

    public class CreateAccount
    {
        public string Password { get; set; }

        /// <summary>Account name for the new account. Can't be a a name that is already taken.
        /// This will also be used as the common name (CN) field of the requested certificate.</summary>
        public string CommonName { get; set; }

        /// <summary>Sha256 hash of new account's password.</summary>
        public string NewAccountPasswordHash { get; set; }

        /// <summary>Access level flags requested for the new account.</summary>
        public int RequestedAccountAccess { get; set; }

        public string OrganizationUnit { get; set; }

        public string Organization { get; set; }

        public string Locality { get; set; }

        public string StateOrProvince { get; set; }

        public string EmailAddress { get; set; }

        public string Country { get; set; }

        /// <summary>
        /// A list of the OIDs for the permissions desired by the requester.
        /// These can also be separately granted by the administrator prior to certificate generation.
        /// </summary>
        public List<Permission> RequestedPermissions { get; set; }

        public CreateAccount(string commonName, string newAccountPasswordHash, int requestedAccountAccess, string organizationUnit, string organization, string locality, string stateOrProvince, string emailAddress, string country, List<string> requestedPermissions, string password)
        {
            this.CommonName = commonName;
            this.NewAccountPasswordHash = newAccountPasswordHash;
            this.RequestedAccountAccess = requestedAccountAccess;
            this.OrganizationUnit = organizationUnit;
            this.Organization = organization;
            this.Locality = locality;
            this.StateOrProvince = stateOrProvince;
            this.EmailAddress = emailAddress;
            this.Country = country;
            this.RequestedPermissions = requestedPermissions.Select(p => new Permission() { Name = p }).ToList();
            this.Password = password;
        }

        public CreateAccount()
        {
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

        public ChangeAccountAccessLevel()
        {
        }
    }

    public sealed class ChangeAccountPasswordModel : CredentialsModel
    {
        /// <summary>The new password to use.</summary>
        public string NewPassword { get; set; }

        /// <summary>The account which password will be changed.</summary>
        public int TargetAccountId { get; set; }

        public ChangeAccountPasswordModel(int accountId, int targetAccountId, string password, string newPassword) : base(accountId, password)
        {
            this.NewPassword = newPassword;
            this.TargetAccountId = targetAccountId;
        }

        public ChangeAccountPasswordModel() { }
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

        public CredentialsModelWithThumbprintModel()
        {
        }
    }

    public class CredentialsModelWithAddressModel : CredentialsModel
    {
        /// <summary>Certificate's P2PKH address, stored in extension OID 1.4.1.</summary>
        public string Address { get; set; }

        public CredentialsModelWithAddressModel(string address, int accountId, string password) : base(accountId, password)
        {
            this.Address = address;
        }

        public CredentialsModelWithAddressModel()
        {
        }
    }

    public class CredentialsModelWithPubKeyHashModel : CredentialsModel
    {
        /// <summary>Certificate's transaction signing pubkey hash, stored in extension OID 1.4.2.</summary>
        public string PubKeyHash { get; set; }

        public CredentialsModelWithPubKeyHashModel(string pubKeyHash, int accountId, string password) : base(accountId, password)
        {
            this.PubKeyHash = pubKeyHash;
        }

        public CredentialsModelWithPubKeyHashModel()
        {
        }
    }

    public class GenerateCertificateSigningRequestModel : CredentialsModel
    {
        public string Address { get; set; }

        // We need to transmit this pubkey so that the CSR template can be generated by the CA. It does not get used after that.
        public string PubKey { get; set; }

        public string BlockSigningPubKey { get; set; }

        public string TransactionSigningPubKeyHash { get; set; }

        public GenerateCertificateSigningRequestModel(string address, string pubKey, string transactionSigningPubKeyHash, string blockSigningPubKey, int accountId, string password) : base(accountId, password)
        {
            this.Address = address;
            this.PubKey = pubKey;
            this.TransactionSigningPubKeyHash = transactionSigningPubKeyHash;
            this.BlockSigningPubKey = blockSigningPubKey;
        }

        public GenerateCertificateSigningRequestModel()
        {
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

        public IssueCertificateFromRequestModel()
        {
        }
    }

    public class IssueCertificateFromFileContentsModel : CredentialsModel
    {
        public string CertificateRequestFileContents { get; set; }

        public IssueCertificateFromFileContentsModel(string certificateRequestFileContents, int accountId, string password) : base(accountId, password)
        {
            this.CertificateRequestFileContents = certificateRequestFileContents;
        }

        public IssueCertificateFromFileContentsModel()
        {
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

        public GetCertificateStatusModel()
        {
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

        public CredentialsModelWithStringModel()
        {
        }
    }
    #endregion
}

