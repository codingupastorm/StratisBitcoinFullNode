using System;

namespace Stratis.SmartContracts.Tokenless
{
    /// <summary>
    /// Used to denote methods that don't change data and should be called locally when called.
    ///
    /// This isn't validated via static analysis and is really a developer aid for now.
    /// </summary>
    public class PureAttribute : Attribute
    {

    }
}
