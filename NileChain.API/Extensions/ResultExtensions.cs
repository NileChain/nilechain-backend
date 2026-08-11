using NileChain.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace NileChain.API.Extensions
{
    public static class ResultExtensions
    {
        public static IActionResult ToActionResult<T>(
            this Result<T> result)
        {
            if (result.IsSuccess)
                return new OkObjectResult(result.Value);

            return ToErrorResult(result.Error!);
        }

        public static IActionResult ToActionResult(
            this Result result)
        {
            if (result.IsSuccess)
                return new OkResult();

            return ToErrorResult(result.Error!);
        }

        private static ObjectResult ToErrorResult(Error error)
        {
            var status = (int)ResultHttpMapper.MapStatus(error);
            return new ObjectResult(ResultHttpMapper.ToErrorBody(error))
            {
                StatusCode = status
            };
        }
    }
}
