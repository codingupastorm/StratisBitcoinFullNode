using Microsoft.AspNetCore.Http;

namespace CertificateAuthority.Models
{
    public class CredentialsModelWithTargetId : CredentialsModel
    {
        /// <summary>Id of an account you want to get information for.</summary>
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

    public class CredentialsModelWithMnemonicModel : CredentialsModel
    {
        /// <summary>Mnemonic words used to derive certificate authority's private key.</summary>
        public string Mnemonic { get; set; }

        /// <summary>Password to be used with the mnemonic words, used to derive certificate authority's private key.
        /// This is a separate password to the actual user account to allow the user account password to be changed without affecting the CA.</summary>
        public string MnemonicPassword { get; set; }

        public int CoinType { get; set; }

        public byte AddressPrefix { get; set; }

        public CredentialsModelWithMnemonicModel(string mnemonic, string mnemonicPassword, int coinType, byte addressPrefix, int accountId, string password) : base(accountId, password)
        {
            this.Mnemonic = mnemonic;
            this.MnemonicPassword = mnemonicPassword;
            this.CoinType = coinType;
            this.AddressPrefix = addressPrefix;
        }

        public CredentialsModelWithMnemonicModel()
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

