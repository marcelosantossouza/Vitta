using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Clients;
using Vitta.Domain.Common;

namespace Vitta.Application.Features.Clients.CreateClient;

public sealed record CreateClientCommand(string FullName, string Cpf, DateOnly? BirthDate, string? Email, string? Phone)
    : ICommand<Result<Guid>>;

internal sealed class CreateClientValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Cpf).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}

public sealed class CreateClientHandler(IClientRepository clients, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateClientCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var result = Client.Create(command.FullName, command.Cpf, command.BirthDate, command.Email, command.Phone);
        if (result.IsFailure)
        {
            return result.Error;
        }

        if (await clients.ExistsByCpfAsync(result.Value.Cpf, cancellationToken))
        {
            return ClientErrors.CpfAlreadyExists;
        }

        clients.Add(result.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result.Value.Id;
    }
}
