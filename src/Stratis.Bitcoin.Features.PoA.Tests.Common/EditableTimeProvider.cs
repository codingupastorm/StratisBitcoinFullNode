using System;
using Stratis.Core.AsyncWork;

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
