using FluentValidation;
using Mediator;
using Vitta.Application.Abstractions.Persistence;
using Vitta.Domain.Common;
using Vitta.Domain.Insurers;

namespace Vitta.Application.Features.Insurers;

public sealed record InsurerDto(Guid Id, string Code, string Name);

public sealed record GetInsurersQuery : IQuery<IReadOnlyList<InsurerDto>>;

public sealed class GetInsurersHandler(IInsurerRepository insurers) : IQueryHandler<GetInsurersQuery, IReadOnlyList<InsurerDto>>
{
    public async ValueTask<IReadOnlyList<InsurerDto>> Handle(GetInsurersQuery query, CancellationToken ct) =>
        (await insurers.ListActiveAsync(ct)).Select(i => new InsurerDto(i.Id, i.Code, i.Name)).ToList();
}

public sealed record CreateInsurerCommand(string Code, string Name) : ICommand<Result<Guid>>;

internal sealed class CreateInsurerValidator : AbstractValidator<CreateInsurerCommand>
{
    public CreateInsurerValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9_-]+$").WithName("Código");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120).WithName("Nome");
    }
}

public sealed class CreateInsurerHandler(IInsurerRepository insurers, IUnitOfWork uow) : ICommandHandler<CreateInsurerCommand, Result<Guid>>
{
    public async ValueTask<Result<Guid>> Handle(CreateInsurerCommand c, CancellationToken ct)
    {
        var insurer = Insurer.Create(c.Code, c.Name);
        if (await insurers.CodeExistsAsync(insurer.Code, ct))
        {
            return Insurer.CodeAlreadyExists;
        }

        insurers.Add(insurer);
        await uow.SaveChangesAsync(ct);
        return insurer.Id;
    }
}
