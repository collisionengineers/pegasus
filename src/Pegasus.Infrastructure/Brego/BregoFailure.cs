using System.Net;

namespace Pegasus.Infrastructure.Brego;

/// <summary>A Brego call that did not answer as Brego answers, named by its stage.</summary>
internal sealed class BregoFailure : Exception
{
    public BregoFailure(string stage, HttpStatusCode? status, string detail, bool forbidden = false)
        : base($"Brego {stage} failed: {detail}")
    {
        Stage = stage;
        Status = status;
        Detail = detail;
        Forbidden = forbidden;
    }

    /// <summary>
    /// A call that got no answer: the request failed or the call ran past its
    /// stage timeout. The exception is kept for the host log.
    /// </summary>
    public BregoFailure(string stage, Exception transport)
        : base($"Brego {stage} failed: transport={transport.GetType().Name}", transport)
    {
        Stage = stage;
        Detail = "transport=" + transport.GetType().Name;
        Transport = true;
    }

    public string Stage { get; }

    public HttpStatusCode? Status { get; }

    public string Detail { get; }

    /// <summary>Whether the call got no answer at all, as against an answer that was refused or unreadable.</summary>
    public bool Transport { get; }

    /// <summary>Whether the refusal is an authentication/authorization rejection even when reported as HTTP 400 (such as Cognito NotAuthorizedException).</summary>
    public bool Forbidden { get; }
}
