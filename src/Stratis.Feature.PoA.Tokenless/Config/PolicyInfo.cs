using CSharpFunctionalExtensions;

namespace Stratis.Feature.PoA.Tokenless.Config
{
    /// <summary>
    /// Container which allows the various policy types defined in <see cref="PolicyType"/> to be stored.
    /// </summary>
    public class PolicyInfo
    {
        public PolicyInfo(PolicyType policyType, string policy)
        {
            this.PolicyType = policyType;
            this.Policy = policy;
        }

        public PolicyType PolicyType { get; }

        public string Policy { get; }

        public Result Validate()
        {
            if (string.IsNullOrWhiteSpace(Policy))
                return Result.Failure("Policy must not be empty.");

            return Result.Success();
        }
    }
}