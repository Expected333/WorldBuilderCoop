I will fix the map loading system to work with Local mode as well.

**Analysis:**
The user's previous issue "[SteamNetwork] Empty data" likely came from an attempted fix or existing logic where data wasn't being correctly passed or serialized in Local mode.
The core issue is that `BlEditorManagerLoadMap_Patch.Postfix` currently checks `SteamNetworkManager.Instance.IsConnected`. With my previous fix to `IsConnected` (handling Local mode), this check passes. However, the serialization or sending logic might need verification for Local mode.
The user specifically asked: "le system de load map fonctionne qu'avec le network steam ? si c'est le cas fait le fonctionner dans le mode local aussi stp".
Actually, `SteamNetworkManager.SendToAll` *does* handle Local mode (lines 328-332 in `SteamNetworkManager.cs`).
The `SerializeLoadMap` function creates a byte array.
If `SendToAll` is called with this array, it should work.

However, `BlEditorManagerLoadMap_Patch` (lines 243-317 in `Patches.cs`) has a logic:
```csharp
if (SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsConnected)
{
    if (SteamNetworkManager.Instance.IsHost)
    {
         // ... serialization ...
         SteamNetworkManager.Instance.SendToAll(data);
    }
}
```
Since I fixed `IsConnected` to return true for Local mode, this code *should* run.
But maybe `SerializeLoadMap` has issues or the chunking logic is problematic for Local streams?
Or, maybe the `LoadMap` packet type isn't handled correctly in `ApplyPacket`? It is handled (case `Packets.LoadMap`).

Wait, the user sees "[SteamNetwork] Empty data". This message comes from `SteamNetworkManager.SendToAll` (line 322):
```csharp
        if (data == null || data.Length == 0)
        {
            ConsoleBase.WriteError("[SteamNetwork] Empty data");
            return;
        }
```
This means `SerializeLoadMap` returned an empty or null array for some reason.
In `SerializeLoadMap` (Patches.cs):
```csharp
                byte[] lastChunk = new byte[0];
                // loop...
                    // inside loop: lastChunk = ms.ToArray();
                    // SteamNetworkManager.Instance.SendToAll(lastChunk);
                // end loop
                return lastChunk;
```
The method returns `lastChunk`. But inside the loop, it calls `SendToAll(lastChunk)`.
Wait, if `objects.Count` is 0, the loop doesn't run, `lastChunk` remains `byte[0]` (empty).
Then `SendToAll` is called with this empty array if the return value is used?
Actually, looking at `Postfix`:
```csharp
byte[] data = SerializeLoadMap(WorldBuilderSync.GetMapsObjects());
SteamNetworkManager.Instance.SendToAll(data);
```
`SerializeLoadMap` *sends* chunks internally inside its loop.
BUT it *also* returns the `lastChunk`.
And then `Postfix` calls `SendToAll(data)` with that returned value!
So the last chunk is sent **twice**!
And if the map is empty or the loop logic is slightly off, it might be sending an empty array at the end.
Specifically:
1. `SerializeLoadMap` iterates and sends chunks.
2. It returns `lastChunk` (the last processed chunk).
3. `Postfix` takes this result and sends it *again*.
This is definitely a bug. `SerializeLoadMap` should probably be `void` and handle all sending, OR `Postfix` should handle sending and `SerializeLoadMap` should just return the list of packets (but it's chunked, so void is better).
OR, `SerializeLoadMap` is designed to return the *full* data? No, it returns `lastChunk`.
So `Postfix` is redundantly sending the last chunk.

**Fix:**
1.  Refactor `SerializeLoadMap` to be `void` and handle all sending internally.
2.  Remove the `SteamNetworkManager.Instance.SendToAll(data);` call in `Postfix`.
3.  Ensure `SerializeLoadMap` handles the case of 0 objects correctly (though `GetMapsObjects` likely returns empty list, loop doesn't run, nothing sent - which is fine, or maybe we should send a "Clear" packet?).
    If `objects.Count` is 0, we still might want to send a "Load Map" packet with 0 objects to clear the clients' maps?
    Yes, `WorldBuilderSync.loadMap` handles `clear`. If we load a map, we usually want to clear the previous one.
    The current logic sets `isFirstChunk` and `isLastChunk`.
    If count is 0:
    Loop doesn't run. Nothing sent. Clients don't get updated.
    I should handle the 0 objects case to send at least one packet saying "Clear everything, 0 objects".

**Plan:**
1.  Modify `Patches.cs`:
    *   Change `SerializeLoadMap` return type to `void`.
    *   Remove the extra `SendToAll` in `Postfix`.
    *   Handle `objects.Count == 0` case in `SerializeLoadMap` to send a single empty clear packet.
    *   Verify `SerializeLoadMap` loop logic to ensure `isLastChunk` is set correctly.

This fixes the "Empty data" error (caused by the redundant send of potentially empty `lastChunk` or just double sending) and ensures map loading works in all modes (since `SendToAll` supports Local).

**Note on Local Mode**: The user asked "make it work in local mode too". Since `SendToAll` works for local, fixing the logic flow (redundant send) should make it work for both. The "Empty data" error was likely the blocker.