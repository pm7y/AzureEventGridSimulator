using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Domain.Commands
{
    // ReSharper disable once UnusedType.Global
    public class ValidateSubscriptionCommandHandler : IRequestHandler<ValidateSubscriptionCommand, bool>
    {
        private readonly ILogger<ValidateSubscriptionCommandHandler> _logger;

        public ValidateSubscriptionCommandHandler(ILogger<ValidateSubscriptionCommandHandler> logger)
        {
            _logger = logger;
        }

        public Task<bool> Handle(ValidateSubscriptionCommand request, CancellationToken cancellationToken)
        {
            var subscriber = request.Topic.Subscribers.FirstOrDefault(s => s.ValidationCode == request.ValidationCode);

            if (subscriber != null &&
                subscriber.ValidationCode == request.ValidationCode &&
                !subscriber.ValidationPeriodExpired)
            {
                subscriber.ValidationStatus = SubscriptionValidationStatus.ValidationSuccessful;
                _logger.LogInformation("Subscription {SubscriptionName} on topic {TopicName} was successfully validated", subscriber.Name, request.Topic.Name);

                return Task.FromResult(true);
            }

            _logger.LogWarning("Validation failed for code {ValidationCode} on topic {TopicName}", request.ValidationCode, request.Topic?.Name);
            return Task.FromResult(false);
        }
    }
}
