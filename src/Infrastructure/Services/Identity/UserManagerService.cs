using System.Data;
using System.Data.Common;
using Application.Common.Interfaces.Services.Identity;
using Application.Common.Interfaces.UnitOfWorks;
using Ardalis.GuardClauses;
using Dapper;
using Domain.Aggregates.Roles;
using Domain.Aggregates.Users;
using Domain.Aggregates.Users.Enums;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Infrastructure.Services.Identity;

public class UserManagerService(
    IRoleManagerService roleManagerService,
    IDbContext context,
    ILogger logger
) : IUserManagerService
{
    private readonly DbSet<UserRole> userRoleContext = context.Set<UserRole>();
    public DbSet<UserRole> UserRoles => userRoleContext;

    private readonly DbSet<Role> roleContext = context.Set<Role>();
    public DbSet<Role> Roles => roleContext;

    private readonly DbSet<UserClaim> userClaimsContext = context.Set<UserClaim>();
    public DbSet<UserClaim> UserClaims => userClaimsContext;

    private readonly DbSet<User> userContext = context.Set<User>();

    public async Task CreateUserAsync(
        User user,
        IEnumerable<Ulid> roleIds,
        IEnumerable<UserClaim>? claims,
        DbTransaction? transaction = null
    )
    {
        try
        {
            if (transaction == null)
            {
                await context.DatabaseFacade.BeginTransactionAsync();
            }
            else
            {
                await context.UseTransactionAsync(transaction);
            }

            await AddRoleToUserAsync(user, roleIds);
            await AddClaimsToUserAsync(user, claims ?? []);

            if (transaction == null)
            {
                await context.DatabaseFacade.CommitTransactionAsync();
            }
        }
        catch (Exception ex)
        {
            logger.Error(
                ex,
                "Error in method {MethodName}. Exception Type: {ExceptionType}. Error Message: {ErrorMessage}. StackTrace: {StackTrace}",
                nameof(CreateUserAsync),
                ex.GetType().Name,
                ex.Message,
                ex.StackTrace
            );

            if (transaction == null)
            {
                await context.DatabaseFacade.RollbackTransactionAsync();
            }
            throw;
        }
    }

    public async Task UpdateUserAsync(
        User user,
        IEnumerable<Ulid>? roleIds,
        IEnumerable<UserClaim>? claims,
        DbTransaction? transaction = null
    )
    {
        try
        {
            if (transaction == null)
            {
                await context.DatabaseFacade.BeginTransactionAsync();
            }
            else
            {
                await context.UseTransactionAsync(transaction);
            }

            // update role for user
            await UpdateRolesToUserAsync(user, roleIds);

            // update default user claim
            // var defautClaims = userClaimsContext
            //     .Where(x => x.UserId == user.Id && x.Type == KindaUserClaimType.Default)
            //     .ToDictionary(x => x.ClaimType, x => x.ClaimValue);
            // await ReplaceDefaultClaimsToUserAsync(user, defautClaims);

            // update custom user claim
            await UpdateClaimsToUserAsync(user, claims);

            if (transaction == null)
            {
                await context.DatabaseFacade.CommitTransactionAsync();
            }
        }
        catch (Exception ex)
        {
            logger.Error(
                ex,
                "Error in method {MethodName}. Exception Type: {ExceptionType}. Error Message: {ErrorMessage}. StackTrace: {StackTrace}",
                nameof(UpdateUserAsync),
                ex.GetType().Name,
                ex.Message,
                ex.StackTrace
            );

            if (transaction == null)
            {
                await context.DatabaseFacade.RollbackTransactionAsync();
            }
            throw;
        }
    }

    public async Task AddRoleToUserAsync(User user, IEnumerable<Ulid> roleIds)
    {
        if (!roleIds.Any())
        {
            return;
        }

        User? currentUser = await userContext
            .Where(x => x.Id == user.Id)
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync();

        Guard.Against.NotFound($"{user.Id}", currentUser, nameof(user));

        if (await roleContext.CountAsync(x => roleIds.Contains(x.Id)) != roleIds.Count())
        {
            throw new ArgumentException($"{nameof(roleIds)} is not found", nameof(roleIds));
        }

        if (await userRoleContext.AnyAsync(x => x.UserId == user.Id && roleIds.Contains(x.RoleId)))
        {
            throw new ArgumentException($"{nameof(roleIds)} is existence in user", nameof(roleIds));
        }

        IEnumerable<UserRole> currentUserRoles = currentUser.UserRoles!;
        IEnumerable<Ulid> rolesToInsert = roleIds.Where(x =>
            !currentUserRoles.Any(p => p.RoleId == x)
        );

        await userRoleContext.AddRangeAsync(
            rolesToInsert.Select(x => new UserRole { RoleId = x, UserId = currentUser.Id })
        );
        await context.SaveChangesAsync();

        //derive all role claims for users if user is assigned specific role.
        IEnumerable<RoleClaim> roleClaims = await roleManagerService.GetClaimsByRolesAsync(
            rolesToInsert
        );
        IEnumerable<UserClaim> userClaims = roleClaims.Select(x => new UserClaim
        {
            UserId = user.Id,
            ClaimType = x.ClaimType,
            ClaimValue = x.ClaimValue,
            RoleClaimId = x.Id,
            Type = KindaUserClaimType.Custom,
        });

        await userClaimsContext.AddRangeAsync(userClaims);
        await context.SaveChangesAsync();
    }

    public async Task UpdateRolesToUserAsync(User user, IEnumerable<Ulid>? roleIds)
    {
        if (roleIds == null || !roleIds.Any())
        {
            return;
        }

        User currentUser = Guard.Against.NotFound(
            $"{user.Id}",
            await userContext
                .Where(x => x.Id == user.Id)
                .Include(x => x.UserRoles)
                .FirstOrDefaultAsync(),
            nameof(user)
        );

        if (await roleContext.CountAsync(x => roleIds.Contains(x.Id)) != roleIds.Count())
        {
            throw new ArgumentException($"{nameof(roleIds)} is invalid");
        }

        List<UserRole> currentUserRoles = currentUser.UserRoles.AsList();

        IEnumerable<Ulid> rolesToRemove = currentUserRoles
            .FindAll(x => !roleIds.Contains(x.RoleId))
            .Select(x => x.RoleId);
        IEnumerable<Ulid> rolesToInsert = roleIds.Where(x =>
            !currentUserRoles.Exists(p => p.RoleId == x)
        );

        await RemoveRoleFromUserAsync(currentUser, rolesToRemove);
        await AddRoleToUserAsync(currentUser, rolesToInsert);
    }

    public async Task RemoveRoleFromUserAsync(User user, IEnumerable<Ulid> roleIds)
    {
        if (!roleIds.Any())
        {
            return;
        }

        User currentUser = Guard.Against.NotFound(
            $"{user.Id}",
            await userContext
                .Where(x => x.Id == user.Id)
                .Include(x => x.UserRoles)!
                .ThenInclude(x => x.Role)
                .ThenInclude(x => x!.RoleClaims)
                .ThenInclude(x => x.UserClaims)
                .AsSplitQuery()
                .FirstOrDefaultAsync(),
            nameof(user)
        );

        if (await roleContext.CountAsync(x => roleIds.Contains(x.Id)) != roleIds.Count())
        {
            throw new ArgumentException($"{nameof(roleIds)} is invalid");
        }

        List<UserRole> currentUserRoles = currentUser.UserRoles.AsList();
        if (roleIds.Any(x => !currentUserRoles.Exists(p => p.RoleId == x)))
        {
            throw new ArgumentException(
                $"{nameof(roleIds)} is not existed in user {nameof(user.Id)}"
            );
        }

        List<UserRole> userRoles = currentUserRoles.FindAll(x => roleIds.Contains(x.RoleId));

        IEnumerable<UserClaim> userClaims = userRoles
            .Select(x => x.Role)
            .SelectMany(x => x!.RoleClaims)
            .SelectMany(x => x.UserClaims!);

        userRoleContext.RemoveRange(userRoles);
        await context.SaveChangesAsync();
        userClaimsContext.RemoveRange(userClaims);
        await context.SaveChangesAsync();
    }

    public async Task AddClaimsToUserAsync(User user, IEnumerable<UserClaim> claims)
    {
        if (claims == null || !claims.Any())
        {
            return;
        }
        _ = await GetUserAsync(user.Id);

        // IEnumerable<UserClaim> userClaims = claims.Select(x => new UserClaim
        // {
        //     ClaimType = x.ClaimType!,
        //     ClaimValue = x.ClaimValue!,
        //     UserId = currentUser.Id,
        //     Type = x.Type,
        // });

        await userClaimsContext.AddRangeAsync(claims);
        await context.SaveChangesAsync();
    }

    public async Task UpdateClaimsToUserAsync(User user, IEnumerable<UserClaim>? claims)
    {
        if (claims == null || !claims.Any())
        {
            return;
        }

        User? currentUser = Guard.Against.NotFound(
            $"{user}",
            await userContext
                .Where(x => x.Id == user.Id)
                .Include(x => x.UserClaims)
                .FirstOrDefaultAsync(),
            nameof(user)
        );

        List<UserClaim> customUserClaims = currentUser
            .UserClaims.AsList()
            .FindAll(x => x.Type == KindaUserClaimType.Custom);
        List<UserClaim> claimsToProcess = claims.AsList();

        IEnumerable<UserClaim> claimsToInsert = claimsToProcess.FindAll(x =>
            !customUserClaims.Exists(p => p.Id == x.Id)
        );
        List<UserClaim> claimsToUpdate = claimsToProcess.FindAll(x =>
            customUserClaims.Exists(p => p.Id == x.Id)
        );

        IEnumerable<UserClaim> claimsToRemove = customUserClaims.FindAll(x =>
            !claimsToProcess.Exists(p => p.Id == x.Id)
        );

        for (int i = 0; i < claimsToUpdate.Count; i++)
        {
            UserClaim claim = claimsToUpdate[i];
            var correspondenceClaim = claimsToProcess.Find(x => x.Id == claim.Id);

            if (correspondenceClaim == null)
            {
                continue;
            }
            claim.ClaimValue = correspondenceClaim.ClaimValue!;
        }

        await RemoveClaimsToUserAsync(currentUser, claimsToRemove);
        userClaimsContext.UpdateRange(claimsToUpdate);
        await context.SaveChangesAsync();
        await AddClaimsToUserAsync(currentUser, claimsToInsert);
    }

    public async Task RemoveClaimsToUserAsync(User user, IEnumerable<Ulid> claimIds)
    {
        if (!claimIds.Any())
        {
            return;
        }

        User currentUser = Guard.Against.NotFound(
            $"{user.Id}",
            await userContext
                .Where(x => x.Id == user.Id)
                .Include(x => x.UserClaims!.Where(p => p.Type == KindaUserClaimType.Custom))
                .FirstOrDefaultAsync(),
            nameof(user)
        );

        List<UserClaim> currentUserClaims = currentUser.UserClaims.AsList();
        if (claimIds.Any(x => !currentUserClaims.Exists(p => p.Id == x)))
        {
            throw new ArgumentException(
                $"{nameof(claimIds)} is not existed in user {nameof(user.Id)}."
            );
        }

        IEnumerable<UserClaim> claimsToRemove = currentUserClaims.FindAll(x =>
            claimIds.Contains(x.Id)
        );

        userClaimsContext.RemoveRange(claimsToRemove);
        await context.SaveChangesAsync();
    }

    public async Task RemoveClaimsToUserAsync(User user, IEnumerable<UserClaim> claims) =>
        await RemoveClaimsToUserAsync(user, claims.Select(x => x.Id));

    public async Task<IEnumerable<Role>> GetRolesInUser(Ulid userId) =>
        await userRoleContext.Where(x => x.UserId == userId).Select(x => x.Role!).ToListAsync();

    public async Task<IEnumerable<UserClaim>> GetClaimsInUser(Ulid userId) =>
        await userClaimsContext.Where(x => x.UserId == userId).ToListAsync();

    public async Task<bool> HasRolesInUserAsync(Ulid id, IEnumerable<string> roleNames) =>
        await userContext.AnyAsync(x =>
            x.Id == id && x.UserRoles!.Any(p => roleNames.Contains(p.Role!.Name))
        );

    public async Task<bool> HasClaimsInUserAsync(
        Ulid id,
        IEnumerable<KeyValuePair<string, string>> claims
    )
    {
        List<UserClaim> userClaims = await userContext
            .Where(x => x.Id == id)
            .SelectMany(x => x.UserClaims!)
            .ToListAsync();

        return userClaims.Any(x => claims.Contains(new(x.ClaimType, x.ClaimValue)));
    }

    public async Task<bool> HasClaimsAndRoleInUserAsync(
        Ulid id,
        IEnumerable<string> roles,
        IEnumerable<KeyValuePair<string, string>> claims
    )
    {
        bool isHaRole = await HasRolesInUserAsync(id, roles);
        bool isHasClaim = await HasClaimsInUserAsync(id, claims);

        return isHaRole && isHasClaim;
    }

    private async Task<User> GetUserAsync(Ulid id) =>
        Guard.Against.NotFound(
            $"{id}",
            await userContext.Where(x => x.Id == id).FirstOrDefaultAsync(),
            nameof(id)
        );
}
