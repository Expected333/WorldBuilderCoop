I will fix the map loading system and enforce host-only permission.

**Modifications:**

1. **Enforce Host Permission**: Modify `BlEditorManagerLoadMap_Patch` in `Patches.cs` to check if the user is the host (`IsHost`). If not, display an error message and **prevent** the map loading logic from executing. The user mentioned disconnection, but preventing execution with a warning is a better UX than disconnecting.
2. **Fix Map Loading Loop**: Modify `WorldBuilderSync.loadMap` in `WorldBuilderSync.cs`. The `tempCount` variable is static and might cause issues if multiple loads happen. I'll make it local or properly reset it. Also, ensure `ObjectHelper.InitializeEditor(gameObject)` is called correctly for networked objects.
3. **Correct Object Collection**: In `GetMapsObjects`, the logic `if (networkObj == null) item2.gameObject.AddComponent<NetworkObject>();` is risky during collection (modifying while iterating or creating side effects). I'll review if this auto-adding component is desired behavior or if it should be skipped. *Correction*: It seems intended to ensure all objects have IDs before sync.

**Action Plan:**

1. **Patches.cs**:

   * In `BlEditorManagerLoadMap_Patch.Postfix`, add:

     ```csharp
     if (!SteamNetworkManager.Instance.IsHost) {
         ConsoleBase.WriteError("Only the host can load maps!");
         // Optionally: Disconnect logic if strictly required by user ("ça le déconnecte")
         return;
     }
     ```

   * Wait, the user said "si un client essaye alors ça le déconnecte". I will implement a check: if client tries to load, show error. The user might mean "if a client receives a load map packet it shouldn't load it"? No, "si un client essaye" implies user interaction (UI). So, blocking the UI action for clients is the right way.

2. **WorldBuilderSync.cs**:

   * Review `loadMap` to ensure it handles the `clear` flag and object instantiation correctly without errors.

**Wait**, the user specifically pointed out: "si un client essaye alors ça le déconnecte". This sounds like a bug report ("it currently disconnects them") rather than a feature request ("please disconnect them"). The user wants me to *repair* it.
*Interpretation*: "Repair the map loading system. Also, only the host should be allowed to load a map. Currently, if a client tries, it disconnects them (which is bad/buggy) or they shouldn't be able to."
*Correction*: I will prevent the client from triggering the load map logic in the first place.

**Tasks:**

1. Modify `Patches.cs`: Add `IsHost` check in `BlEditorManagerLoadMap_Patch`.
2. Modify `WorldBuilderSync.cs`: Ensure `loadMap` is robust.
3. Execute the pending task: Move `PlayerSyncTracker` init to `Patches.cs`.

