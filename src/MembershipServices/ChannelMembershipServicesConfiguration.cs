namespace MembershipServices
{
    public class ChannelMembershipServicesConfiguration
    {
        // TODO: This is still a stub, as we do not yet have channels implemented

        // The essential idea is that a channel membership configuration is a (JSON) document describing the permissions of various entities participating in a channel.
        // This list is intended to be kept synchronised via consensus in the HL MSP design; the mechanism whereby this will be done in this solution is not yet fully defined.

        // https://hyperledger-fabric.readthedocs.io/en/release-2.0/membership/membership.html#channel-msps

        public ChannelMembershipServicesConfiguration()
        {
            // Channel MSPs contain the MSPs of the organizations of the channel members.

            // An actor with the admin role needs to be explicitly granted admin rights to a given channel resource via the channel policy.
            // TODO: Define channel policy structure/format
        }
    }
}
