using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace UTI.Tests
{
    public class BeanArtifactPathsTests
    {
        [Test]
        public void ProjectRootDirectory_IsParentOfApplicationDataPath()
        {
            string dataPath = Path.GetFullPath(Application.dataPath);

            StringAssert.StartsWith(BeanArtifactPaths.ProjectRootDirectory, dataPath);
        }

        [Test]
        public void RootDirectory_IsUtiSubfolderOfProjectRoot()
        {
            StringAssert.StartsWith(BeanArtifactPaths.ProjectRootDirectory, BeanArtifactPaths.RootDirectory);
            StringAssert.Contains("UTI", BeanArtifactPaths.RootDirectory);
        }

        [Test]
        public void RootDirectory_IsNotUnderPersistentDataPath()
        {
            StringAssert.DoesNotContain("AppData", BeanArtifactPaths.RootDirectory);
        }

        [Test]
        public void ResolveDefaultPath_CombinesRootSubfolderAndFileName()
        {
            string path = BeanArtifactPaths.ResolveDefaultPath("SomeSubfolder", "file.txt");

            StringAssert.StartsWith(BeanArtifactPaths.RootDirectory, path);
            StringAssert.Contains("SomeSubfolder", path);
            StringAssert.EndsWith("file.txt", path);
        }

        [Test]
        public void NewUniqueToken_ReturnsNonEmptyDistinctValuesEachCall()
        {
            string first = BeanArtifactPaths.NewUniqueToken();
            string second = BeanArtifactPaths.NewUniqueToken();

            Assert.IsNotEmpty(first);
            Assert.AreNotEqual(first, second);
        }
    }
}
