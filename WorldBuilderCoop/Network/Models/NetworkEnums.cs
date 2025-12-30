namespace WorldBuilderCoop.Network
{
    public enum Packets
    {
        AssignID = 0,
        PlaceObject = 1,
        RemoveObjects = 2,
        UpdateObjects = 3,
        LoadMap = 4,
        AddToSelection = 5,
        PlayerSync = 6,
        RemoveFromSelection = 7,
        RemovePlayer = 8
    }

    public enum PacketDistribution
    {
        SendToAll = 1,
        SendToOthers = 2,
        SendToUser = 3,
    }
}