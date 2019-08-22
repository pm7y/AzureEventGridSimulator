using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Domain.Commands
{
    public class ValidateSubscriptionCommandHandler : IRequestHandler<ValidateSubscriptionCommand, bool>
    {
        private readonly ILogger _logger;

        public ValidateSubscriptionCommandHandler(ILogger logger)
        {
            _logger = logger;
        }

        public Task<bool> Handle(ValidateSubscriptionCommand request, CancellationToken cancellationToken)
        {
            // TODO Actually validate this properly.

            var validCode = new Guid("d9ebebb7-f884-40b5-8c0d-9ec72f6f19cd");
            return Task.FromResult(request.ValidationCode == validCode);
        }
    }
}
