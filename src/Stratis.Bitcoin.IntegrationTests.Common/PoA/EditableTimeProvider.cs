using System;
using Stratis.Core.Utilities;

namespace Stratis.Bitcoin.IntegrationTests.Common.PoA
{
    public class EditableTimeProvider : DateTimeProvider
    {
        public TimeSpan AdjustedTimeOffset
        {
            get { return this.adjustedTimeOffset; }
            set { this.adjustedTimeOffset = value; }
        }
    }
}
