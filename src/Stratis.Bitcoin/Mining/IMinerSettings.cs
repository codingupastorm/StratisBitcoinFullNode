namespace Stratis.Bitcoin.Mining
{
    public interface IMinerSettings
    {
        /// <summary>
        /// Settings for <see cref="BlockDefinition"/>.
        /// </summary>
        BlockDefinitionOptions BlockDefinitionOptions { get; }
    }
}
