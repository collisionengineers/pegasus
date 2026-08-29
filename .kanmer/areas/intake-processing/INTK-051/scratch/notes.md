## The array binding was NOT proven by precedent — so it was proven empirically

`Program.cs:266` reads the allowed content types as

```csharp
var allowedMediaTypes = section.GetSection("AllowedMediaTypes").Get<string[]>()
    ?? throw new InvalidOperationException(
        "DocumentRequests:AllowedMediaTypes is required when accepted request limits are enabled.");
```

and the bicep supplies them as five separate environment variables,
`DocumentRequests__AllowedMediaTypes__0` … `__4`. If that indexed form did not
bind to a `string[]`, `Get<string[]>()` would return null, the factory would
throw, and **the Container App would crash-loop** — taking the whole
application down, not just upload links.

**The precedent I first reached for does not actually hold.** I assumed the
pattern was proven because the integration fixture uses it — but
`IntakeWebTestSupport.cs:136-141` supplies those keys through
`AddInMemoryCollection`, which is a different provider from
`AddEnvironmentVariables`. And the one array-shaped setting that *does* reach
production through an environment variable, `AutomationMcp__RedirectUris`, is
**not** bound as an array at all: `AutomationMcp.cs:109` reads it as a single
delimited string and splits it by hand. So nothing in this deployment
demonstrated that an indexed array binds from real environment variables.

Rather than reason about documented framework behaviour, it was tested directly.
A throwaway console project set the exact thirteen variables the bicep sets as
real process environment variables, built the configuration with
`AddEnvironmentVariables()` alone, and made the identical
`GetSection("AllowedMediaTypes").Get<string[]>()` call:

```
AllowedMediaTypes bound: 5 entries
   application/pdf
   image/jpeg
   image/png
   text/plain
   application/vnd.openxmlformats-officedocument.wordprocessingml.document
LimitsVersion            = 'int-31-interim-v1'
LifetimeHours            = 168
MaximumFileCount         = 10
MaximumRequestBytes      = 10485760
RateLimitWindowMinutes   = 10
```

Five entries, in order, and every scalar parses to the intended value rather
than the `0` that `GetValue<T>` silently returns for a missing or misspelled
key — which would itself have tripped `RequestUploadLimits`' constructor and
crash-looped the host from nothing worse than a typo.

The scratch project was deleted after the run; it exists only in this record.

**Conclusion: the composition is safe.** This was the single highest-risk
detail in the change, and it is now settled by evidence rather than by
confidence in a framework convention.
