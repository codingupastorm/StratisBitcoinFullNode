namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public struct Organisation
    {
        public Organisation(string value)
        {
            this.Value = value;
        }

        public readonly string Value;

        public static explicit operator Organisation(string value)
        {
            return new Organisation(value);
        }

        public static implicit operator string(Organisation gas)
        {
            return gas.Value;
        }

        public override string ToString()
        {
            return this.Value;
        }
    }
}