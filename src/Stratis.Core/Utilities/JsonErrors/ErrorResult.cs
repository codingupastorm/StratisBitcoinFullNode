using Microsoft.AspNetCore.Mvc;

namespace Stratis.Core.Utilities.JsonErrors
{
    public class ErrorResult : ObjectResult
    {
        public ErrorResult(int statusCode, ErrorResponse value) : base(value)
        {
            this.StatusCode = statusCode;
        }
    }
}
