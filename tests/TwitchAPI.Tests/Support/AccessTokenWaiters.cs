using TwitchAPI.Application.Twitch;

namespace TwitchAPI.Tests.Support;

public sealed class AccessTokenWaiters
{
    private readonly TaskCompletionSource _allWaiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _expected = int.MaxValue;
    private int _waiting;

    public Task AllWaiting => _allWaiting.Task;

    public void Expect(int count)
    {
        Volatile.Write(ref _waiting, 0);
        Volatile.Write(ref _expected, count);
    }

    internal IAppAccessTokenProvider Observe(IAppAccessTokenProvider inner) => new ObservedProvider(inner, this);

    private void OnWaiting()
    {
        if (Interlocked.Increment(ref _waiting) == Volatile.Read(ref _expected))
        {
            _allWaiting.TrySetResult();
        }
    }

    private sealed class ObservedProvider(IAppAccessTokenProvider inner, AccessTokenWaiters waiters)
        : IAppAccessTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            var accessToken = inner.GetAccessTokenAsync(cancellationToken);
            waiters.OnWaiting();
            return accessToken;
        }

        public void Invalidate(string rejectedAccessToken) => inner.Invalidate(rejectedAccessToken);
    }
}
