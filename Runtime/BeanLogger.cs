using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UTI
{
    [Flags]
    public enum BeanOutputTargets
    {
        None = 0,
        Console = 1 << 0,
        Csv = 1 << 1
    }

    /// <summary>Subscribes to a BeanTracker's samples and forwards them to one or more IBeanOutputs.</summary>
    public class BeanLogger : MonoBehaviour
    {
        [SerializeField] private BeanTracker tracker;
        [SerializeField] private BeanOutputTargets outputTargets = BeanOutputTargets.Console;
        [SerializeField] private string filePath;
        // Off by default - matches the original behavior of a fresh, freshly-timestamped CSV on
        // every Open(). A pooled object (SetActive(false)->SetActive(true) reuse instead of
        // Destroy/Instantiate) can opt in to keep one running CSV across reuse cycles instead of
        // truncating each time - see DESIGN.md Sec 13/T14.
        [SerializeField] private bool appendAcrossReuse;

        public BeanTracker Tracker { get => tracker; set => tracker = value; }
        public BeanOutputTargets OutputTargets { get => outputTargets; set => outputTargets = value; }
        public string FilePath { get => filePath; set => filePath = value; }
        public bool AppendAcrossReuse { get => appendAcrossReuse; set => appendAcrossReuse = value; }

        // Extra sinks beyond outputTargets (JSON, an analytics endpoint, whatever) without
        // touching core code. Combined with the outputTargets-built outputs, not a replacement.
        public List<IBeanOutput> CustomOutputs { get; } = new List<IBeanOutput>();

        private readonly List<IBeanOutput> activeOutputs = new List<IBeanOutput>();
        private bool isOpen;
        // Only ever set/read when appendAcrossReuse is true - resolved once on this instance's
        // first Open() and reused on every subsequent Open(), so a pooled object's repeated
        // SetActive(false)->SetActive(true) cycles keep writing to the same file instead of each
        // reopen resolving a brand-new timestamped path (which would defeat append entirely).
        private string cachedCsvPath;

        private void OnEnable() => Open();

        private void OnDisable() => Close();

        private void OnDestroy() => Close();

        // Standalone builds don't reliably hit OnDisable/OnDestroy on quit - belt and suspenders
        // so CSV output actually gets flushed.
        private void OnApplicationQuit() => Close();

        /// <summary>
        /// Resolves the tracker, builds the active output set, and opens each output. Exposed
        /// publicly (like BeanTracker.StartTracking/SimulateFrame) so this is testable in Edit
        /// Mode without relying on OnEnable, which doesn't fire there without [ExecuteAlways].
        /// Idempotent - calling twice without an intervening Close() is a no-op.
        /// </summary>
        public void Open()
        {
            if (isOpen)
                return;

            if (tracker == null)
                tracker = GetComponent<BeanTracker>();

            if (tracker == null)
                return;

            BuildActiveOutputs();

            // Opening one output is a real system boundary (file I/O - disk full, permission
            // denied, an invalid path) that shouldn't take down every other output with it. Without
            // isolating this, a single broken CSV path would previously propagate out of Open()
            // entirely, skipping "tracker.OnSample += HandleSample; isOpen = true;" below - meaning
            // Console output (already opened successfully) would silently never receive samples
            // either, and this BeanLogger would look enabled in the Inspector while doing nothing.
            for (int i = activeOutputs.Count - 1; i >= 0; i--)
            {
                try
                {
                    activeOutputs[i].Open(tracker);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Bean] Output {activeOutputs[i].GetType().Name} failed to open for '{gameObject.name}', disabling it for this run: {ex.Message}");
                    activeOutputs.RemoveAt(i);
                }
            }

            tracker.OnSample += HandleSample;
            isOpen = true;
        }

        /// <summary>Unsubscribes and closes every active output. Idempotent.</summary>
        public void Close()
        {
            if (!isOpen)
                return;

            tracker.OnSample -= HandleSample;

            // Same reasoning as Open() above - one output failing to close (e.g. a final flush
            // hitting a full disk) shouldn't stop the others from closing/flushing cleanly.
            foreach (IBeanOutput output in activeOutputs)
            {
                try
                {
                    output.Close();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Bean] Output {output.GetType().Name} failed to close cleanly for '{gameObject.name}': {ex.Message}");
                }
            }

            isOpen = false;
        }

        private void BuildActiveOutputs()
        {
            activeOutputs.Clear();

            if ((outputTargets & BeanOutputTargets.Console) != 0)
                activeOutputs.Add(new ConsoleBeanOutput());

            if ((outputTargets & BeanOutputTargets.Csv) != 0)
            {
                string path = ResolveCsvPath();
                activeOutputs.Add(new CsvBeanOutput(path, appendAcrossReuse));
            }

            activeOutputs.AddRange(CustomOutputs);
        }

        // Fresh path every call when appendAcrossReuse is off (original behavior); the first
        // call's path when it's on, cached and reused for every subsequent Open() on this
        // instance so a pooled object's reuse cycles keep landing in the same file.
        private string ResolveCsvPath()
        {
            if (appendAcrossReuse && !string.IsNullOrEmpty(cachedCsvPath))
                return cachedCsvPath;

            string path = ResolveFilePath(filePath, gameObject.name, BeanArtifactPaths.NewUniqueToken(), DateTime.UtcNow);
            if (appendAcrossReuse)
                cachedCsvPath = path;

            return path;
        }

        // Subfolder name, nested under BeanArtifactPaths.RootDirectory ("<project root>/UTI/").
        private const string LogSubfolder = "BeanLogs";

        /// <summary>
        /// Resolves where the CSV gets written - explicit filePath if set (used as-is), otherwise
        /// a default under BeanArtifactPaths.RootDirectory/BeanLogs/ that's unique both across
        /// repeated runs on the same GameObject (timestamped, so "ran it 5 times, want to
        /// compare" doesn't silently overwrite the same file) and across duplicate GameObject
        /// names alive at once (uniqueToken, so two prefab clones both named e.g. "Bullet(Clone)"
        /// opening in the same frame - and therefore the same millisecond - still don't collide).
        /// This was the CSV half of the collision gap flagged in DESIGN.md Sec 13/T13, now fixed
        /// to match BeanSnapshotExporter's PNG side. Pure function (given a timestamp/token) so
        /// it's testable without a live GameObject.
        /// </summary>
        public static string ResolveFilePath(string explicitPath, string objectName, string uniqueToken, DateTime captureTimeUtc)
        {
            if (!string.IsNullOrEmpty(explicitPath))
                return explicitPath;

            string timestamp = captureTimeUtc.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string fileName = $"{timestamp}_{objectName}_{uniqueToken}_bean.csv";
            return BeanArtifactPaths.ResolveDefaultPath(LogSubfolder, fileName);
        }

        private void HandleSample(BeanSample sample)
        {
            // Same isolation as Open()/Close(): a write failure partway through a run (e.g. disk
            // fills up) shouldn't propagate up through the tracker's own OnSample invocation - that
            // would break BeanTracker.Capture() itself for every other OnSample subscriber, not
            // just this one broken output. Drop the offending output instead of retrying it every
            // sample once it's proven itself unreliable this run.
            for (int i = activeOutputs.Count - 1; i >= 0; i--)
            {
                try
                {
                    activeOutputs[i].Write(sample);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Bean] Output {activeOutputs[i].GetType().Name} failed to write a sample for '{gameObject.name}', disabling it for the rest of this run: {ex.Message}");
                    activeOutputs.RemoveAt(i);
                }
            }
        }
    }
}
