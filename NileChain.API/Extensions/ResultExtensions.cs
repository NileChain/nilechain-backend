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

            return new BadRequestObjectResult(new
            {
                Code = result.Error!.Code,
                Message = result.Error.Description
            });
        }

        public static IActionResult ToActionResult(
            this Result result)
        {
            if (result.IsSuccess)
                return new OkResult();

            return new BadRequestObjectResult(new
            {
                Code = result.Error!.Code,
                Message = result.Error.Description
            });
        }
    }
}
