# Service layer conventions (`Services/`)

Business logic is being progressively extracted from controllers into injectable per-feature services
(see "Split Controllers into smaller services" in [TODO.md](../../TODO.md)). `UsersController` /
`Services/Users/` is the first controller migrated — use it as the template for the next one.

## Structure

Each migrated feature gets its own folder under `Services/<Feature>/` (plural, matching the controller
name — e.g. `Services/Users/` for `UsersController`) containing exactly two files:

- `I<Feature>Service.cs` — the interface, plus any `Result` records/enums its write operations return
  (see below).
- `<Feature>Service.cs` — the implementation, taking over the `PinballPVPContext` and any other
  dependencies (e.g. `IPasswordHasher`) the controller previously held, injected via primary constructor.

Register the service as scoped in `Program.cs` alongside the other `AddScoped<I..., ...>()` calls.

**Naming note:** the folder/namespace is plural (`Services/Users`, not `Services/User`) to avoid a
namespace/type collision with the `Models.User` entity — a namespace and a type sharing the same
unqualified name (`User`) causes ambiguous-reference errors when both `Models` and the service namespace
are `using`'d in the same file.

## Read operations

Read methods return DTOs (or `PagedResult<Dto>`) directly — `null` for a not-found single-entity lookup,
which the controller maps to `NotFound()`. Same `.AsNoTracking()` + `.Select(Dto.Projection)` +
`ToPagedResultAsync` conventions as before, just moved into the service.

## Write operations — the Result pattern

Write methods that can fail for *expected* business reasons (uniqueness conflicts, not-found) don't
throw — they return a small `record Result(TError Error, TDto? Value)` per operation, e.g.
`CreateUserResult` / `UpdateNicknameResult` in `IUserService.cs`:

```csharp
public enum CreateUserError { None, UsernameInUse, NicknameInUse, EmailInUse, DuplicateDetails }

public record CreateUserResult(CreateUserError Error, UserResponseDto? User)
{
    public bool Succeeded => Error == CreateUserError.None;
    public static CreateUserResult Success(UserResponseDto user) => new(CreateUserError.None, user);
    public static CreateUserResult Failure(CreateUserError error) => new(error, null);
}
```

The controller switches on `result.Error` to pick the HTTP response (`BadRequest("...")`, `NotFound()`,
`CreatedAtAction(...)`, `Ok(result.Value)`) — the service owns *what* went wrong, the controller owns *how*
that's expressed over HTTP. Postgres unique-constraint violations (`DbUpdateException` /
`SqlState: "23505"`) caught during `SaveChangesAsync` are mapped to the same error enum by
`ConstraintName`, exactly as the pre-extraction controller code did.

## All-purpose `CancellationToken`

Following `IRefreshTokenService`'s precedent, every service method takes a trailing
`CancellationToken ct = default`, even though controller actions don't currently accept/forward one.
