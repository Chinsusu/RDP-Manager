namespace RdpManager.Models
{
    public enum AppSection
    {
        Connections,
        CloudminiSync,
        Settings
    }

    public enum NavigationFilter
    {
        AllConnections,
        Favorites,
        Recent
    }

    public enum PlatformFilter
    {
        All,
        Windows,
        Linux
    }

    public enum StatusFilter
    {
        All,
        Online,
        Offline,
        Other
    }
}
