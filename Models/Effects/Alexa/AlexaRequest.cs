namespace Misfitz_Games.Models.Effects.Alexa;

public sealed class AlexaRequestEnvelope
{
    public AlexaDirective? Directive { get; set; }
}

public sealed class AlexaDirective
{
    public AlexaHeader? Header { get; set; }
    public AlexaEndpoint? Endpoint { get; set; }
    public object? Payload { get; set; }
}

public sealed class AlexaHeader
{
    public string Namespace { get; set; } = "";
    public string Name { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string PayloadVersion { get; set; } = "3";
    public string? CorrelationToken { get; set; }
}

public sealed class AlexaEndpoint
{
    public string EndpointId { get; set; } = "";
    public AlexaScope? Scope { get; set; }
}

public sealed class AlexaScope
{
    public string Type { get; set; } = "";
    public string Token { get; set; } = "";
}