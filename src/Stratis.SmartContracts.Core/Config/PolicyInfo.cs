namespace Stratis.SmartContracts.Core.Config
{
    /// <summary>
    /// Container which allows the various policy types defined in <see cref="PolicyType"/> to be stored.
    /// </summary>
    public class PolicyInfo
    {
        public PolicyType PolicyType { get; set; }

        public string Policy { get; set; }
    }
}