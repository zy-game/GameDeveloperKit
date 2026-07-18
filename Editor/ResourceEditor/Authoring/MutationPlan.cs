using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GameDeveloperKit.ResourceEditor.Authoring
{
    internal sealed class MutationPlan
    {
        private readonly Settings m_Settings;
        private readonly List<BundleState> m_Bundles;
        private readonly string m_BeforeFingerprint;

        private MutationPlan(Settings settings)
        {
            m_Settings = settings;
            m_Bundles = settings.Packages
                .Where(package => package != null)
                .SelectMany(package => package.Bundles.Where(bundle => bundle != null))
                .Select(bundle => new BundleState(bundle))
                .ToList();
            m_BeforeFingerprint = CalculateFingerprint(settings);
        }

        public bool HasChanges => string.Equals(
            m_BeforeFingerprint,
            CalculateFingerprint(m_Settings),
            StringComparison.Ordinal) is false;

        public static MutationPlan Capture(Settings settings)
        {
            return new MutationPlan(
                settings ?? throw new ArgumentNullException(nameof(settings)));
        }

        public void Rollback()
        {
            foreach (var bundle in m_Bundles)
            {
                bundle.Restore();
            }
        }

        private static string CalculateFingerprint(Settings settings)
        {
            return JsonConvert.SerializeObject(new
            {
                Packages = settings.Packages
                    .Where(package => package != null)
                    .Select(package => new
                    {
                        package.Name,
                        Bundles = package.Bundles
                            .Where(bundle => bundle != null)
                            .Select(bundle => new
                            {
                                bundle.Name,
                                bundle.Group,
                                bundle.CollectorId,
                                bundle.SourceFolder,
                                Entries = bundle.Entries.Select(entry => entry == null
                                    ? null
                                    : new
                                    {
                                        entry.Guid,
                                        entry.AssetPath,
                                        entry.Location,
                                        entry.TypeName,
                                        entry.ProviderId,
                                        entry.ExcludeKind,
                                        Labels = entry.Labels
                                    })
                            })
                    })
            });
        }

        private sealed class BundleState
        {
            private readonly Bundle m_Bundle;
            private readonly string m_Name;
            private readonly string m_Group;
            private readonly string m_CollectorId;
            private readonly string m_SourceFolder;
            private readonly List<AssetEntry> m_Entries;
            private readonly List<EntryState> m_States;

            public BundleState(Bundle bundle)
            {
                m_Bundle = bundle;
                m_Name = bundle.Name;
                m_Group = bundle.Group;
                m_CollectorId = bundle.CollectorId;
                m_SourceFolder = bundle.SourceFolder;
                m_Entries = new List<AssetEntry>(bundle.Entries);
                m_States = m_Entries
                    .Where(entry => entry != null)
                    .Select(entry => new EntryState(entry))
                    .ToList();
            }

            public void Restore()
            {
                foreach (var state in m_States)
                {
                    state.Restore();
                }

                m_Bundle.Name = m_Name;
                m_Bundle.Group = m_Group;
                m_Bundle.CollectorId = m_CollectorId;
                m_Bundle.SourceFolder = m_SourceFolder;
                m_Bundle.Entries.Clear();
                m_Bundle.Entries.AddRange(m_Entries);
            }
        }

        private sealed class EntryState
        {
            private readonly AssetEntry m_Entry;
            private readonly string m_Guid;
            private readonly string m_AssetPath;
            private readonly string m_Location;
            private readonly string m_TypeName;
            private readonly string m_ProviderId;
            private readonly EntryExcludeKind m_ExcludeKind;
            private readonly string[] m_Labels;

            public EntryState(AssetEntry entry)
            {
                m_Entry = entry;
                m_Guid = entry.Guid;
                m_AssetPath = entry.AssetPath;
                m_Location = entry.Location;
                m_TypeName = entry.TypeName;
                m_ProviderId = entry.ProviderId;
                m_ExcludeKind = entry.ExcludeKind;
                m_Labels = entry.Labels.ToArray();
            }

            public void Restore()
            {
                m_Entry.Guid = m_Guid;
                m_Entry.AssetPath = m_AssetPath;
                m_Entry.Location = m_Location;
                m_Entry.TypeName = m_TypeName;
                m_Entry.ProviderId = m_ProviderId;
                m_Entry.ExcludeKind = m_ExcludeKind;
                m_Entry.Labels.Clear();
                m_Entry.Labels.AddRange(m_Labels);
            }
        }
    }
}
