using System;

namespace SkySync.Services.Identity.Domain.Constants;

public static class RoleConstants
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly Guid AdminRoleId = Guid.Parse("44E54B9F-0B4A-4FB6-8AC2-08F3AD85D3F1");
    public static readonly Guid UserRoleId = Guid.Parse("6BE1578A-92C4-4A2D-9203-13DCF124BCAF");
}
