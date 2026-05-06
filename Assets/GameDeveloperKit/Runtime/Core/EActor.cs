using System;
using UnityEngine;

namespace GameDeveloperKit
{
    [Flags]
    public enum HideFlags
    {
        None = 0,
        HideInHierarchy = 1,
        HideInInspector = 2,
        DontSaveInEditor = 4,
        NotEditable = 8,
        DontSaveInBuild = 0x10,
        DontUnloadUnusedAsset = 0x20,
        DontSave = 0x34,
        HideAndDontSave = 0x3D
    }

    public abstract class EActor : IReference
    {
        int id { get; set; }
        string name { get; set; }
        HideFlags hideFlags { get; set; }

        void IDisposable.Dispose()
        {
            Release();
        }

        public virtual void Release()
        {
            id = 0;
            name = string.Empty;
            hideFlags = HideFlags.None;
        }
    }
}