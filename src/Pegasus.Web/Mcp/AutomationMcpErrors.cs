using ModelContextProtocol;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Mcp;

/// <summary>
/// Translates Core refusals into content-safe MCP errors. Domain exceptions
/// carry deliberately safe messages (version conflicts, lease state,
/// operation replays, validation refusals) and pass through; anything
/// unexpected collapses to a generic failure so no infrastructure detail
/// crosses the boundary.
/// </summary>
internal static class AutomationMcpErrors
{
    public static async Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> action)
    {
        try
        {
            return await action();
        }
        catch (McpException)
        {
            throw;
        }
        catch (StaffAuthorizationException)
        {
            throw new McpException(
                "The Automation actor is not authorized for this action.");
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or InvalidDataException)
        {
            throw new McpException(exception.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new McpException("The automation action failed.");
        }
    }

    /// <summary>
    /// Mutation tools take explicit caller idempotency keys prefixed
    /// <c>mcp:</c>, mirroring the existing command contracts (100-character
    /// maximum, no whitespace or control characters).
    /// </summary>
    public static string RequireOperationKey(string operationKey)
    {
        var normalized = operationKey?.Trim();
        if (string.IsNullOrEmpty(normalized)
            || !normalized.StartsWith("mcp:", StringComparison.Ordinal)
            || normalized.Length is <= 4 or > 100
            || normalized.Any(character =>
                char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new McpException(
                "A caller idempotency operation key is required: prefixed 'mcp:', "
                + "at most 100 characters, without whitespace or control characters.");
        }

        return normalized;
    }

    public static Guid RequireId(Guid value, string name) =>
        value == Guid.Empty
            ? throw new McpException($"A non-empty {name} is required.")
            : value;

    public static byte[] DecodeContent(string contentBase64, int maximumBytes, string description)
    {
        var maximumCharacters = ((maximumBytes + 2) / 3) * 4;
        if (string.IsNullOrWhiteSpace(contentBase64)
            || contentBase64.Length > maximumCharacters)
        {
            throw new McpException(
                $"{description} is required and must decode to at most {maximumBytes} bytes.");
        }

        byte[] content;
        try
        {
            content = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            throw new McpException($"{description} is not valid base64.");
        }

        if (content.Length == 0 || content.Length > maximumBytes)
        {
            throw new McpException(
                $"{description} must decode to between 1 and {maximumBytes} bytes.");
        }

        return content;
    }

    public static string RequireFileName(string fileName)
    {
        var normalized = fileName?.Trim();
        if (string.IsNullOrEmpty(normalized)
            || normalized.Length > 255
            || !Path.GetFileName(normalized).Equals(normalized, StringComparison.Ordinal)
            || normalized is "." or "..")
        {
            throw new McpException(
                "A leaf file name of at most 255 characters without path components is required.");
        }

        return normalized;
    }

    public static string RequireMediaType(string mediaType)
    {
        var normalized = mediaType?.Trim();
        if (string.IsNullOrEmpty(normalized)
            || normalized.Length > 200
            || normalized.Any(char.IsControl))
        {
            throw new McpException("A media type of at most 200 characters is required.");
        }

        return normalized;
    }
}
