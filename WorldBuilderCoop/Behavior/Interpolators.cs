using UnityEngine;

namespace WorldBuilderCoop
{
    public class UserInterpolator : MonoBehaviour
    {
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 currentPosition;
        private Quaternion currentRotation;
        private float interpolationSpeed = 10f;

        public void SetTarget(Vector3 newPosition, Quaternion newRotation)
        {
            targetPosition = newPosition;
            targetRotation = newRotation;
        }

        public void Update()
        {
            currentPosition = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * interpolationSpeed);
            currentRotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * interpolationSpeed);

            transform.position = currentPosition;
            transform.rotation = currentRotation;
        }
    }

    public class GameObjectInterpolator : MonoBehaviour
    {
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 targetScale;
        private float interpolationSpeed = 15f;
        private float snapDistance = 10f;

        public void SetTarget(Vector3 newPosition, Quaternion newRotation, Vector3 newScale)
        {
            targetPosition = newPosition;
            targetRotation = newRotation;
            targetScale = newScale;

            if (Vector3.Distance(transform.position, targetPosition) > snapDistance)
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
                transform.localScale = targetScale;
            }
        }

        public void Update()
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * interpolationSpeed);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * interpolationSpeed);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * interpolationSpeed);
        }
    }
}