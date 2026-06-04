using UnityEngine;

namespace GameDeveloperKit.Logger
{
    internal sealed class DebugGuiDriver : MonoBehaviour
    {
        private DebugModule m_Module;

        public void Initialize(DebugModule module)
        {
            m_Module = module;
        }

        private void Update()
        {
            m_Module?.UpdateMetrics(Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            m_Module?.DrawGui();
        }
    }
}
