using System.Linq;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;

namespace Stratis.Feature.PoA.Tokenless
{
    public static class CertificateExtensions
    {
        public static string GetOrganisation(this X509Certificate certificate)
        {
            return certificate.SubjectDN.GetValueList(X509Name.O).OfType<string>().First();
        }
    }
}