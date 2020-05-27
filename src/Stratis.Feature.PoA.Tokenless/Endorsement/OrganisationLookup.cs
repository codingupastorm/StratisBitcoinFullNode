﻿using MembershipServices;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IOrganisationLookup
    {
        (Organisation organisation, string sender) FromTransaction(Transaction transaction);
        (Organisation organisation, string sender) FromCertificate(X509Certificate certificate);
    }

    public class OrganisationLookup : IOrganisationLookup
    {
        private readonly ITokenlessSigner tokenlessSigner;
        private readonly IMembershipServicesDirectory membershipServices;
        private readonly Network network;

        public OrganisationLookup(ITokenlessSigner tokenlessSigner, IMembershipServicesDirectory membershipServices, Network network)
        {
            this.tokenlessSigner = tokenlessSigner;
            this.membershipServices = membershipServices;
            this.network = network;
        }

        public (Organisation organisation, string sender) FromTransaction(Transaction transaction)
        {
            // Retrieving sender and certificate should not fail due to mempool rule ie. we shouldn't need to check for errors here.
            GetSenderResult sender = this.tokenlessSigner.GetSender(transaction);

            X509Certificate certificate = this.membershipServices.GetCertificateForAddress(sender.Sender);

            var organisation = (Organisation)certificate.GetOrganisation();

            return (organisation, sender.Sender.ToBase58Address(this.network));
        }

        public (Organisation organisation, string sender) FromCertificate(X509Certificate certificate)
        {
            var organisation = (Organisation)certificate.GetOrganisation();
            var base58SenderAddress = MembershipServicesDirectory.GetCertificateTransactionSigningAddress(certificate, this.network);

            return (organisation, base58SenderAddress);
        }
    }
}