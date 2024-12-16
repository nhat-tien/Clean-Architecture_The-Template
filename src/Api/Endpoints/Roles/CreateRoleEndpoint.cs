using Application.UseCases.Roles.Commands.Create;
using Ardalis.ApiEndpoints;
using Contracts.ApiWrapper;
using Contracts.RouteResults;
using Contracts.Routers;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Endpoints.Roles;

public class CreateRoleEndpoint(ISender sender)
    : EndpointBaseAsync.WithRequest<CreateRoleCommand>.WithActionResult<ApiResponse>
{
    [HttpPost(Router.RoleRoute.Roles)]
    [SwaggerOperation(Tags = [Router.RoleRoute.Tags], Summary = "create Role")]
    public override async Task<ActionResult<ApiResponse>> HandleAsync(
        CreateRoleCommand request,
        CancellationToken cancellationToken = default
    )
    {
        CreateRoleResponse role = await sender.Send(request, cancellationToken);
        return this.Created201(Router.RoleRoute.GetRouteName, role.Id, role);
    }
}
