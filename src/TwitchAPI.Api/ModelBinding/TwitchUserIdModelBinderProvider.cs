using Microsoft.AspNetCore.Mvc.ModelBinding;
using TwitchAPI.Domain;

namespace TwitchAPI.Api.ModelBinding;

internal sealed class TwitchUserIdModelBinderProvider : IModelBinderProvider
{
    private static readonly TwitchUserIdModelBinder Binder = new();

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Metadata.ModelType == typeof(TwitchUserId) ? Binder : null;
    }
}
