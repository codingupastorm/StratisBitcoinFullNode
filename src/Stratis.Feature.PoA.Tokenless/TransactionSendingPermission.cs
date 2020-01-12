using System;
using CertificateAuthority;

namespace Stratis.Feature.PoA.Tokenless
{
    public enum TransactionSendingPermission
    {
        Send,
        CallContract,
        CreateContract
    }

    public static class TransactionSendingPermissionUtils
    {
        public static string GetPermissionOid(this TransactionSendingPermission permission)
        {
            switch (permission)
            {
                case TransactionSendingPermission.Send:
                    return CaCertificatesManager.SendPermission;
                case TransactionSendingPermission.CallContract:
                    return CaCertificatesManager.CallContractPermissionOid;
                case TransactionSendingPermission.CreateContract:
                    return CaCertificatesManager.CreateContractPermissionOid;
            }

            throw new ArgumentException("Input permission is not bound to an Oid.");
        }
    }
}
