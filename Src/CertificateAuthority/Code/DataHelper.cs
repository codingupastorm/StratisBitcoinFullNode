using CertificateAuthority.Code.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CertificateAuthority.Code
{
    public static class DataHelper
    {
        public static List<AccountAccessFlags> AllAccessFlags { get; private set; }

        static DataHelper()
        {
            AllAccessFlags = Enum.GetValues(typeof(AccountAccessFlags)).Cast<AccountAccessFlags>().ToList();
        }

        /// <summary>Computes sha245 hash of provided string.</summary>
        public static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2"));

                return builder.ToString();
            }
        }

        /// <summary>Converts space separated single line certificate request into multiple lines format.</summary>
        /// <remarks>
        /// Converts space separated single line with following format
        /// <c>-----BEGIN CERTIFICATE REQUEST----- MIIE1jCCAr ... 7w1gjwn -----END CERTIFICATE REQUEST-----</c>
        /// to an array of separated lines.
        /// If format is invalid <c>null</c> is returned.
        /// </remarks>
        public static List<string> GetCertificateRequestLines(string singleLineCertRequest)
        {
            string startString = "-----BEGIN CERTIFICATE REQUEST-----";
            string endString = "-----END CERTIFICATE REQUEST-----";

            if (!singleLineCertRequest.StartsWith(startString + " ") || !singleLineCertRequest.EndsWith(endString))
                return null;

            string temp = singleLineCertRequest;

            temp = temp.Substring(startString.Length + 1, temp.Length - startString.Length - 1);
            temp = temp.Substring(0, temp.Length - endString.Length - 1);

            List<string> contentLines = temp.Split(' ').ToList();

            if (contentLines.Any(x => string.IsNullOrEmpty(x)))
                return null;

            contentLines.Insert(0, startString);
            contentLines.Add(endString);

            return contentLines;
        }

        /// <summary>Checks if creator has more or the same access level as child account.</summary>
        public static bool IsCreatorHasGreaterOrEqualAccess(AccountAccessFlags creatorFlags, AccountAccessFlags childAccountAccess)
        {
            int master = (int)creatorFlags;
            int slave = (int)childAccountAccess;

            int xor = master ^ slave;
            int flippedMaster = ~master;

            int result = xor & flippedMaster; // should be 0

            return result == 0;
        }
    }
}
