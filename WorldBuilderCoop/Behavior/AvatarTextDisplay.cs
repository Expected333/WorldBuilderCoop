using BrokeProtocol.Entities;
using BrokeProtocol.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace BrokeProtocol.Client.Builder
{
    public class AvatarTextDisplay : MonoBehaviour
    {
        private ShTextDisplay _shTextDisplay;
        private string _pendingPlayerName;
        private GameObject _textObj;

        private void Start()
        {
            if (!string.IsNullOrEmpty(_pendingPlayerName))
            {
                SetupTextDisplay(_pendingPlayerName);
                _pendingPlayerName = null;
            }
        }

        private void LateUpdate()
        {
            if (_textObj != null)
            {
                _textObj.transform.position = transform.position + Vector3.up * 0.8f;
                var camera = MonoBehaviourSingleton<BlSceneCamera>.Instance;
                if (camera != null)
                {
                    _textObj.transform.LookAt(_textObj.transform.position + camera.mTransform.forward);
                }
            }
        }

        public void Initialize(string playerName)
        {
            _pendingPlayerName = playerName;
            enabled = true;
        }

        private void SetupTextDisplay(string playerName)
        {
            _shTextDisplay = gameObject.AddComponent<ShTextDisplay>();
            var blGizmo = gameObject.AddComponent<BlGizmoIcon>();
            CreateTextComponent(playerName);
        }

        private void CreateTextComponent(string playerName)
        {
            var existingCanvas = FindObjectOfType<Canvas>();
            Canvas canvas = null;

            if (existingCanvas != null)
            {
                canvas = existingCanvas;
            }
            else
            {
                GameObject canvasObj = new GameObject("AvatarCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 10;
            }

            _textObj = new GameObject("PlayerLabel");
            _textObj.transform.SetParent(canvas.transform, false);
            _textObj.transform.position = transform.position + Vector3.up * 0.8f;

            CanvasRenderer canvasRenderer = _textObj.AddComponent<CanvasRenderer>();

            Text text = _textObj.AddComponent<Text>();
            text.text = playerName;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 20;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = 20;

            RectTransform rectTransform = _textObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100, 50);
            rectTransform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            _shTextDisplay.textComponent = text;
            SetPlayerName(playerName);
        }

        public void SetPlayerName(string playerName)
        {
            if (_shTextDisplay == null)
                return;

            _shTextDisplay.UpdateText(playerName);
        }
    }
}