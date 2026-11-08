using Microsoft.AspNetCore.Mvc.ModelBinding;
using TwitchAPI.Domain;

namespace TwitchAPI.Api.ModelBinding;

internal sealed class TwitchUserIdModelBinder : IModelBinder
{
    private const string NotExactlyOneWellFormedTwitchUserId =
        "Expected exactly one Twitch User ID made of 1 to 20 ASCII digits.";

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var values = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (values.Length == 1 && TwitchUserId.TryParse(values.FirstValue, out var twitchUserId))
        {
            bindingContext.Result = ModelBindingResult.Success(twitchUserId);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, NotExactlyOneWellFormedTwitchUserId);
        }

        return Task.CompletedTask;
    }
}
