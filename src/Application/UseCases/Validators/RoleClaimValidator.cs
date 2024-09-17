using Application.UseCases.Projections.Roles;
using Contracts.Common.Messages;
using FluentValidation;

namespace Application.UseCases.Validators;

public class RoleClaimValidator : AbstractValidator<RoleClaimModel>
{
    public RoleClaimValidator()
    {
        RuleFor(x => x.ClaimType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithState(x =>
                Messager
                    .Create<RoleClaimModel>(nameof(RoleModel.Claims))
                    .Property(x => x.ClaimType!)
                    .Message(MessageType.Null)
                    .Negative()
                    .Build()
            );

        RuleFor(x => x.ClaimValue)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithState(x =>
                Messager
                    .Create<RoleClaimModel>(nameof(RoleModel.Claims))
                    .Property(x => x.ClaimValue!)
                    .Message(MessageType.Null)
                    .Negative()
                    .Build()
            );
    }
}
