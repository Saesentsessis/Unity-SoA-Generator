using NUnit.Framework;

namespace Saesentsessis.DOD.SoA.Tests
{
    public class BitwiseTests
    {
        [Test]
        public void AlignDown_Int_AlignsToPowerOfTwo()
        {
            Assert.AreEqual(4, Bitwise.AlignDown(7, 4));
        }

        [Test]
        public void AlignDown_Int_AlreadyAligned_ReturnsSame()
        {
            Assert.AreEqual(8, Bitwise.AlignDown(8, 4));
        }

        [Test]
        public void AlignUp_Int_RoundsUp()
        {
            Assert.AreEqual(8, Bitwise.AlignUp(5, 4));
        }

        [Test]
        public void AlignUp_Int_AlreadyAligned_ReturnsSame()
        {
            Assert.AreEqual(8, Bitwise.AlignUp(8, 4));
        }

        [Test]
        public void AlignDown_Long_AlignsToPowerOfTwo()
        {
            Assert.AreEqual(4L, Bitwise.AlignDown(7L, 4));
        }

        [Test]
        public void AlignUp_Long_RoundsUp()
        {
            Assert.AreEqual(8L, Bitwise.AlignUp(5L, 4));
        }

        [Test]
        public void ComputeByteSize_NoFlags_ReturnsAlignedElementTimesCapacity()
        {
            Assert.AreEqual(Bitwise.AlignUp(20L * 100, 8), Bitwise.ComputeByteSize(20, 100, 0));
        }

        [Test]
        public void ComputeByteSize_WithFlags_IncludesBitBlocks()
        {
            int elementSize = 8;
            int capacity = 100;
            int flagCount = 2;
            long expected = Bitwise.AlignUp((long)elementSize * capacity, 8) + Bitwise.GetBlockCount(capacity) * flagCount;
            Assert.AreEqual(expected, Bitwise.ComputeByteSize(elementSize, capacity, flagCount));
        }

        [Test]
        public void ComputeByteSize_ZeroCapacity_ReturnsZero()
        {
            Assert.AreEqual(0L, Bitwise.ComputeByteSize(20, 0, 0));
        }

        [Test]
        public void GetBlockCount_Aligned64_ReturnsEightBytes()
        {
            Assert.AreEqual(8, Bitwise.GetBlockCount(64));
        }

        [Test]
        public void GetBlockCount_65_ReturnsSixteenBytes()
        {
            Assert.AreEqual(16, Bitwise.GetBlockCount(65));
        }

        [Test]
        public void GetBlockCount_Zero_ReturnsZero()
        {
            Assert.AreEqual(0, Bitwise.GetBlockCount(0));
        }

        [Test]
        public void GetBlockCount_One_ReturnsEightBytes()
        {
            Assert.AreEqual(8, Bitwise.GetBlockCount(1));
        }
    }
}
