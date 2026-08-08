using NUnit.Framework;
using UnityEngine;

namespace UTI.Tests
{
    public class BeanBufferTests
    {
        [Test]
        public void Add_WithinCapacity_PreservesChronologicalOrder()
        {
            var buffer = new BeanBuffer(3);
            buffer.Add(MakeSample(0));
            buffer.Add(MakeSample(1));
            buffer.Add(MakeSample(2));

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(0, buffer[0].TickIndex);
            Assert.AreEqual(1, buffer[1].TickIndex);
            Assert.AreEqual(2, buffer[2].TickIndex);
        }

        [Test]
        public void Add_BeyondCapacity_OverwritesOldestAndKeepsOrder()
        {
            var buffer = new BeanBuffer(3);
            buffer.Add(MakeSample(0));
            buffer.Add(MakeSample(1));
            buffer.Add(MakeSample(2));
            buffer.Add(MakeSample(3)); // should push out tick 0

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(1, buffer[0].TickIndex);
            Assert.AreEqual(2, buffer[1].TickIndex);
            Assert.AreEqual(3, buffer[2].TickIndex);
        }

        [Test]
        public void Clear_ResetsCountToZero()
        {
            var buffer = new BeanBuffer(3);
            buffer.Add(MakeSample(0));
            buffer.Add(MakeSample(1));

            buffer.Clear();

            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Extras_DefaultsToNull()
        {
            var sample = MakeSample(0);
            Assert.IsNull(sample.Extras);
        }

        private static BeanSample MakeSample(int tickIndex)
        {
            return new BeanSample(tickIndex, tickIndex, Vector3.zero, Quaternion.identity);
        }
    }
}
