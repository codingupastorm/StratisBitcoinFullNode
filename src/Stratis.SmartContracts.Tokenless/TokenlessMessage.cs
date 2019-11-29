namespace Stratis.SmartContracts.Tokenless
{
    public class TokenlessMessage : ITokenlessMessage
    {
        public Address ContractAddress { get; }
        public Address Sender { get; }

        public TokenlessMessage(IMessage message)
        {
            this.ContractAddress = message.ContractAddress;
            this.Sender = message.Sender;
        }
    }
}
