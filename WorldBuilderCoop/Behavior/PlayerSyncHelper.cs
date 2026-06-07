using BrokeProtocol.Client.Builder;
using ModLoader;
using Steamworks;
using System.IO;
using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop
{
    internal class PlayerSyncHelper
    {
        public static byte[] SerializePlayerSync(int userId, Vector3 position, Quaternion rotation, int placeIndex)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)Packets.PlayerSync);
                    writer.Write(userId);
                    writer.Write(position.x);
                    writer.Write(position.y);
                    writer.Write(position.z);
                    writer.Write(rotation.x);
                    writer.Write(rotation.y);
                    writer.Write(rotation.z);
                    writer.Write(rotation.w);
                    writer.Write(placeIndex);
                    return ms.ToArray();
                }
            }
        }

        public static byte[] SerializeRemovePlayer(int userId)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)Packets.RemovePlayer);
                writer.Write(userId);
                return ms.ToArray();
            }
        }

        public static void userSync(int userId, Vector3 position, Quaternion rotation, int placeIndex)
        {
            // Lookup O(1) via le registre au lieu d'un FindObjectsByType par paquet (~20/s/joueur).
            UserAvatar foundAvatar = Core.networkObjectManager.GetUserAvatar(userId);

            if (foundAvatar == null)
            {
                WbLog.Debug($"[PlayerSyncHelper] New user detected (ID: {userId}), creating avatar.");
                addUser(userId, position, rotation, placeIndex);
            }
            else
            {
                WbLog.Debug($"[PlayerSyncHelper] Updating existing user (ID: {userId}) to {position}");
                var interpolator = foundAvatar.GetComponent<UserInterpolator>();
                if (interpolator == null)
                {
                    interpolator = foundAvatar.gameObject.AddComponent<UserInterpolator>();
                }
                interpolator.SetTarget(position, rotation);

                // Update place index and visibility
                foundAvatar.placeIndex = placeIndex;
                UpdateAvatarVisibility(foundAvatar);
            }
        }

        public static void RemoveUser(int userId)
        {
            // Libère les verrous de sélection du joueur parti (sinon ses objets restent
            // bloqués pour les autres). Chaque client fait ce nettoyage localement.
            Core.networkObjectManager.ReleaseAllByUser(userId);

            UserAvatar avatar = Core.networkObjectManager.GetUserAvatar(userId);
            if (avatar != null)
            {
                WbLog.Debug($"[PlayerSyncHelper] Removing user {userId}");
                Core.networkObjectManager.UnregisterUserAvatar(userId);
                UnityEngine.Object.Destroy(avatar.gameObject);
            }
        }

        public static void addUser(int userId, Vector3 position, Quaternion rotation, int placeIndex)
        {
            int currentUserId = GetCurrentUserId();
            if (userId == currentUserId)
            {
                return;
            }

            if (Core.networkObjectManager.GetUserAvatar(userId) != null)
            {
                WbLog.Debug($"[PlayerSyncHelper] [Sync Conflict Avoided] Avatar already exists for user {userId}. Skipping creation.");
                return;
            }

            WbLog.Debug($"[PlayerSyncHelper] [New Player Sync] Initializing avatar for user {userId} at {position}");

            try
            {
                GameObject userSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                userSphere.name = "User_" + userId;
                userSphere.transform.position = position;
                userSphere.transform.rotation = rotation;
                userSphere.transform.localScale = Vector3.one * 0.5f;

                Renderer renderer = userSphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = new Material(Shader.Find("Standard"));
                    material.color = new Color(1f, 0f, 0f, 0.5f);
                    renderer.material = material;
                }

                var collider = userSphere.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;

                UserAvatar avatar = userSphere.AddComponent<UserAvatar>();
                avatar.UserId = userId;
                avatar.position = position;
                avatar.rotation = rotation;
                avatar.placeIndex = placeIndex;

                Core.networkObjectManager.RegisterUserAvatar(userId, avatar);

                // Add interpolator
                var interpolator = userSphere.AddComponent<UserInterpolator>();
                interpolator.SetTarget(position, rotation);

                GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arrow.name = "User_" + userId + "_Arrow";
                arrow.transform.parent = userSphere.transform;
                arrow.transform.localPosition = Vector3.forward * 0.5f;
                arrow.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

                Renderer arrowRenderer = arrow.GetComponent<Renderer>();
                if (arrowRenderer != null)
                {
                    Material arrowMaterial = new Material(Shader.Find("Standard"));
                    arrowMaterial.color = Color.white;
                    arrowRenderer.material = arrowMaterial;
                }

                var arrowCollider = arrow.GetComponent<Collider>();
                if (arrowCollider != null) arrowCollider.enabled = false;

                AvatarTextDisplay textDisplay = userSphere.AddComponent<AvatarTextDisplay>();
                textDisplay.Initialize(userId.ToString());

                UpdateAvatarVisibility(avatar);

                WbLog.Debug($"[PlayerSyncHelper] [Success] Avatar created for user {userId}");
            }
            catch (System.Exception ex)
            {
                ConsoleBase.WriteError($"[PlayerSyncHelper] [Error] Failed to create avatar for user {userId}: {ex.Message}");
            }
        }

        public static void UpdateAllAvatarsVisibility()
        {
            foreach (var avatar in Core.networkObjectManager.GetAllUserAvatars())
            {
                UpdateAvatarVisibility(avatar);
            }
        }

        public static void UpdateAvatarVisibility(UserAvatar avatar)
        {
            // We need to check local player's place index
            // Assuming we can get it from SceneManager or similar
            // For now, let's try to find the local player's place index via SceneManager if possible

            if (BrokeProtocol.Managers.SceneManager.Instance != null)
            {
                int localPlaceIndex = BrokeProtocol.Managers.SceneManager.Instance.currentPlace;
                bool isVisible = (avatar.placeIndex == localPlaceIndex);

                foreach (var r in avatar.GetComponentsInChildren<Renderer>())
                {
                    r.enabled = isVisible;
                }
                foreach (var c in avatar.GetComponentsInChildren<Canvas>()) // For text display
                {
                    c.enabled = isVisible;
                }
            }
        }

        private static int GetCurrentUserId() => NetworkIdentity.GetUserId();

        public static void removeUser(int userId) => RemoveUser(userId);
    }
}
