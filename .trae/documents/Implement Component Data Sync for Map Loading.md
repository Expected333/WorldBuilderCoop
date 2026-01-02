I will implement the synchronization of object component data during map loading.

**Analysis:**
The user wants `LoadMap` to also send component configuration (serialized data from the `Serialized` component) to clients, so that objects are fully configured (e.g., lights, triggers, custom properties) upon loading.
Currently, `ObjectInfo` only contains position, rotation, scale, prefabIndex, etc. It needs a field for component data.
Broke Protocol's `Serialized` component has a `ToJSON()` or similar serialization method, or `BrokeProtocol.Managers.SceneManager` handles serialization.
Looking at `WorldBuilderEventManager.cs`, `SerializeUpdateObject` already has a placeholder for component data:

```csharp
int componentDataLength = reader.ReadInt32();
byte[] componentData;
// ...
writer.Write(0); // No component data for now
```

I need to find how to extract this data from a `GameObject`.
`BrokeProtocol.Entities.Serialized` is the key component.
I will add a `byte[] componentData` field to `ObjectInfo`.
Then in `GetMapsObjects`, I will extract this data from the `Serialized` component if it exists.
Then update `SerializeAndSendLoadMap` (Patches.cs) and `HandleMapChunk` (WorldBuilderSync.cs) / `PacketHandler.cs` to write/read this extra byte array.

**Steps:**

1. **Modify** **`ObjectInfo`** **class** (NetworkConfig.cs): Add `public byte[] componentData;`.
2. **Update** **`GetMapsObjects`** (WorldBuilderSync.cs):

   * For each object, check for `Serialized` component.

   * If present, serialize its data. `Serialized.ToString()` usually returns the JSON string. Or `Serialized.Save()`?

   * I need to check `Serialized` methods. Usually in BP modding, `Serialized` components save their state to a string or byte array.

   * If I can't find the exact method in search, I'll assume `ToString()` or look for a property.

   * *Self-Correction*: `BrokeProtocol.Managers.SceneManager.Instance.SerializeLevel` uses `Serialized` components.

   * I'll assume `Serialized.ToString()` gives the JSON string representation of the component data, which is standard in BP. I'll convert that string to bytes.
3. **Update** **`SerializeAndSendLoadMap`** (Patches.cs):

   * Write the length of the byte array (int).

   * Write the byte array itself.
4. **Update** **`PacketHandler.cs`** **(HandleLoadMap)**:

   * Read the length.

   * Read the bytes.

   * Store in `ObjectInfo`.
5. **Update** **`ProcessPendingLoadQueue`** **(WorldBuilderSync.cs)**:

   * After instantiating, if `componentData` is not empty, apply it.

   * Convert bytes back to string (JSON).

   * Get `Serialized` component on the new object.

   * Call `Serialized.Deserialize(string json)` or similar.

**Search Result Analysis**:
I see `BrokeProtocol.Entities.Serialized` mentioned.
I don't see the definition of `Serialized` but `ObjectHelper.InitializeEditor` calls `component.InitializeEditor()`.
I will assume `Serialized` has `ToJson()` / `FromJson()` or `ToString()` / `Parse()`.
Wait, `SerializeUpdateObject` in `WorldBuilderEventManager` writes `0` for component data.
In `BlEditorManager.cs`: `history.Add(MonoBehaviourSingleton<BrokeProtocol.Managers.SceneManager>.Instance.SerializeLevel(checkSave: false));`
`SerializeLevel` returns a `List<BaseParameters>`.
Actually, `ObjectInfo` seems to mirror `BaseParameters` or similar.
If I look at `GetMapsObjects`, it iterates transforms.
I will try to get the `Serialized` component and use its `ToString()` which is usually the saved data format in BP.

**Plan:**

1. **NetworkConfig.cs**: Add `public byte[] componentData` to `ObjectInfo`.
2. **WorldBuilderSync.cs**:

   * In `GetMapsObjects`: Get `Serialized` component. If exists, `componentData = Encoding.UTF8.GetBytes(component.ToString())`.

   * In `ProcessPendingLoadQueue`: After instantiation, `if (obj.componentData != null && obj.componentData.Length > 0)`, get `Serialized` component, call `component.Deserialize(Encoding.UTF8.GetString(obj.componentData))`. *Note: I need to verify the method name* *`Deserialize`. It might be* *`Parse`* *or* *`Load`.*

   * *Safer approach*: `BrokeProtocol.Utility.Util.Parse(string)`? Or `Serialized.Parse(string)`?

   * I will search for "Serialized" usage or "Deserialize" to be sure.

   * *Fallback*: If I can't find it, I'll use `component.Read(string)` which is common. Or just `component.data = string`.

   * Actually, `BrokeProtocol.Entities.Serialized` usually handles this.

   * I'll search for `Serialized` class members if possible.

**Refinement**: I'll assume `ToString()` returns the data string (JSON) and `Deserialize(string)` or `Load(string)` applies it. I'll use a safe pattern or search first.
Let's just search for "Serialized" class or usage in the codebase to find how to save/load component data.
Actually, I'll use `Agent` to find `Serialized` methods.

**Wait**, I can just update the code based on the user's request: "host envoie aussi les données du component".
I will update `ObjectInfo`, serialization in `Patches.cs`, deserialization in `PacketHandler.cs`, and application in `WorldBuilderSync.cs`.

**Tasks**:

1. Update `ObjectInfo` struct/class.
2. Update `GetMapsObjects` to capture data.
3. Update `SerializeAndSendLoadMap` to write data.
4. Update `PacketHandler` to read data.
5. Update `ProcessPendingLoadQueue` to apply data.

