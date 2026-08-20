# Research

## Verified premises

- At current head `eaf2f9f4`, `ReadFolderMessagesAsync` directly calls `GetProperty("value").EnumerateArray()`; missing/non-array values therefore escape as framework exceptions rather than the existing `InvalidDataException`.
- A present `@odata.nextLink` is passed to `new Uri(..., UriKind.Absolute)`; malformed or relative strings escape as `UriFormatException` before exact path validation.
- `ResolveDeletedItemsFolderAsync` passes its root directly to `RequiredString`; a successful array/scalar root makes `TryGetProperty` throw `InvalidOperationException`.
- `GraphDeletedMailSearchSource` already maps `InvalidDataException` to unavailable. The outer catch must remain unchanged.
- Existing fake-HTTP Graph and authenticated Web tests can prove the boundary without a new fake or framework.

## Assumptions

- None. All three exception paths and the current narrow unavailable mapping were checked directly.
