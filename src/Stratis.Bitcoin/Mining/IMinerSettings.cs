namespace Stratis.Core.Mining
{
    public interface IMinerSettings
    {
        /// <summary>
        /// Settings for <see cref="BlockDefinition"/>.
        /// </summary>
        BlockDefinitionOptions BlockDefinitionOptions { get; }
    }
}
