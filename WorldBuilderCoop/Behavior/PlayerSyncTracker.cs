using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop.Behavior
{
    public class PlayerSyncTracker : MonoBehaviour
    {
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private const float PositionThreshold = 0.05f;
        private const float RotationThreshold = 1.0f;
        private const float SyncInterval = 0.05f; // 20 times per second
        private float _lastSyncTime;

        private Transform _targetTransform;
        private float _lastDebugTime;

        private int _lastPlaceIndex = -1;

        void Start()
        {
            // Initialize with current transform as fallback, but we will look for camera in Update
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            if (BrokeProtocol.Managers.SceneManager.Instance != null)
                _lastPlaceIndex = BrokeProtocol.Managers.SceneManager.Instance.currentPlace;
        }

        void Update()
        {
            if (Time.time - _lastSyncTime < SyncInterval) return;

            // Find the target transform (Camera) if we haven't yet or if it's lost
            if (_targetTransform == null)
            {
                if (BrokeProtocol.Client.Builder.BlSceneCamera.Instance != null)
                {
                    _targetTransform = BrokeProtocol.Client.Builder.BlSceneCamera.Instance.mTransform;
                    // ConsoleBase.WriteLine("[PlayerSyncTracker] Found BlSceneCamera");
                }
                else if (Camera.main != null)
                {
                    _targetTransform = Camera.main.transform;
                    // ConsoleBase.WriteLine("[PlayerSyncTracker] Found Camera.main");
                }
                else
                {
                    var cam = Object.FindObjectOfType<Camera>();
                    if (cam != null)
                    {
                        _targetTransform = cam.transform;
                        // ConsoleBase.WriteLine($"[PlayerSyncTracker] Found fallback Camera: {cam.name}");
                    }
                }

                if (_targetTransform == null)
                {
                    if (Time.time - _lastDebugTime > 5.0f)
                    {
                        // ConsoleBase.WriteLine("[PlayerSyncTracker] Waiting for Camera...");
                        _lastDebugTime = Time.time;
                    }
                    return; // Still no target, cannot sync
                }
            }

            int currentPlaceIndex = BrokeProtocol.Managers.SceneManager.Instance != null ? BrokeProtocol.Managers.SceneManager.Instance.currentPlace : 0;

            bool hasMoved = Vector3.Distance(_targetTransform.position, _lastPosition) > PositionThreshold;
            bool hasRotated = Quaternion.Angle(_targetTransform.rotation, _lastRotation) > RotationThreshold;
            bool placeChanged = currentPlaceIndex != _lastPlaceIndex;

            if (hasMoved || hasRotated || placeChanged)
            {
                SendSyncPacket();
                _lastPosition = _targetTransform.position;
                _lastRotation = _targetTransform.rotation;
                _lastPlaceIndex = currentPlaceIndex;
                _lastSyncTime = Time.time;
                
                if (placeChanged)
                {
                    PlayerSyncHelper.UpdateAllAvatarsVisibility();
                }
            }
        }

        private void SendSyncPacket()
        {
            if (SteamNetworkManager.Instance == null || !SteamNetworkManager.Instance.IsConnected)
            {
                if (Time.time - _lastDebugTime > 5.0f)
                {
                    // ConsoleBase.WriteLine("[PlayerSyncTracker] Not connected to network, skipping sync.");
                    _lastDebugTime = Time.time;
                }
                return;
            }
            if (_targetTransform == null) return;

            int userId = GetCurrentUserId();
            int placeIndex = BrokeProtocol.Managers.SceneManager.Instance != null ? BrokeProtocol.Managers.SceneManager.Instance.currentPlace : 0;

            // ConsoleBase.WriteLine($"[PlayerSyncTracker] Sending sync packet for user {userId} at {_targetTransform.position}");
            byte[] data = PlayerSyncHelper.SerializePlayerSync(userId, _targetTransform.position, _targetTransform.rotation, placeIndex);
            SteamNetworkManager.Instance.SendToAll(data);
        }

        private int GetCurrentUserId()
        {
            var steamNetManager = SteamNetworkManager.Instance;
            if (steamNetManager != null && steamNetManager.IsLocalMode())
            {
                return LocalUserManager.GetLocalUserId();
            }
            return Steamworks.SteamUser.GetSteamID().m_SteamID.GetHashCode();
        }
    }
}
