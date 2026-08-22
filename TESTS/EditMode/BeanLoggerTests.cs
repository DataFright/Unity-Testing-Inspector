using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UTI.Tests
{
    public class BeanLoggerTests
    {
        private class SpyBeanOutput : IBeanOutput
        {
            public int OpenCount;
            public int CloseCount;
            public readonly List<BeanSample> WrittenSamples = new List<BeanSample>();

            public void Open(BeanTracker tracker) => OpenCount++;
            public void Write(BeanSample sample) => WrittenSamples.Add(sample);
            public void Close() => CloseCount++;
        }

        private class ThrowingBeanOutput : IBeanOutput
        {
            public bool ThrowOnOpen;
            public bool ThrowOnWrite;
            public bool ThrowOnClose;
            public int WriteCount;
            public int CloseCount;

            public void Open(BeanTracker tracker)
            {
                if (ThrowOnOpen)
                    throw new InvalidOperationException("open boom");
            }

            public void Write(BeanSample sample)
            {
                WriteCount++;
                if (ThrowOnWrite)
                    throw new InvalidOperationException("write boom");
            }

            public void Close()
            {
                CloseCount++;
                if (ThrowOnClose)
                    throw new InvalidOperationException("close boom");
            }
        }

        [Test]
        public void Open_ForwardsCapturedSamplesToActiveOutputs()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.None;

            var spy = new SpyBeanOutput();
            logger.CustomOutputs.Add(spy);
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, spy.OpenCount);
            Assert.AreEqual(1, spy.WrittenSamples.Count);
        }

        [Test]
        public void Close_StopsForwardingAndClosesOutputs()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.None;

            var spy = new SpyBeanOutput();
            logger.CustomOutputs.Add(spy);
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            logger.Close();
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, spy.CloseCount);
            Assert.AreEqual(1, spy.WrittenSamples.Count, "no samples should be forwarded after Close()");
        }

        [Test]
        public void ConsoleOutput_LogsFormattedSample()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Console;
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Bean\].*tick=0.*"));
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void CsvOutput_WritesHeaderAndOneRowPerSample()
        {
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv;
            logger.FilePath = path;
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual("tick,timestamp,x,y,z,qx,qy,qz,qw,extras", lines[0]);
            Assert.AreEqual(3, lines.Length, "expected a header row plus one row per sample");
            StringAssert.StartsWith("0,", lines[1]);
            StringAssert.StartsWith("1,", lines[2]);
        }

        [Test]
        public void CsvOutput_CustomCaptureExtras_WritesExtrasColumnWithRealData()
        {
            // T11 (TESTS/TestTracker.md): the delegate -> Capture() -> CsvBeanOutput pipeline,
            // not just "extras is empty when unassigned" (already covered by the header/row test
            // above via a tracker with no CustomCapture set).
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv;
            logger.FilePath = path;
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.CustomCapture = _ => new Dictionary<string, float> { { "health", 42f } };
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            StringAssert.Contains("health=42", lines[1]);
        }

        [Test]
        public void Open_OneOutputThrowsOnOpen_OtherOutputsStillOpenAndReceiveSamples()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.None;

            var throwing = new ThrowingBeanOutput { ThrowOnOpen = true };
            var spy = new SpyBeanOutput();
            logger.CustomOutputs.Add(throwing);
            logger.CustomOutputs.Add(spy);

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Bean\] Output ThrowingBeanOutput failed to open.*"));
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, spy.OpenCount, "the healthy output should still open despite the other one throwing");
            Assert.AreEqual(1, spy.WrittenSamples.Count, "the healthy output should still receive samples");
        }

        [Test]
        public void HandleSample_OneOutputThrowsOnWrite_OtherOutputStillReceivesSamplesAndBrokenOneIsDropped()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.None;

            var throwing = new ThrowingBeanOutput { ThrowOnWrite = true };
            var spy = new SpyBeanOutput();
            logger.CustomOutputs.Add(throwing);
            logger.CustomOutputs.Add(spy);
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Bean\] Output ThrowingBeanOutput failed to write.*"));
            tracker.SimulateFrame(1f / 60f);
            tracker.SimulateFrame(1f / 60f);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, throwing.WriteCount, "the broken output should be dropped after its first failure, not retried every sample");
            Assert.AreEqual(2, spy.WrittenSamples.Count, "the healthy output should keep receiving every sample");
        }

        [Test]
        public void Close_OneOutputThrowsOnClose_OtherOutputStillCloses()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.None;

            var throwing = new ThrowingBeanOutput { ThrowOnClose = true };
            var spy = new SpyBeanOutput();
            logger.CustomOutputs.Add(throwing);
            logger.CustomOutputs.Add(spy);
            logger.Open();

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Bean\] Output ThrowingBeanOutput failed to close.*"));
            logger.Close();

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, throwing.CloseCount);
            Assert.AreEqual(1, spy.CloseCount, "the healthy output should still close despite the other one throwing");
        }

        [Test]
        public void OutputTargets_ChangedAfterOpen_TakesEffectImmediately()
        {
            // BUG-06 (TESTS/BugTracker.md): a script that adds BeanLogger at runtime - where
            // OnEnable()->Open() fires synchronously during AddComponent<T>() - and changes
            // OutputTargets a moment later used to see zero effect from that change until a
            // manual Close()+Open(), since Open() only ever built activeOutputs once. Reproduces
            // that exact sequence: Open() while still Console-only, then add Csv afterward.
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Console;
            logger.Open(); // simulates the synchronous OnEnable()->Open() a real runtime AddComponent<BeanLogger>() would trigger

            logger.OutputTargets = BeanOutputTargets.Console | BeanOutputTargets.Csv; // changed after Open() already ran
            logger.FilePath = path;

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            bool fileWasCreated = File.Exists(path);
            string[] lines = fileWasCreated ? File.ReadAllLines(path) : null;
            if (fileWasCreated)
                File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.IsTrue(fileWasCreated, "changing OutputTargets after Open() should take effect, not silently do nothing");
            Assert.AreEqual(2, lines.Length, "expected the header plus the one sample captured after the OutputTargets change");
        }

        [Test]
        public void OutputTargets_SetToSameValue_DoesNotReopen()
        {
            // The re-open-on-change fix above should be a no-op when the value doesn't actually
            // change - otherwise something as harmless as re-assigning the same OutputTargets in
            // a script would needlessly truncate an in-progress CSV back to just the header.
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv;
            logger.FilePath = path;
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);

            logger.OutputTargets = BeanOutputTargets.Csv; // same value - should not reopen/truncate

            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(3, lines.Length, "re-assigning the same OutputTargets value should not truncate rows already written");
        }

        [Test]
        public void CsvOutput_DefaultAppendFalse_TruncatesOnReopen()
        {
            // T14 (TESTS/TestTracker.md): the original, still-default behavior - re-opening the
            // same path without append starts over, matching a pooled object getting a fresh log
            // per reuse unless it explicitly opts in to appending.
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            var output = new CsvBeanOutput(path);
            output.Open(tracker);
            output.Write(new BeanSample(0, 0f, Vector3.zero, Quaternion.identity));
            output.Close();

            output.Open(tracker); // simulates a pooled object's reuse cycle re-opening the same path
            output.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, lines.Length, "reopening without append should truncate back to just the header");
        }

        [Test]
        public void CsvOutput_AppendTrue_PreservesPriorRowsAcrossReopen()
        {
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            var output = new CsvBeanOutput(path, append: true);
            output.Open(tracker);
            output.Write(new BeanSample(0, 0f, Vector3.zero, Quaternion.identity));
            output.Close();

            output.Open(tracker); // simulates a pooled object's reuse cycle re-opening the same path
            output.Write(new BeanSample(1, 1f, Vector3.zero, Quaternion.identity));
            output.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(3, lines.Length, "expected one header row plus both rows preserved across the reopen");
            Assert.AreEqual("tick,timestamp,x,y,z,qx,qy,qz,qw,extras", lines[0]);
            StringAssert.StartsWith("0,", lines[1]);
            StringAssert.StartsWith("1,", lines[2]);
        }

        [Test]
        public void BeanLogger_AppendAcrossReuseFalse_TruncatesAcrossOpenCloseCycles()
        {
            // Confirms the default (off) BeanLogger.AppendAcrossReuse threads append:false into
            // CsvBeanOutput exactly as before - a fixed shared path re-opened twice still starts
            // over, not accumulates.
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv;
            logger.FilePath = path;
            Assert.IsFalse(logger.AppendAcrossReuse, "should default to false");

            logger.Open();
            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            logger.Open(); // simulates SetActive(false) -> SetActive(true) reuse
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(1, lines.Length, "expected just the header - reuse without opting in should not preserve prior rows");
        }

        [Test]
        public void BeanLogger_AppendAcrossReuseTrue_PreservesRowsAcrossOpenCloseCycles()
        {
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv;
            logger.FilePath = path;
            logger.AppendAcrossReuse = true;

            logger.Open();
            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            logger.Open(); // simulates SetActive(false) -> SetActive(true) reuse
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(3, lines.Length, "expected the header plus one row from each reuse cycle, preserved across reopen");
        }

        [Test]
        public void ResolveFilePath_ExplicitPathSet_ReturnsItUnchanged()
        {
            string path = BeanLogger.ResolveFilePath("C:/somewhere/custom.csv", "AnyName", "abc12345", System.DateTime.UtcNow);

            Assert.AreEqual("C:/somewhere/custom.csv", path);
        }

        [Test]
        public void ResolveFilePath_JsonExtension_DefaultsToJsonlUnderProjectRoot()
        {
            var captureTime = new System.DateTime(2026, 8, 8, 12, 0, 0, System.DateTimeKind.Utc);

            string path = BeanLogger.ResolveFilePath(null, "PlayerPlane", "abc12345", captureTime, "jsonl");

            StringAssert.Contains("BeanLogs", path);
            StringAssert.EndsWith("_PlayerPlane_abc12345_bean.jsonl", path);
        }

        [Test]
        public void JsonOutput_WritesOneJsonObjectPerLine()
        {
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.jsonl");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Json;
            logger.FilePath = path;
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(2, lines.Length, "expected one JSON line per sample, no header row");
            StringAssert.StartsWith("{\"tick\":0,", lines[0]);
            StringAssert.StartsWith("{\"tick\":1,", lines[1]);
            StringAssert.Contains("\"extras\":null", lines[0]);
        }

        [Test]
        public void JsonOutput_CustomCaptureExtras_WritesNestedExtrasObject()
        {
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.jsonl");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Json;
            logger.FilePath = path;
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.CustomCapture = _ => new Dictionary<string, float> { { "health", 42f } };
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            StringAssert.Contains("\"extras\":{\"health\":42}", lines[0]);
        }

        [Test]
        public void JsonlBeanOutput_DefaultAppendFalse_TruncatesOnReopen()
        {
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.jsonl");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            var output = new JsonlBeanOutput(path);
            output.Open(tracker);
            output.Write(new BeanSample(0, 0f, Vector3.zero, Quaternion.identity));
            output.Close();

            output.Open(tracker); // simulates a pooled object's reuse cycle re-opening the same path
            output.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(0, lines.Length, "reopening without append should truncate back to nothing");
        }

        [Test]
        public void JsonlBeanOutput_AppendTrue_PreservesPriorRowsAcrossReopen()
        {
            string path = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.jsonl");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            tracker.ClearSamples();
            tracker.StartTracking();

            var output = new JsonlBeanOutput(path, append: true);
            output.Open(tracker);
            output.Write(new BeanSample(0, 0f, Vector3.zero, Quaternion.identity));
            output.Close();

            output.Open(tracker); // simulates a pooled object's reuse cycle re-opening the same path
            output.Write(new BeanSample(1, 1f, Vector3.zero, Quaternion.identity));
            output.Close();

            string[] lines = File.ReadAllLines(path);
            File.Delete(path);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(2, lines.Length, "expected both rows preserved across the reopen");
            StringAssert.StartsWith("{\"tick\":0,", lines[0]);
            StringAssert.StartsWith("{\"tick\":1,", lines[1]);
        }

        [Test]
        public void BuildActiveOutputs_BothCsvAndJsonWithExplicitFilePath_FallBackToSeparateDefaultPaths()
        {
            // One fixed explicit path can't safely back two different file formats at once -
            // same precedent as BeanSnapshotExporter's multi-angle capture (DESIGN.md Sec 8.4).
            string explicitPath = Path.Combine(Path.GetTempPath(), $"bean_test_{System.Guid.NewGuid():N}.csv");

            var go = new GameObject("BeanLoggerTestObject");
            var tracker = go.AddComponent<BeanTracker>();
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv | BeanOutputTargets.Json;
            logger.FilePath = explicitPath;
            logger.Open();

            tracker.ClearSamples();
            tracker.StartTracking();
            tracker.SimulateFrame(1f / 60f);
            logger.Close();

            bool explicitPathWasCreated = File.Exists(explicitPath);
            if (explicitPathWasCreated)
                File.Delete(explicitPath);
            UnityEngine.Object.DestroyImmediate(go);

            Assert.IsFalse(explicitPathWasCreated, "neither output should have written to the shared explicit path when both formats are active");
        }

        [Test]
        public void ApplyConfigDefaults_NullConfig_LeavesOutputTargetsUnchanged()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var logger = go.AddComponent<BeanLogger>();
            logger.OutputTargets = BeanOutputTargets.Csv;

            logger.ApplyConfigDefaults(null);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(BeanOutputTargets.Csv, logger.OutputTargets);
        }

        [Test]
        public void ApplyConfigDefaults_WithConfig_AppliesOutputTargets()
        {
            var go = new GameObject("BeanLoggerTestObject");
            var logger = go.AddComponent<BeanLogger>();

            var config = new BeanConfig { DefaultOutputTargets = BeanOutputTargets.Json };

            logger.ApplyConfigDefaults(config);

            UnityEngine.Object.DestroyImmediate(go);

            Assert.AreEqual(BeanOutputTargets.Json, logger.OutputTargets);
        }

        [Test]
        public void ResolveFilePath_NoExplicitPath_DefaultsUnderProjectRootSubfolderWithTimestampAndToken()
        {
            var captureTime = new System.DateTime(2026, 8, 7, 13, 45, 30, 250, System.DateTimeKind.Utc);

            string path = BeanLogger.ResolveFilePath(null, "PlayerPlane", "abc12345", captureTime);

            StringAssert.StartsWith(BeanArtifactPaths.RootDirectory, path);
            StringAssert.DoesNotContain("AppData", path);
            StringAssert.Contains("BeanLogs", path);
            StringAssert.Contains("20260807_134530_250_PlayerPlane_abc12345_bean.csv", path);
        }

        [Test]
        public void ResolveFilePath_TwoInstancesAtSameTimestamp_ProduceDifferentPaths()
        {
            var captureTime = new System.DateTime(2026, 8, 7, 13, 45, 30, System.DateTimeKind.Utc);

            string firstPath = BeanLogger.ResolveFilePath(null, "Bullet(Clone)", "token0001", captureTime);
            string secondPath = BeanLogger.ResolveFilePath(null, "Bullet(Clone)", "token0002", captureTime);

            Assert.AreNotEqual(firstPath, secondPath);
        }
    }
}
