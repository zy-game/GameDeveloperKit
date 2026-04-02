using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Player;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine.Build.Pipeline;

namespace GameDeveloperKit.Tests.Editor
{
    internal sealed class FakeBundleBuildResults : IBundleBuildResults
    {
        private readonly Dictionary<Type, IContextObject> _contextObjects = new();

        public FakeBundleBuildResults(Dictionary<string, BundleDetails> bundleInfos)
        {
            BundleInfos = bundleInfos;
            WriteResults = new Dictionary<string, WriteResult>();
            WriteResultsMetaData = new Dictionary<string, SerializedFileMetaData>();
            AssetResults = new Dictionary<GUID, AssetResultData>();
        }

        public ScriptCompilationResult ScriptResults { get; set; }

        public Dictionary<string, WriteResult> WriteResults { get; }

        public Dictionary<string, SerializedFileMetaData> WriteResultsMetaData { get; }

        public Dictionary<GUID, AssetResultData> AssetResults { get; }

        public Dictionary<string, BundleDetails> BundleInfos { get; }

        public bool ContainsContextObject<T>() where T : IContextObject
        {
            return _contextObjects.ContainsKey(typeof(T));
        }

        public bool ContainsContextObject(Type type)
        {
            return type != null && _contextObjects.ContainsKey(type);
        }

        public T GetContextObject<T>() where T : IContextObject
        {
            return TryGetContextObject(out T contextObject) ? contextObject : default;
        }

        public IContextObject GetContextObject(Type type)
        {
            if (type == null)
            {
                return null;
            }

            _contextObjects.TryGetValue(type, out var contextObject);
            return contextObject;
        }

        public void SetContextObject<T>(IContextObject contextObject) where T : IContextObject
        {
            SetContextObject(typeof(T), contextObject);
        }

        public void SetContextObject(Type type, IContextObject contextObject)
        {
            if (type == null || contextObject == null)
            {
                return;
            }

            _contextObjects[type] = contextObject;
        }

        public void SetContextObject(IContextObject contextObject)
        {
            if (contextObject == null)
            {
                return;
            }

            _contextObjects[contextObject.GetType()] = contextObject;
        }

        public bool TryGetContextObject<T>(out T contextObject) where T : IContextObject
        {
            if (_contextObjects.TryGetValue(typeof(T), out var value) && value is T typed)
            {
                contextObject = typed;
                return true;
            }

            contextObject = default;
            return false;
        }

        public bool TryGetContextObject(Type type, out IContextObject contextObject)
        {
            if (type == null)
            {
                contextObject = null;
                return false;
            }

            return _contextObjects.TryGetValue(type, out contextObject);
        }
    }
}
