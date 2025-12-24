using BrokeProtocol.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace WorldBuilderCoop.Behavior
{
    internal static class BoundingHighlightManager
    {
        public static CustomBoundingHighlight BoundsHighlightAdd(GameObject gameObject, Bounds bounds)
        {
            CustomBoundingHighlight boundsHighlight = gameObject.AddComponent<CustomBoundingHighlight>();
            boundsHighlight.bounds = bounds;
            return boundsHighlight;
        }

        public static void BoundsHighlightRemove(GameObject gameObject)
        {
            gameObject.TryGetComponent<CustomBoundingHighlight>(out CustomBoundingHighlight component);
            if (component)
                UnityEngine.Object.Destroy(gameObject);
        }
    }

    [GizmoComponent]
    internal class CustomBoundingHighlight : MonoBehaviour
    {
        [NonSerialized]
        public Bounds bounds;

        private Transform mTransform;

        private Material material;

        private Mesh mesh;

        private void Start()
        {
            mTransform = base.transform;
            material = Resources.Load<Material>("Materials/Lines");
            GenerateBounds();
        }

        private void OnDestroy()
        {
            CleanupMesh();
        }

        private void CleanupMesh()
        {
            if ((bool)mesh)
            {
                UnityEngine.Object.Destroy(mesh);
            }
        }

        private void LateUpdate()
        {
            Graphics.DrawMesh(mesh, mTransform.localToWorldMatrix, material, 0);
        }

        public void GenerateBounds()
        {
            TransformStruct ts = new TransformStruct(mTransform);
            mTransform.SetTRS(TransformStruct.identity);
            Physics.SyncTransforms();
            Bounds bounds = ((this.bounds.extents == Vector3.zero) ? mTransform.GetWorldBounds() : this.bounds);
            mTransform.SetTRS(ts);
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            List<Vector3> list = new List<Vector3>();
            list.AddRange(DrawBoundsEdge(center, 0f - extents.x, 0f - extents.y, 0f - extents.z));
            list.AddRange(DrawBoundsEdge(center, 0f - extents.x, 0f - extents.y, extents.z));
            list.AddRange(DrawBoundsEdge(center, extents.x, 0f - extents.y, 0f - extents.z));
            list.AddRange(DrawBoundsEdge(center, extents.x, 0f - extents.y, extents.z));
            list.AddRange(DrawBoundsEdge(center, 0f - extents.x, extents.y, 0f - extents.z));
            list.AddRange(DrawBoundsEdge(center, 0f - extents.x, extents.y, extents.z));
            list.AddRange(DrawBoundsEdge(center, extents.x, extents.y, 0f - extents.z));
            list.AddRange(DrawBoundsEdge(center, extents.x, extents.y, extents.z));
            Vector2[] array = new Vector2[48];
            int[] array2 = new int[48];
            Color[] array3 = new Color[48];
            for (int i = 0; i < 48; i++)
            {
                array2[i] = i;
                array[i] = Vector2.zero;
                array3[i] = Color.blue;
                array3[i].a = 1f;
            }
            CleanupMesh();
            mesh = new Mesh
            {
                vertices = list.ToArray(),
                subMeshCount = 1
            };
            mesh.SetIndices(array2, MeshTopology.Lines, 0);
            mesh.uv = array;
            mesh.normals = list.ToArray();
            mesh.colors = array3;
            material.SetVector("_Center", mesh.bounds.center);
        }

        private Vector3[] DrawBoundsEdge(Vector3 p, float x, float y, float z)
        {
            Vector3[] array = new Vector3[6];
            p += new Vector3(x, y, z);
            array[0] = (array[2] = (array[4] = p));
            array[1] = p - new Vector3(x * 0.5f, 0f, 0f);
            array[3] = p - new Vector3(0f, y * 0.5f, 0f);
            array[5] = p - new Vector3(0f, 0f, z * 0.5f);
            return array;
        }
    }
}