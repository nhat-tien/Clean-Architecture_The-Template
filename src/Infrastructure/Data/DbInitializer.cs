using Application.Common.Interfaces.Repositories;
using Application.Common.Interfaces.Services.Identity;
using Domain.Aggregates.Users;
using Domain.Aggregates.Users.Enums;
using Infrastructure.Constants;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Infrastructure.Data;

public class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider provider)
    {
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var roleManagerService = provider.GetRequiredService<IRoleManagerService>();
        var userManagerService = provider.GetRequiredService<IUserManagerService>();
        var logger = provider.GetRequiredService<ILogger>();

        if (await unitOfWork.Repository<User>().AnyAsync(x => true))
        {
            return;
        }

        logger.Information("Seeding data is starting.............");
        try
        {
            await unitOfWork.CreateTransactionAsync();

            User user =
                new(
                    "Chloe",
                    "Kim",
                    "chloe.kim",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "chloe.kim@gmail.com",
                    "0925123123"
                )
                {
                    Address = "NYC",
                    DayOfBirth = new DateTime(1990, 10, 1),
                    Status = UserStatus.Active,
                };

            Role role =
                new()
                {
                    Id = Ulid.Parse(Credential.ADMIN_ROLE_ID),
                    Name = Credential.ADMIN_ROLE,
                    RoleClaims = Credential
                        .CLAIMS.Select(x => new RoleClaim
                        {
                            ClaimType = x.Key,
                            ClaimValue = x.Value,
                        })
                        .ToList(),
                };

            await unitOfWork.Repository<User>().AddAsync(user);
            await unitOfWork.SaveAsync();

            await roleManagerService.CreateRoleAsync(role);

            // add roles and claims to user
            await userManagerService.CreateUserAsync(
                user,
                [role.Id],
                user.GetUserClaims(),
                unitOfWork.Transaction
            );

            User[] users =
            [
                new(
                    "John",
                    "Doe",
                    "john.doe",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "john.doe@example.com",
                    "0803456789"
                )
                {
                    Address = "Los Angeles",
                    DayOfBirth = new DateTime(1985, 4, 23),
                    Status = UserStatus.Active,
                },
                new(
                    "Alice",
                    "Smith",
                    "alice.smith",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "alice.smith@example.com",
                    "0912345678"
                )
                {
                    Address = "Chicago",
                    DayOfBirth = new DateTime(1992, 7, 19),
                    Status = UserStatus.Inactive,
                },
                new(
                    "Bob",
                    "Johnson",
                    "bob.johnson",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "bob.johnson@example.com",
                    "0934567890"
                )
                {
                    Address = "San Francisco",
                    DayOfBirth = new DateTime(1980, 3, 15),
                    Status = UserStatus.Active,
                },
                new(
                    "Emily",
                    "Brown",
                    "emily.brown",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "emily.brown@example.com",
                    "0945678901"
                )
                {
                    Address = "Seattle",
                    DayOfBirth = new DateTime(1995, 5, 5),
                    Status = UserStatus.Active,
                },
                new(
                    "James",
                    "Williams",
                    "james.williams",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "james.williams@example.com",
                    "0978901234"
                )
                {
                    Address = "Houston",
                    DayOfBirth = new DateTime(1983, 11, 9),
                    Status = UserStatus.Active,
                },
                new(
                    "Olivia",
                    "Taylor",
                    "olivia.taylor",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "olivia.taylor@example.com",
                    "0989012345"
                )
                {
                    Address = "Miami",
                    DayOfBirth = new DateTime(1998, 2, 18),
                    Status = UserStatus.Active,
                },
                new(
                    "Daniel",
                    "Lee",
                    "daniel.lee",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "daniel.lee@example.com",
                    "0901234567"
                )
                {
                    Address = "Boston",
                    DayOfBirth = new DateTime(1987, 9, 21),
                    Status = UserStatus.Inactive,
                },
                new(
                    "Sophia",
                    "Garcia",
                    "sophia.garcia",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "sophia.garcia@example.com",
                    "0912345679"
                )
                {
                    Address = "Dallas",
                    DayOfBirth = new DateTime(1994, 12, 12),
                    Status = UserStatus.Active,
                },
                new(
                    "Michael",
                    "Martinez",
                    "michael.martinez",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "michael.martinez@example.com",
                    "0913456789"
                )
                {
                    Address = "Phoenix",
                    DayOfBirth = new DateTime(1978, 8, 8),
                    Status = UserStatus.Inactive,
                },
                new(
                    "Isabella",
                    "Harris",
                    "isabella.harris",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "isabella.harris@example.com",
                    "0945678902"
                )
                {
                    Address = "San Diego",
                    DayOfBirth = new DateTime(1991, 1, 1),
                    Status = UserStatus.Active,
                },
                new(
                    "David",
                    "Clark",
                    "david.clark",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "david.clark@example.com",
                    "0934567891"
                )
                {
                    Address = "Austin",
                    DayOfBirth = new DateTime(1984, 6, 6),
                    Status = UserStatus.Active,
                },
                new(
                    "Emma",
                    "Rodriguez",
                    "emma.rodriguez",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "emma.rodriguez@example.com",
                    "0956789012"
                )
                {
                    Address = "Denver",
                    DayOfBirth = new DateTime(1993, 3, 3),
                    Status = UserStatus.Active,
                },
                new(
                    "Andrew",
                    "Moore",
                    "andrew.moore",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "andrew.moore@example.com",
                    "0923456789"
                )
                {
                    Address = "Portland",
                    DayOfBirth = new DateTime(1981, 10, 30),
                    Status = UserStatus.Inactive,
                },
                new(
                    "Ava",
                    "Jackson",
                    "ava.jackson",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "ava.jackson@example.com",
                    "0935678903"
                )
                {
                    Address = "Las Vegas",
                    DayOfBirth = new DateTime(2000, 4, 14),
                    Status = UserStatus.Active,
                },
                new(
                    "Joshua",
                    "White",
                    "joshua.white",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "joshua.white@example.com",
                    "0914567890"
                )
                {
                    Address = "Orlando",
                    DayOfBirth = new DateTime(1986, 11, 17),
                    Status = UserStatus.Inactive,
                },
                new(
                    "Charlotte",
                    "Thomas",
                    "charlotte.thomas",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "charlotte.thomas@example.com",
                    "0934567892"
                )
                {
                    Address = "New Orleans",
                    DayOfBirth = new DateTime(1997, 7, 7),
                    Status = UserStatus.Active,
                },
                new(
                    "Ethan",
                    "King",
                    "ethan.king",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "ethan.king@example.com",
                    "0923456781"
                )
                {
                    Address = "Philadelphia",
                    DayOfBirth = new DateTime(1999, 9, 9),
                    Status = UserStatus.Active,
                },
                new(
                    "Abigail",
                    "Scott",
                    "abigail.scott",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "abigail.scott@example.com",
                    "0916789013"
                )
                {
                    Address = "San Antonio",
                    DayOfBirth = new DateTime(1989, 2, 2),
                    Status = UserStatus.Active,
                },
                new(
                    "Liam",
                    "Perez",
                    "liam.perez",
                    HashPassword(Credential.USER_DEFAULT_PASSWORD),
                    "liam.perez@example.com",
                    "0909876543"
                )
                {
                    Address = "Atlanta",
                    DayOfBirth = new DateTime(1988, 12, 25),
                    Status = UserStatus.Inactive,
                },
            ];

            await unitOfWork.Repository<User>().AddRangeAsync(users);
            await unitOfWork.SaveAsync();
            
            foreach (var user1 in users)
            {
                await userManagerService.CreateUserAsync(
                    user1,
                    [role.Id],
                    user1.GetUserClaims(),
                    unitOfWork.Transaction
                );
            }

            logger.Information("Seeding data has done.............");
            await unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackAsync();
            logger.Information("error had occured while seeding data with {message}", ex);
            throw;
        }
    }
}
