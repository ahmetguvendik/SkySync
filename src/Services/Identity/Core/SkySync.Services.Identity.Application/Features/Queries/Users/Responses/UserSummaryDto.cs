using System;
using System.Collections.Generic;

namespace SkySync.Services.Identity.Application.Features.Queries.Users.Responses;

public record UserSummaryDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsEmailConfirmed,
    DateTime CreatedTime);

public class GetUsersQueryResponse
{
    public IReadOnlyList<UserSummaryDto> Items { get; init; } = Array.Empty<UserSummaryDto>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
