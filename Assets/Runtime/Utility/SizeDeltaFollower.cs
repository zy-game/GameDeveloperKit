using UnityEngine;

namespace GameDeveloperKit
{
    /// <summary>
    /// RectTransform SizeDelta跟随组件
    /// 让当前RectTransform的sizeDelta跟随目标RectTransform
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SizeDeltaFollower : MonoBehaviour
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private Vector2 offset;
        [SerializeField] private bool followWidth = true;
        [SerializeField] private bool followHeight = true;

        private RectTransform _rectTransform;
        private Vector2 _lastTargetSizeDelta;

        /// <summary>
        /// 目标RectTransform
        /// </summary>
        public RectTransform Target
        {
            get => target;
            set
            {
                target = value;
                UpdateSizeDelta();
            }
        }

        /// <summary>
        /// 偏移量
        /// </summary>
        public Vector2 Offset
        {
            get => offset;
            set
            {
                offset = value;
                UpdateSizeDelta();
            }
        }

        /// <summary>
        /// 是否跟随宽度
        /// </summary>
        public bool FollowWidth
        {
            get => followWidth;
            set
            {
                followWidth = value;
                UpdateSizeDelta();
            }
        }

        /// <summary>
        /// 是否跟随高度
        /// </summary>
        public bool FollowHeight
        {
            get => followHeight;
            set
            {
                followHeight = value;
                UpdateSizeDelta();
            }
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
            
            UpdateSizeDelta();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 仅当目标sizeDelta变化时更新
            if (_lastTargetSizeDelta != target.sizeDelta)
            {
                UpdateSizeDelta();
                _lastTargetSizeDelta = target.sizeDelta;
            }
        }

        /// <summary>
        /// 更新sizeDelta
        /// </summary>
        public void UpdateSizeDelta()
        {
            if (target == null || _rectTransform == null) return;

            var currentSize = _rectTransform.sizeDelta;
            var targetSize = target.sizeDelta;

            _rectTransform.sizeDelta = new Vector2(
                followWidth ? targetSize.x + offset.x : currentSize.x,
                followHeight ? targetSize.y + offset.y : currentSize.y
            );

            _lastTargetSizeDelta = targetSize;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
            
            UpdateSizeDelta();
        }
#endif
    }
}
