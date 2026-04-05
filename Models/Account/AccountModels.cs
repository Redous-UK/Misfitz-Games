namespace Misfitz_Games.Models.Account
{
    public class AccountModels
    {

        public sealed record PortalStateDto(
    PortalUserDto User,
    PortalRoomDto Room,
    PortalPreferencesDto Preferences
);

        public sealed record PortalUserDto(
            string UserId,
            string Email,
            string DisplayName,
            string Username,
            string? Bio,
            string? AvatarUrl,
            bool IsProfilePublic,
            bool ShowAvatarInRoom,
            bool ShowOnlineStatus,
            string Role
        );

        public sealed record PortalRoomDto(
            string RoomId,
            string RoomName,
            string RoomSlug,
            string? Description,
            string DefaultGame,
            bool AutoRestore,
            bool AllowGuests,
            bool OverlaysEnabled,
            bool IsPrivate,
            string PortalPath
        );

        public sealed record PortalPreferencesDto(
            bool EmailAlerts,
            bool SecurityAlerts,
            bool GameReminders,
            string DigestFrequency,
            string Timezone,
            string Theme,
            string Accent,
            bool CompactLayout,
            bool ShowTips,
            bool PublicRoomListing,
            bool ShowGameplayStats
        );

        public sealed record SavePortalProfileRequest(
            string DisplayName,
            string Email,
            string Username,
            string? Bio,
            string? AvatarUrl,
            bool IsProfilePublic,
            bool ShowAvatarInRoom,
            bool ShowOnlineStatus
        );

        public sealed record SavePortalRoomRequest(
            string RoomName,
            string RoomSlug,
            string? Description,
            string DefaultGame,
            bool AutoRestore,
            bool AllowGuests,
            bool OverlaysEnabled,
            bool IsPrivate
        );

        public sealed record SavePortalPreferencesRequest(
            bool EmailAlerts,
            bool SecurityAlerts,
            bool GameReminders,
            string DigestFrequency,
            string Timezone,
            string Theme,
            string Accent,
            bool CompactLayout,
            bool ShowTips,
            bool PublicRoomListing,
            bool ShowGameplayStats
        );


    }
}
