using System;
using UnityEngine;

namespace GameDeveloperKit
{
    public static class GameObjectExtensions
    {
        #region Collision Events

        public static CollisionListener OnCollisionEnterAsObservable(this GameObject go, Action<Collision> callback)
        {
            var listener = go.GetOrAddComponent<CollisionListener>();
            listener.OnEnter += callback;
            return listener;
        }

        public static CollisionListener OnCollisionStayAsObservable(this GameObject go, Action<Collision> callback)
        {
            var listener = go.GetOrAddComponent<CollisionListener>();
            listener.OnStay += callback;
            return listener;
        }

        public static CollisionListener OnCollisionExitAsObservable(this GameObject go, Action<Collision> callback)
        {
            var listener = go.GetOrAddComponent<CollisionListener>();
            listener.OnExit += callback;
            return listener;
        }

        #endregion

        #region Trigger Events

        public static TriggerListener OnTriggerEnterAsObservable(this GameObject go, Action<Collider> callback)
        {
            var listener = go.GetOrAddComponent<TriggerListener>();
            listener.OnEnter += callback;
            return listener;
        }

        public static TriggerListener OnTriggerStayAsObservable(this GameObject go, Action<Collider> callback)
        {
            var listener = go.GetOrAddComponent<TriggerListener>();
            listener.OnStay += callback;
            return listener;
        }

        public static TriggerListener OnTriggerExitAsObservable(this GameObject go, Action<Collider> callback)
        {
            var listener = go.GetOrAddComponent<TriggerListener>();
            listener.OnExit += callback;
            return listener;
        }

        #endregion

        #region 2D Collision Events

        public static Collision2DListener OnCollisionEnter2DAsObservable(this GameObject go, Action<Collision2D> callback)
        {
            var listener = go.GetOrAddComponent<Collision2DListener>();
            listener.OnEnter += callback;
            return listener;
        }

        public static Collision2DListener OnCollisionStay2DAsObservable(this GameObject go, Action<Collision2D> callback)
        {
            var listener = go.GetOrAddComponent<Collision2DListener>();
            listener.OnStay += callback;
            return listener;
        }

        public static Collision2DListener OnCollisionExit2DAsObservable(this GameObject go, Action<Collision2D> callback)
        {
            var listener = go.GetOrAddComponent<Collision2DListener>();
            listener.OnExit += callback;
            return listener;
        }

        #endregion

        #region 2D Trigger Events

        public static Trigger2DListener OnTriggerEnter2DAsObservable(this GameObject go, Action<Collider2D> callback)
        {
            var listener = go.GetOrAddComponent<Trigger2DListener>();
            listener.OnEnter += callback;
            return listener;
        }

        public static Trigger2DListener OnTriggerStay2DAsObservable(this GameObject go, Action<Collider2D> callback)
        {
            var listener = go.GetOrAddComponent<Trigger2DListener>();
            listener.OnStay += callback;
            return listener;
        }

        public static Trigger2DListener OnTriggerExit2DAsObservable(this GameObject go, Action<Collider2D> callback)
        {
            var listener = go.GetOrAddComponent<Trigger2DListener>();
            listener.OnExit += callback;
            return listener;
        }

        #endregion

        #region Component Helpers

        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        public static T GetOrAddComponent<T>(this Component comp) where T : Component
        {
            return comp.gameObject.GetOrAddComponent<T>();
        }

        public static bool HasComponent<T>(this GameObject go) where T : Component
        {
            return go.GetComponent<T>() != null;
        }

        public static void RemoveComponent<T>(this GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null)
                UnityEngine.Object.Destroy(component);
        }

        #endregion

        #region Hierarchy Helpers

        public static void SetParent(this GameObject go, Transform parent, bool worldPositionStays = true)
        {
            go.transform.SetParent(parent, worldPositionStays);
        }

        public static void SetParent(this GameObject go, GameObject parent, bool worldPositionStays = true)
        {
            go.transform.SetParent(parent?.transform, worldPositionStays);
        }

        public static void DestroyChildren(this GameObject go)
        {
            var transform = go.transform;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(transform.GetChild(i).gameObject);
            }
        }

        public static void DestroyChildrenImmediate(this GameObject go)
        {
            var transform = go.transform;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        #endregion

        #region Layer Helpers

        public static void SetLayer(this GameObject go, int layer, bool includeChildren = false)
        {
            go.layer = layer;
            if (includeChildren)
            {
                foreach (Transform child in go.transform)
                {
                    child.gameObject.SetLayer(layer, true);
                }
            }
        }

        public static void SetLayer(this GameObject go, string layerName, bool includeChildren = false)
        {
            go.SetLayer(LayerMask.NameToLayer(layerName), includeChildren);
        }

        #endregion

        #region Active State

        public static void SetActiveIfNeeded(this GameObject go, bool active)
        {
            if (go.activeSelf != active)
                go.SetActive(active);
        }

        #endregion
    }

    #region Listener Components

    public class CollisionListener : MonoBehaviour
    {
        public event Action<Collision> OnEnter;
        public event Action<Collision> OnStay;
        public event Action<Collision> OnExit;

        private void OnCollisionEnter(Collision collision) => OnEnter?.Invoke(collision);
        private void OnCollisionStay(Collision collision) => OnStay?.Invoke(collision);
        private void OnCollisionExit(Collision collision) => OnExit?.Invoke(collision);

        public void Clear()
        {
            OnEnter = null;
            OnStay = null;
            OnExit = null;
        }
    }

    public class TriggerListener : MonoBehaviour
    {
        public event Action<Collider> OnEnter;
        public event Action<Collider> OnStay;
        public event Action<Collider> OnExit;

        private void OnTriggerEnter(Collider other) => OnEnter?.Invoke(other);
        private void OnTriggerStay(Collider other) => OnStay?.Invoke(other);
        private void OnTriggerExit(Collider other) => OnExit?.Invoke(other);

        public void Clear()
        {
            OnEnter = null;
            OnStay = null;
            OnExit = null;
        }
    }

    public class Collision2DListener : MonoBehaviour
    {
        public event Action<Collision2D> OnEnter;
        public event Action<Collision2D> OnStay;
        public event Action<Collision2D> OnExit;

        private void OnCollisionEnter2D(Collision2D collision) => OnEnter?.Invoke(collision);
        private void OnCollisionStay2D(Collision2D collision) => OnStay?.Invoke(collision);
        private void OnCollisionExit2D(Collision2D collision) => OnExit?.Invoke(collision);

        public void Clear()
        {
            OnEnter = null;
            OnStay = null;
            OnExit = null;
        }
    }

    public class Trigger2DListener : MonoBehaviour
    {
        public event Action<Collider2D> OnEnter;
        public event Action<Collider2D> OnStay;
        public event Action<Collider2D> OnExit;

        private void OnTriggerEnter2D(Collider2D other) => OnEnter?.Invoke(other);
        private void OnTriggerStay2D(Collider2D other) => OnStay?.Invoke(other);
        private void OnTriggerExit2D(Collider2D other) => OnExit?.Invoke(other);

        public void Clear()
        {
            OnEnter = null;
            OnStay = null;
            OnExit = null;
        }
    }

    #endregion
}
