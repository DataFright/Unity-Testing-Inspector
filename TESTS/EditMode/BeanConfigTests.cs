using NUnit.Framework;

namespace UTI.Tests
{
    public class BeanConfigTests
    {
        [Test]
        public void ParseLines_AllKeysPresent_AppliesAllValues()
        {
            var lines = new[]
            {
                "DefaultCaptureMode=EveryNSeconds",
                "DefaultCaptureInterval=2.5",
                "DefaultOutputTargets=Csv",
                "DefaultDimensionMode=Force2D",
                "DefaultMinFramingRadius=6.5"
            };

            BeanConfig config = BeanConfig.ParseLines(lines);

            Assert.AreEqual(BeanCaptureMode.EveryNSeconds, config.DefaultCaptureMode);
            Assert.AreEqual(2.5f, config.DefaultCaptureInterval);
            Assert.AreEqual(BeanOutputTargets.Csv, config.DefaultOutputTargets);
            Assert.AreEqual(BeanDimensionMode.Force2D, config.DefaultDimensionMode);
            Assert.AreEqual(6.5f, config.DefaultMinFramingRadius);
        }

        [Test]
        public void ParseLines_EmptyInput_ReturnsCompiledInDefaults()
        {
            BeanConfig config = BeanConfig.ParseLines(new string[0]);

            Assert.AreEqual(BeanCaptureMode.EveryUpdate, config.DefaultCaptureMode);
            Assert.AreEqual(0.5f, config.DefaultCaptureInterval);
            Assert.AreEqual(BeanOutputTargets.Console, config.DefaultOutputTargets);
            Assert.AreEqual(BeanDimensionMode.Auto, config.DefaultDimensionMode);
            Assert.AreEqual(2f, config.DefaultMinFramingRadius);
        }

        [Test]
        public void ParseLines_DefaultOutputTargetsCombinedFlags_ParsesCommaSeparatedValue()
        {
            // BeanOutputTargets is [Flags] - Enum.TryParse natively supports a comma-separated
            // list of flag names, so a dev can ask for more than one output at once.
            var lines = new[] { "DefaultOutputTargets=Console,Json" };

            BeanConfig config = BeanConfig.ParseLines(lines);

            Assert.AreEqual(BeanOutputTargets.Console | BeanOutputTargets.Json, config.DefaultOutputTargets);
        }

        [Test]
        public void ParseLines_MalformedDefaultOutputTargets_LeavesDefaultUnchanged()
        {
            var lines = new[] { "DefaultOutputTargets=NotARealTarget" };

            BeanConfig config = BeanConfig.ParseLines(lines);

            Assert.AreEqual(BeanOutputTargets.Console, config.DefaultOutputTargets);
        }

        [Test]
        public void ParseLines_MalformedMinFramingRadius_LeavesDefaultUnchanged()
        {
            var lines = new[] { "DefaultMinFramingRadius=not-a-number" };

            BeanConfig config = BeanConfig.ParseLines(lines);

            Assert.AreEqual(2f, config.DefaultMinFramingRadius);
        }

        [Test]
        public void ParseLines_CommentsAndBlankLines_AreIgnored()
        {
            var lines = new[]
            {
                "# this is a comment",
                "",
                "   ",
                "DefaultDimensionMode=Force3D"
            };

            BeanConfig config = BeanConfig.ParseLines(lines);

            Assert.AreEqual(BeanDimensionMode.Force3D, config.DefaultDimensionMode);
        }

        [Test]
        public void ParseLines_UnrecognizedKey_IsIgnoredWithoutError()
        {
            var lines = new[] { "SomeMadeUpKey=whatever", "DefaultCaptureMode=EveryFixedUpdate" };

            BeanConfig config = BeanConfig.ParseLines(lines);

            Assert.AreEqual(BeanCaptureMode.EveryFixedUpdate, config.DefaultCaptureMode);
        }

        [Test]
        public void ParseLines_MalformedEnumValue_LeavesDefaultUnchanged()
        {
            var lines = new[] { "DefaultCaptureMode=NotARealMode" };

            BeanConfig config = BeanConfig.ParseLines(lines);

            Assert.AreEqual(BeanCaptureMode.EveryUpdate, config.DefaultCaptureMode);
        }

        [Test]
        public void ParseLines_LineWithNoEqualsSign_IsIgnoredWithoutError()
        {
            var lines = new[] { "this line has no equals sign", "DefaultDimensionMode=Force2D" };

            BeanConfig config = BeanConfig.ParseLines(lines);

            Assert.AreEqual(BeanDimensionMode.Force2D, config.DefaultDimensionMode);
        }

        [Test]
        public void CreateTemplateIfMissing_TemplateContents_ParseBackToCompiledInDefaults()
        {
            // The shipped template should round-trip to exactly the compiled-in defaults, so a
            // freshly-created BeanConfig.txt doesn't silently change anyone's behavior until
            // they actually edit it.
            string[] lines = BeanConfig.TemplateContents.Split('\n');

            BeanConfig config = BeanConfig.ParseLines(lines);

            Assert.AreEqual(BeanCaptureMode.EveryUpdate, config.DefaultCaptureMode);
            Assert.AreEqual(0.5f, config.DefaultCaptureInterval);
            Assert.AreEqual(BeanOutputTargets.Console, config.DefaultOutputTargets);
            Assert.AreEqual(BeanDimensionMode.Auto, config.DefaultDimensionMode);
            Assert.AreEqual(2f, config.DefaultMinFramingRadius);
        }
    }
}
