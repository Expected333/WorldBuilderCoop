I will modify `PlayerSyncTracker.cs` to add robust debugging and improve camera detection.

**Specific changes:**
1.  **Improve Camera Detection**: Add a fallback to `Object.FindObjectOfType<Camera>()` if `BlSceneCamera.Instance` and `Camera.main` are null.
2.  **Add Status Logs**:
    *   Log periodically (e.g., every 5 seconds) if the target camera is still null.
    *   Log once when the target camera is successfully found.
    *   Log if packet sending is skipped because `SteamNetworkManager` is not connected.
3.  **Verify Update Loop**: Ensure the `Update` loop is running and checking for movement.

**Goal**: These changes will either fix the issue (by finding the camera via fallback) or provide the exact reason why packets aren't being sent (missing camera vs network state).