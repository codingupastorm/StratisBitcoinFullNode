using System;
using Stratis.Core.Utilities;

namespace Stratis.Features.PoA.Tests.Common
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
