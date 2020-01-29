using System.ComponentModel.DataAnnotations;

namespace CertificateAuthority.Models
{
    /// <summary>
    /// This model initializes the certificate authority as well as set the admin account's password.
    /// </summary>
    public sealed class InitializeCertificateAuthorityModel
    {
        [Required]
        public byte AddressPrefix { get; set; }

        [Required]
        public int CoinType { get; set; }

        /// <summary>Mnemonic words used to derive certificate authority's private key.</summary>
        [Required]
        public string Mnemonic { get; set; }

        /// <summary>Password to be used with the mnemonic words, used to derive certificate authority's private key.
        /// This is a separate password to the actual user account to allow the user account password to be changed without affecting the CA.</summary>
        [Required]
        public string MnemonicPassword { get; set; }

        /// <summary>
        /// This will be used to set the admin account's password on initialization.
        /// </summary>
        [Required]
        public string AdminPassword { get; set; }

        public InitializeCertificateAuthorityModel()
        {
        }

        public InitializeCertificateAuthorityModel(string mnemonic, string mnemonicPassword, int coinType, byte addressPrefix, string password)
        {
            this.AddressPrefix = addressPrefix;
            this.CoinType = coinType;
            this.Mnemonic = mnemonic;
            this.MnemonicPassword = mnemonicPassword;
            this.AdminPassword = password;
        }
    }
}
