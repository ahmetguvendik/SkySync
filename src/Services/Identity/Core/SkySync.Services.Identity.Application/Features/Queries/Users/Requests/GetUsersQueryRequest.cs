using MediatR;
using SkySync.Services.Identity.Application.Features.Queries.Users.Responses;

namespace SkySync.Services.Identity.Application.Features.Queries.Users.Requests;

public class GetUsersQueryRequest : IRequest<GetUsersQueryResponse>
{
    private int _page = 1;
    private int _pageSize = 10;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (value < 1) _pageSize = 10;
            else if (value > 100) _pageSize = 100;
            else _pageSize = value;
        }
    }
}
