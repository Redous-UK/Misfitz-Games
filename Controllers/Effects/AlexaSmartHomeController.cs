using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Misfitz_Games.Services.Effects;

namespace Misfitz_Games.Controllers.Effects
{
    [ApiController]
    [Route("api/alexa/smarthome")]
    public sealed class AlexaSmartHomeController(
        EffectsEngine engine,
        ILogger<AlexaSmartHomeController> log) : ControllerBase
    {
        private readonly EffectsEngine _engine = engine;
        private readonly ILogger<AlexaSmartHomeController> _log = log;

        [HttpPost]
        public async Task<IActionResult> Post(
            [FromBody] AlexaRequestEnvelope? envelope,
            CancellationToken ct)
        {
            if (envelope?.Directive?.Header == null)
                return BadRequest("Missing Alexa directive header.");

            var header = envelope.Directive.Header;
            var directiveNamespace = header.Namespace ?? string.Empty;
            var directiveName = header.Name ?? string.Empty;

            _log.LogInformation(
                "Alexa directive received: {Namespace}.{Name}",
                directiveNamespace,
                directiveName);

            if (directiveNamespace == "Alexa.Discovery" && directiveName == "Discover")
            {
                return Ok(BuildDiscoveryResponse());
            }

            if (directiveNamespace == "Alexa.SceneController" && directiveName == "Activate")
            {
                var endpointId = envelope.Directive.Endpoint?.EndpointId ?? string.Empty;

                if (string.IsNullOrWhiteSpace(endpointId))
                {
                    return Ok(BuildErrorResponse(
                        header.CorrelationToken,
                        "INVALID_DIRECTIVE",
                        "Missing endpointId."));
                }

                await _engine.RunSceneAsync(endpointId, ct);

                return Ok(BuildSceneActivationResponse(
                    header.CorrelationToken,
                    endpointId));
            }

            return Ok(BuildErrorResponse(
                header.CorrelationToken,
                "INVALID_DIRECTIVE",
                $"Unsupported directive: {directiveNamespace}.{directiveName}"));
        }

        private static AlexaResponseEnvelope BuildDiscoveryResponse()
        {
            return new AlexaResponseEnvelope
            {
                Event = new AlexaResponseEvent
                {
                    Header = new AlexaResponseHeader
                    {
                        Namespace = "Alexa.Discovery",
                        Name = "Discover.Response",
                        PayloadVersion = "3",
                        MessageId = Guid.NewGuid().ToString("N")
                    },
                    Payload = new AlexaDiscoveryPayload
                    {
                        Endpoints =
                        [
                            BuildSceneEndpoint("battle_mode", "Battle Mode", "Misfitz battle lighting"),
                            BuildSceneEndpoint("victory_flash", "Victory Flash", "Misfitz victory lighting"),
                            BuildSceneEndpoint("defeat_fade", "Defeat Fade", "Misfitz defeat lighting"),
                            BuildSceneEndpoint("hype_mode", "Hype Mode", "Misfitz hype lighting"),
                            BuildSceneEndpoint("all_off", "All Off", "Turn all stream lights off")
                        ]
                    }
                }
            };
        }

        private static AlexaDiscoveryEndpoint BuildSceneEndpoint(
            string endpointId,
            string friendlyName,
            string description)
        {
            return new AlexaDiscoveryEndpoint
            {
                EndpointId = endpointId,
                ManufacturerName = "Misfitz Games",
                FriendlyName = friendlyName,
                Description = description,
                DisplayCategories = ["SCENE_TRIGGER"],
                Cookie = [],
                Capabilities =
                [
                    new AlexaCapability
                    {
                        Type = "AlexaInterface",
                        Interface = "Alexa",
                        Version = "3"
                    },
                    new AlexaCapability
                    {
                        Type = "AlexaInterface",
                        Interface = "Alexa.SceneController",
                        Version = "3",
                        SupportsDeactivation = false,
                        ProactivelyReported = false
                    }
                ]
            };
        }

        private static AlexaResponseEnvelope BuildSceneActivationResponse(
            string? correlationToken,
            string endpointId)
        {
            return new AlexaResponseEnvelope
            {
                Context = new AlexaContext
                {
                    Properties = []
                },
                Event = new AlexaResponseEvent
                {
                    Header = new AlexaResponseHeader
                    {
                        Namespace = "Alexa",
                        Name = "Response",
                        PayloadVersion = "3",
                        MessageId = Guid.NewGuid().ToString("N"),
                        CorrelationToken = correlationToken
                    },
                    Endpoint = new AlexaResponseEndpoint
                    {
                        EndpointId = endpointId
                    },
                    Payload = new Dictionary<string, object>()
                }
            };
        }

        private static AlexaResponseEnvelope BuildErrorResponse(
            string? correlationToken,
            string errorType,
            string message)
        {
            return new AlexaResponseEnvelope
            {
                Event = new AlexaResponseEvent
                {
                    Header = new AlexaResponseHeader
                    {
                        Namespace = "Alexa",
                        Name = "ErrorResponse",
                        PayloadVersion = "3",
                        MessageId = Guid.NewGuid().ToString("N"),
                        CorrelationToken = correlationToken
                    },
                    Payload = new AlexaErrorPayload
                    {
                        Type = errorType,
                        Message = message
                    }
                }
            };
        }
    }

