using System;
using MediatR;
using SkySync.Services.Identity.Application.Features.Queries.Users.Requests;
using SkySync.Services.Identity.Application.Features.Queries.Users.Responses;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Domain.Constants;
using System.Linq;

namespace SkySync.Services.Identity.Application.Features.Handlers.Users;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQueryRequest, GetUsersQueryResponse>
{
    private readonly IUserRepository _userRepository;

    public GetUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetUsersQueryResponse> Handle(GetUsersQueryRequest request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;

        var (users, totalCount) = await _userRepository.GetPagedAsync(page, pageSize, cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = users
            .Select(u => new UserSummaryDto(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role?.Name ?? RoleConstants.User,
                u.IsEmailConfirmed,
                u.CreatedTime))
            .ToList();

        return new GetUsersQueryResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}
