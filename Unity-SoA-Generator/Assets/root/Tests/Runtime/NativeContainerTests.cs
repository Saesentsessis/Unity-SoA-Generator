using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Saesentsessis.DOD.SoA.Tests
{
    public class NativeSimpleEntitySoATests
    {
        private NativeSimpleEntitySoA _container;

        [SetUp]
        public void SetUp()
        {
            _container = new NativeSimpleEntitySoA(128, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            if (_container.IsCreated)
                _container.Dispose();
        }

        [Test]
        public void Constructor_SetsIsCreatedTrue()
        {
            Assert.IsTrue(_container.IsCreated);
        }

        [Test]
        public void Constructor_SetsCapacity()
        {
            Assert.AreEqual(128, _container.Capacity);
        }

        [Test]
        public void Constructor_SetsAllocator()
        {
            Assert.AreEqual(Allocator.Persistent, (Allocator)_container.Allocator.Value);
        }

        [Test]
        public unsafe void Constructor_DataPtrIsNotNull()
        {
            Assert.IsTrue(_container.DataPtr != null);
        }

        [Test]
        public void Constructor_ClearMemory_ZerosFieldArrays()
        {
            using var container = new NativeSimpleEntitySoA(16, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            var health = container.Health;
            var id = container.Id;
            var position = container.Position;
            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(0f, health[i]);
                Assert.AreEqual(0, id[i]);
                Assert.AreEqual(float3.zero, position[i]);
            }
        }

        [Test]
        public void FieldArray_Health_ReadWriteRoundtrip()
        {
            var health = _container.Health;
            health[0] = 42.5f;
            Assert.AreEqual(42.5f, health[0]);
        }

        [Test]
        public void FieldArray_Id_ReadWriteRoundtrip()
        {
            var id = _container.Id;
            id[0] = 99;
            Assert.AreEqual(99, id[0]);
        }

        [Test]
        public void FieldArray_Position_ReadWriteRoundtrip()
        {
            var pos = new float3(1f, 2f, 3f);
            var position = _container.Position;
            position[0] = pos;
            Assert.AreEqual(pos, position[0]);
        }

        [Test]
        public void FieldArray_Length_MatchesCapacity()
        {
            Assert.AreEqual(_container.Capacity, _container.Health.Length);
            Assert.AreEqual(_container.Capacity, _container.Id.Length);
            Assert.AreEqual(_container.Capacity, _container.Position.Length);
        }

        [Test]
        public void FieldArray_AllFields_IndependentStorage()
        {
            var health = _container.Health;
            var id = _container.Id;
            var position = _container.Position;

            health[0] = 100f;
            id[0] = 42;
            position[0] = new float3(7f, 8f, 9f);

            Assert.AreEqual(100f, health[0]);
            Assert.AreEqual(42, id[0]);
            Assert.AreEqual(new float3(7f, 8f, 9f), position[0]);
        }

        [Test]
        public void FieldArray_MultipleIndices_Independent()
        {
            var health = _container.Health;
            health[0] = 1f;
            health[1] = 2f;
            health[2] = 3f;

            Assert.AreEqual(1f, health[0]);
            Assert.AreEqual(2f, health[1]);
            Assert.AreEqual(3f, health[2]);
        }

        [Test]
        public void ByteSize_MatchesManualComputation()
        {
            long expected = Bitwise.ComputeByteSize(UnsafeSimpleEntitySoA.ElementSize, _container.Capacity, UnsafeSimpleEntitySoA.FlagCount);
            Assert.AreEqual(expected, _container.ByteSize);
        }

        [Test]
        public void SetCapacity_IncreasesCapacity()
        {
            _container.SetCapacity(256);
            Assert.GreaterOrEqual(_container.Capacity, 256);
        }

        [Test]
        public void SetCapacity_PreservesExistingData()
        {
            var health = _container.Health;
            var id = _container.Id;
            var position = _container.Position;

            health[0] = 42.5f;
            id[0] = 99;
            position[0] = new float3(1f, 2f, 3f);

            _container.SetCapacity(256);

            health = _container.Health;
            id = _container.Id;
            position = _container.Position;

            Assert.AreEqual(42.5f, health[0]);
            Assert.AreEqual(99, id[0]);
            Assert.AreEqual(new float3(1f, 2f, 3f), position[0]);
        }

        [Test]
        public void Dispose_SetsIsCreatedFalse()
        {
            _container.Dispose();
            Assert.IsFalse(_container.IsCreated);
        }

        [Test]
        public void Equals_SameInstance_ReturnsTrue()
        {
            var copy = _container;
            Assert.IsTrue(_container.Equals(copy));
        }

        [Test]
        public void Equals_DifferentInstance_ReturnsFalse()
        {
            using var other = new NativeSimpleEntitySoA(128, Allocator.Persistent);
            Assert.IsFalse(_container.Equals(other));
        }

        [Test]
        public void EqualityOperators_MatchEquals()
        {
            var copy = _container;
            Assert.IsTrue(_container == copy);
            Assert.IsFalse(_container != copy);

            using var other = new NativeSimpleEntitySoA(128, Allocator.Persistent);
            Assert.IsTrue(_container != other);
            Assert.IsFalse(_container == other);
        }

        [Test]
        public void GetHashCode_Consistent()
        {
            int hash1 = _container.GetHashCode();
            int hash2 = _container.GetHashCode();
            Assert.AreEqual(hash1, hash2);
        }

        [Test]
        public void Constructor_Capacity1_Works()
        {
            using var container = new NativeSimpleEntitySoA(1, Allocator.Persistent);
            Assert.IsTrue(container.IsCreated);
            var health = container.Health;
            health[0] = 1f;
            Assert.AreEqual(1f, health[0]);
        }

        [Test]
        public void Constructor_LargeCapacity_Works()
        {
            using var container = new NativeSimpleEntitySoA(65536, Allocator.Persistent);
            Assert.IsTrue(container.IsCreated);
            Assert.AreEqual(65536, container.Capacity);
        }
    }

    public class NativeFlaggedEntitySoATests
    {
        private NativeFlaggedEntitySoA _container;

        [SetUp]
        public void SetUp()
        {
            _container = new NativeFlaggedEntitySoA(128, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        [TearDown]
        public void TearDown()
        {
            if (_container.IsCreated)
                _container.Dispose();
        }

        [Test]
        public void Constructor_SetsIsCreatedTrue()
        {
            Assert.IsTrue(_container.IsCreated);
        }

        [Test]
        public void FieldArray_Speed_ReadWriteRoundtrip()
        {
            var speed = _container.Speed;
            speed[0] = 3.14f;
            Assert.AreEqual(3.14f, speed[0]);
        }

        [Test]
        public void FieldArray_Score_ReadWriteRoundtrip()
        {
            var score = _container.Score;
            score[0] = 42;
            Assert.AreEqual(42, score[0]);
        }

        [Test]
        public void FlagBitArray_IsActive_SetAndGet()
        {
            var flags = _container.IsActiveBitArray;
            flags.Set(5, true);
            Assert.IsTrue(flags.IsSet(5));
        }

        [Test]
        public void FlagBitArray_IsVisible_SetAndGet()
        {
            var flags = _container.IsVisibleBitArray;
            flags.Set(10, true);
            Assert.IsTrue(flags.IsSet(10));
        }

        [Test]
        public void FlagBitArray_DefaultIsFalse_WithClearMemory()
        {
            var isActive = _container.IsActiveBitArray;
            var isVisible = _container.IsVisibleBitArray;
            for (int i = 0; i < _container.Capacity; i++)
            {
                Assert.IsFalse(isActive.IsSet(i));
                Assert.IsFalse(isVisible.IsSet(i));
            }
        }

        [Test]
        public void FlagBitArray_TwoFlags_Independent()
        {
            var isActive = _container.IsActiveBitArray;
            var isVisible = _container.IsVisibleBitArray;

            isActive.Set(3, true);
            isVisible.Set(7, true);

            Assert.IsTrue(isActive.IsSet(3));
            Assert.IsFalse(isActive.IsSet(7));
            Assert.IsFalse(isVisible.IsSet(3));
            Assert.IsTrue(isVisible.IsSet(7));
        }

        [Test]
        public void ByteSize_IncludesAlignedFlagBlock()
        {
            long expected = Bitwise.ComputeByteSize(UnsafeFlaggedEntitySoA.ElementSize, _container.Capacity, UnsafeFlaggedEntitySoA.FlagCount);
            Assert.AreEqual(expected, _container.ByteSize);
            Assert.Greater(_container.ByteSize, (long)_container.Capacity * UnsafeFlaggedEntitySoA.ElementSize);
        }

        [Test]
        public void SetCapacity_PreservesFlagData()
        {
            _container.IsActiveBitArray.Set(0, true);
            _container.IsVisibleBitArray.Set(1, true);

            _container.SetCapacity(256);

            Assert.IsTrue(_container.IsActiveBitArray.IsSet(0));
            Assert.IsTrue(_container.IsVisibleBitArray.IsSet(1));
        }

        [Test]
        public void SetCapacity_PreservesFieldAndFlagDataTogether()
        {
            var speed = _container.Speed;
            var score = _container.Score;
            speed[0] = 5.5f;
            score[0] = 77;
            _container.IsActiveBitArray.Set(0, true);
            _container.IsVisibleBitArray.Set(0, true);

            _container.SetCapacity(512);

            speed = _container.Speed;
            score = _container.Score;
            Assert.AreEqual(5.5f, speed[0]);
            Assert.AreEqual(77, score[0]);
            Assert.IsTrue(_container.IsActiveBitArray.IsSet(0));
            Assert.IsTrue(_container.IsVisibleBitArray.IsSet(0));
        }
    }

    public class NativeNestedPaddedSoATests
    {
        private NativeNestedPaddedSoA _container;

        [SetUp]
        public void SetUp()
        {
            _container = new NativeNestedPaddedSoA(128, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            if (_container.IsCreated)
                _container.Dispose();
        }

        [Test]
        public void Constructor_SetsIsCreatedTrue()
        {
            Assert.IsTrue(_container.IsCreated);
        }

        [Test]
        public void FlattenedField_Inner_Tag_ReadWriteRoundtrip()
        {
            var tags = _container.Inner_Tag;
            tags[0] = 42;
            Assert.AreEqual(42, tags[0]);
        }

        [Test]
        public void FlattenedField_Inner_Value_ReadWriteRoundtrip()
        {
            var values = _container.Inner_Value;
            values[0] = 12345;
            Assert.AreEqual(12345, values[0]);
        }

        [Test]
        public void Field_Weight_ReadWriteRoundtrip()
        {
            var weight = _container.Weight;
            weight[0] = 3.14f;
            Assert.AreEqual(3.14f, weight[0]);
        }

        [Test]
        public void UniformAccessor_GetInner_ReconstructsCorrectly()
        {
            var tags = _container.Inner_Tag;
            var values = _container.Inner_Value;
            tags[0] = 7;
            values[0] = 999;

            var inner = _container.GetInner(0);

            Assert.AreEqual(7, inner.Tag);
            Assert.AreEqual(999, inner.Value);
        }

        [Test]
        public void SetCapacity_PreservesFlattenedFieldData()
        {
            var tags = _container.Inner_Tag;
            var values = _container.Inner_Value;
            var weight = _container.Weight;
            tags[0] = 42;
            values[0] = 999;
            weight[0] = 1.5f;

            _container.SetCapacity(256);

            tags = _container.Inner_Tag;
            values = _container.Inner_Value;
            weight = _container.Weight;
            Assert.AreEqual(42, tags[0]);
            Assert.AreEqual(999, values[0]);
            Assert.AreEqual(1.5f, weight[0]);
        }
    }

    public class NativeNoBitPackEntitySoATests
    {
        private NativeNoBitPackEntitySoA _container;

        [SetUp]
        public void SetUp()
        {
            _container = new NativeNoBitPackEntitySoA(128, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            if (_container.IsCreated)
                _container.Dispose();
        }

        [Test]
        public void FieldArray_Flag_ReadWriteRoundtrip()
        {
            var flags = _container.Flag;
            flags[0] = true;
            Assert.IsTrue(flags[0]);
            flags[0] = false;
            Assert.IsFalse(flags[0]);
        }

        [Test]
        public void FieldArray_Flag_Length_MatchesCapacity()
        {
            Assert.AreEqual(_container.Capacity, _container.Flag.Length);
        }
    }
}
