namespace Chrono.Services;

/// <summary>Токен доступа Google и срок его жизни (<c>expires_in</c>).</summary>
public sealed record GoogleToken(string AccessToken, TimeSpan ExpiresIn);

/// <summary>Платформенный вход в Google: выдаёт токен доступа к календарю.</summary>
public interface IGoogleAuth
{
    /// <summary>interactive=false — только без интерфейса; null — нужен вход.</summary>
    Task<GoogleToken?> GetTokenAsync(bool interactive, CancellationToken ct);

    Task SignOutAsync();
}

/// <summary>Google ответил 410: syncToken устарел, нужна полная загрузка без токена.</summary>
public sealed class SyncTokenExpiredException : Exception
{
}

/// <summary>Токена нет и без интерфейса его не получить: пользователю нужно войти заново.</summary>
public sealed class SignInRequiredException : Exception
{
}