    // =========================
    // Incoming Alexa request DTOs
    // =========================

    public sealed class AlexaRequestEnvelope
    {
        public AlexaDirective? Directive { get; set; }
    }

    public sealed class AlexaDirective
    {
        public AlexaRequestHeader? Header { get; set; }
        public AlexaRequestEndpoint? Endpoint { get; set; }
        public object? Payload { get; set; }
    }

    public sealed class AlexaRequestHeader
    {
        public string? Namespace { get; set; }
        public string? Name { get; set; }
        public string? MessageId { get; set; }
        public string? PayloadVersion { get; set; }
        public string? CorrelationToken { get; set; }
    }

    public sealed class AlexaRequestEndpoint
    {
        public string? EndpointId { get; set; }
        public AlexaScope? Scope { get; set; }
    }

    public sealed class AlexaScope
    {
        public string? Type { get; set; }
        public string? Token { get; set; }
    }

    // =========================
    // Outgoing Alexa response DTOs
    // =========================

    public sealed class AlexaResponseEnvelope
    {
        public AlexaContext? Context { get; set; }
        public AlexaResponseEvent? Event { get; set; }
    }

    public sealed class AlexaContext
    {
        public List<object>? Properties { get; set; }
    }

    public sealed class AlexaResponseEvent
    {
        public AlexaResponseHeader? Header { get; set; }
        public AlexaResponseEndpoint? Endpoint { get; set; }
        public object? Payload { get; set; }
    }

    public sealed class AlexaResponseHeader
    {
        public string? Namespace { get; set; }
        public string? Name { get; set; }
        public string? PayloadVersion { get; set; }
        public string? MessageId { get; set; }
        public string? CorrelationToken { get; set; }
    }

    public sealed class AlexaResponseEndpoint
    {
        public string? EndpointId { get; set; }
    }

    public sealed class AlexaDiscoveryPayload
    {
        public List<AlexaDiscoveryEndpoint>? Endpoints { get; set; }
    }

    public sealed class AlexaDiscoveryEndpoint
    {
        public string? EndpointId { get; set; }
        public string? ManufacturerName { get; set; }
        public string? FriendlyName { get; set; }
        public string? Description { get; set; }
        public List<string>? DisplayCategories { get; set; }
        public Dictionary<string, string>? Cookie { get; set; }
        public List<AlexaCapability>? Capabilities { get; set; }
    }

    public sealed class AlexaCapability
    {
        public string? Type { get; set; }
        public string? Interface { get; set; }
        public string? Version { get; set; }
        public bool? SupportsDeactivation { get; set; }
        public bool? ProactivelyReported { get; set; }
    }

    public sealed class AlexaErrorPayload
    {
        public string? Type { get; set; }
        public string? Message { get; set; }
    }
}