using BrokeProtocol.Client.Builder;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using System;
using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop
{
    internal class ObjectHelper
    {
        public static void UpdateObject(Transform t, ObjectInfo obj)
        {
            if (obj.placeIndex >= 0)
            {
                t.SetParent(MonoBehaviourSingleton<SceneManager>.Instance.mTransform.GetChild(obj.placeIndex), worldPositionStays: true);
            }
            else
            {
                t.SetParent(null, worldPositionStays: true);
            }
        }

        public static void InitializeEditor(GameObject go)
        {
            MonoBehaviour[] componentsInChildren = go.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour monoBehaviour in componentsInChildren)
            {
                if (Attribute.GetCustomAttribute(monoBehaviour.GetType(), typeof(GizmoComponentAttribute)) != null)
                {
                    monoBehaviour.enabled = false;
                    UnityEngine.Object.Destroy(monoBehaviour);
                }
            }

            if (go.TryGetComponent<Serialized>(out var component))
            {
                component.InitializeEditor();
            }
            else
            {
                go.InitializeEditor();
            }

            go.GetMesh(out var _);
            UpdateRendering(go);
        }

        private static void UpdateRendering(GameObject g)
        {
            LayerType[] array = SceneManager.Instance.layerTypes;
            foreach (LayerType layerType in array)
            {
                if (layerType.type != null)
                {
                    if (!g.GetComponent(layerType.type))
                    {
                        continue;
                    }
                }
                else if ((bool)g.GetComponent<Serialized>())
                {
                    continue;
                }

                if (layerType.visible)
                {
                    if (!g.activeSelf)
                    {
                        g.SetActive(value: true);
                    }
                    continue;
                }

                if (g.activeSelf)
                {
                    g.SetActive(value: false);
                }
                break;
            }

            BlGizmoForceIcon component2;
            if (SceneManager.Instance.forceGizmos)
            {
                if (!g.TryGetComponent<BlGizmoForceIcon>(out var component) || !component.enabled)
                {
                    g.AddComponent<BlGizmoForceIcon>();
                }
            }
            else if (g.TryGetComponent<BlGizmoForceIcon>(out component2))
            {
                component2.enabled = false;
                UnityEngine.Object.Destroy(component2);
            }
        }
    }
}