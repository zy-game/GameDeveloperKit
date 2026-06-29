using System.Collections.Generic;

namespace GameDeveloperKit.UnityBridge
{
    public interface IUnityBridgeSkill
    {
        string Name { get; }
        string Description { get; }
        string Trigger { get; }
        IEnumerable<UnityBridgeSkillEndpoint> Endpoints { get; }
        IEnumerable<string> Examples { get; }
        IEnumerable<string> Notes { get; }
        bool CanExecute(UnityBridgeSkillRequest request);
        UnityBridgeSkillResponse Execute(UnityBridgeSkillRequest request);
    }

    public readonly struct UnityBridgeSkillEndpoint
    {
        public readonly string Method;
        public readonly string Path;
        public readonly string Description;
        public readonly string BodyExample;

        public UnityBridgeSkillEndpoint(string method, string path, string description, string bodyExample = null)
        {
            Method = method;
            Path = path;
            Description = description;
            BodyExample = bodyExample;
        }
    }
}
