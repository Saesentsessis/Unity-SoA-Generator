using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Saesentsessis.DOD.SoA.Tests
{
    public unsafe class UnsafeSimpleEntitySoATests
    {
        private UnsafeSimpleEntitySoA _container;

        [SetUp]
        public void SetUp()
        {
            _container = new UnsafeSimpleEntitySoA(128, Allocator.Persistent);
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
        public void Constructor_DataPtrIsNotNull()
        {
            Assert.IsTrue(_container.DataPtr != null);
        }

        [Test]
        public void Constructor_ClearMemory_ZerosAllFields()
        {
            using var container = new UnsafeSimpleEntitySoA(16, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(0f, container.HealthPtr[i]);
                Assert.AreEqual(0, container.IdPtr[i]);
                Assert.AreEqual(float3.zero, container.PositionPtr[i]);
            }
        }

        [Test]
        public void Constants_ElementSize_IsCorrect()
        {
            Assert.AreEqual(20, UnsafeSimpleEntitySoA.ElementSize);
        }

        [Test]
        public void Constants_FlagCount_IsZero()
        {
            Assert.AreEqual(0, UnsafeSimpleEntitySoA.FlagCount);
        }

        [Test]
        public void Constants_MaxCapacity_IsIntMaxValue()
        {
            Assert.AreEqual(int.MaxValue, UnsafeSimpleEntitySoA.MaxCapacity);
        }

        [Test]
        public void FieldPtr_Health_ReadWriteRoundtrip()
        {
            _container.HealthPtr[0] = 42.5f;
            Assert.AreEqual(42.5f, _container.HealthPtr[0]);
        }

        [Test]
        public void FieldPtr_Id_ReadWriteRoundtrip()
        {
            _container.IdPtr[0] = 99;
            Assert.AreEqual(99, _container.IdPtr[0]);
        }

        [Test]
        public void FieldPtr_Position_ReadWriteRoundtrip()
        {
            var pos = new float3(1f, 2f, 3f);
            _container.PositionPtr[0] = pos;
            Assert.AreEqual(pos, _container.PositionPtr[0]);
        }

        [Test]
        public void FieldPtr_AllFields_IndependentStorage()
        {
            _container.HealthPtr[0] = 100f;
            _container.IdPtr[0] = 42;
            _container.PositionPtr[0] = new float3(7f, 8f, 9f);

            Assert.AreEqual(100f, _container.HealthPtr[0]);
            Assert.AreEqual(42, _container.IdPtr[0]);
            Assert.AreEqual(new float3(7f, 8f, 9f), _container.PositionPtr[0]);
        }

        [Test]
        public void FieldPtr_MultipleIndices_Independent()
        {
            _container.HealthPtr[0] = 1f;
            _container.HealthPtr[1] = 2f;
            _container.HealthPtr[2] = 3f;

            Assert.AreEqual(1f, _container.HealthPtr[0]);
            Assert.AreEqual(2f, _container.HealthPtr[1]);
            Assert.AreEqual(3f, _container.HealthPtr[2]);
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
        public void SetCapacity_RoundsUpToPowerOfTwo()
        {
            _container.SetCapacity(100);
            Assert.IsTrue(math.ispow2(_container.Capacity));
            Assert.GreaterOrEqual(_container.Capacity, 100);
        }

        [Test]
        public void SetCapacity_PreservesExistingData()
        {
            _container.HealthPtr[0] = 42.5f;
            _container.IdPtr[0] = 99;
            _container.PositionPtr[0] = new float3(1f, 2f, 3f);

            _container.SetCapacity(256);

            Assert.AreEqual(42.5f, _container.HealthPtr[0]);
            Assert.AreEqual(99, _container.IdPtr[0]);
            Assert.AreEqual(new float3(1f, 2f, 3f), _container.PositionPtr[0]);
        }

        [Test]
        public void SetCapacity_PreservesMultipleElements()
        {
            for (int i = 0; i < 10; i++)
            {
                _container.HealthPtr[i] = i * 1.5f;
                _container.IdPtr[i] = i * 10;
                _container.PositionPtr[i] = new float3(i, i + 1, i + 2);
            }

            _container.SetCapacity(512);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(i * 1.5f, _container.HealthPtr[i]);
                Assert.AreEqual(i * 10, _container.IdPtr[i]);
                Assert.AreEqual(new float3(i, i + 1, i + 2), _container.PositionPtr[i]);
            }
        }

        [Test]
        public void Dispose_SetsIsCreatedFalse()
        {
            _container.Dispose();
            Assert.IsFalse(_container.IsCreated);
        }

        [Test]
        public void Dispose_DoubleDispose_DoesNotThrow()
        {
            _container.Dispose();
            Assert.DoesNotThrow(() => _container.Dispose());
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
            using var other = new UnsafeSimpleEntitySoA(128, Allocator.Persistent);
            Assert.IsFalse(_container.Equals(other));
        }

        [Test]
        public void EqualityOperators_MatchEquals()
        {
            var copy = _container;
            Assert.IsTrue(_container == copy);
            Assert.IsFalse(_container != copy);

            using var other = new UnsafeSimpleEntitySoA(128, Allocator.Persistent);
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
        public void GetHashCode_EqualInstances_SameHash()
        {
            var copy = _container;
            Assert.AreEqual(_container.GetHashCode(), copy.GetHashCode());
        }

        [Test]
        public void Constructor_Capacity1_Works()
        {
            using var container = new UnsafeSimpleEntitySoA(1, Allocator.Persistent);
            Assert.IsTrue(container.IsCreated);
            container.HealthPtr[0] = 1f;
            Assert.AreEqual(1f, container.HealthPtr[0]);
        }

        [Test]
        public void Constructor_LargeCapacity_Works()
        {
            using var container = new UnsafeSimpleEntitySoA(65536, Allocator.Persistent);
            Assert.IsTrue(container.IsCreated);
            Assert.AreEqual(65536, container.Capacity);
        }
    }

    public unsafe class UnsafeFlaggedEntitySoATests
    {
        private UnsafeFlaggedEntitySoA _container;

        [SetUp]
        public void SetUp()
        {
            _container = new UnsafeFlaggedEntitySoA(128, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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
        public void Constants_FlagCount_IsTwo()
        {
            Assert.AreEqual(2, UnsafeFlaggedEntitySoA.FlagCount);
        }

        [Test]
        public void Constants_ElementSize_ExcludesBooleans()
        {
            Assert.AreEqual(8, UnsafeFlaggedEntitySoA.ElementSize);
        }

        [Test]
        public void Constants_MaxCapacity_AccountsForFlags()
        {
            Assert.AreEqual(int.MaxValue / UnsafeFlaggedEntitySoA.ElementSize, UnsafeFlaggedEntitySoA.MaxCapacity);
        }

        [Test]
        public void FieldPtr_Speed_ReadWriteRoundtrip()
        {
            _container.SpeedPtr[0] = 3.14f;
            Assert.AreEqual(3.14f, _container.SpeedPtr[0]);
        }

        [Test]
        public void FieldPtr_Score_ReadWriteRoundtrip()
        {
            _container.ScorePtr[0] = 42;
            Assert.AreEqual(42, _container.ScorePtr[0]);
        }

        [Test]
        public void FlagBitArray_IsActive_SetAndGet()
        {
            _container.IsActiveBitArray.Set(5, true);
            Assert.IsTrue(_container.IsActiveBitArray.IsSet(5));
        }

        [Test]
        public void FlagBitArray_IsVisible_SetAndGet()
        {
            _container.IsVisibleBitArray.Set(10, true);
            Assert.IsTrue(_container.IsVisibleBitArray.IsSet(10));
        }

        [Test]
        public void FlagBitArray_DefaultIsFalse_WithClearMemory()
        {
            for (int i = 0; i < _container.Capacity; i++)
            {
                Assert.IsFalse(_container.IsActiveBitArray.IsSet(i));
                Assert.IsFalse(_container.IsVisibleBitArray.IsSet(i));
            }
        }

        [Test]
        public void FlagBitArray_TwoFlags_Independent()
        {
            _container.IsActiveBitArray.Set(3, true);
            _container.IsVisibleBitArray.Set(7, true);

            Assert.IsTrue(_container.IsActiveBitArray.IsSet(3));
            Assert.IsFalse(_container.IsActiveBitArray.IsSet(7));
            Assert.IsFalse(_container.IsVisibleBitArray.IsSet(3));
            Assert.IsTrue(_container.IsVisibleBitArray.IsSet(7));
        }

        [Test]
        public void FlagBitArray_IndependentFromFieldData()
        {
            _container.SpeedPtr[0] = 999f;
            _container.ScorePtr[0] = 123;
            _container.IsActiveBitArray.Set(0, true);

            Assert.AreEqual(999f, _container.SpeedPtr[0]);
            Assert.AreEqual(123, _container.ScorePtr[0]);
            Assert.IsTrue(_container.IsActiveBitArray.IsSet(0));
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
            _container.SpeedPtr[0] = 5.5f;
            _container.ScorePtr[0] = 77;
            _container.IsActiveBitArray.Set(0, true);
            _container.IsVisibleBitArray.Set(0, true);

            _container.SetCapacity(512);

            Assert.AreEqual(5.5f, _container.SpeedPtr[0]);
            Assert.AreEqual(77, _container.ScorePtr[0]);
            Assert.IsTrue(_container.IsActiveBitArray.IsSet(0));
            Assert.IsTrue(_container.IsVisibleBitArray.IsSet(0));
        }
    }

    public unsafe class UnsafeNestedPaddedSoATests
    {
        private UnsafeNestedPaddedSoA _container;

        [SetUp]
        public void SetUp()
        {
            _container = new UnsafeNestedPaddedSoA(128, Allocator.Persistent);
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
            _container.Inner_TagPtr[0] = 42;
            Assert.AreEqual(42, _container.Inner_TagPtr[0]);
        }

        [Test]
        public void FlattenedField_Inner_Value_ReadWriteRoundtrip()
        {
            _container.Inner_ValuePtr[0] = 12345;
            Assert.AreEqual(12345, _container.Inner_ValuePtr[0]);
        }

        [Test]
        public void Field_Weight_ReadWriteRoundtrip()
        {
            _container.WeightPtr[0] = 3.14f;
            Assert.AreEqual(3.14f, _container.WeightPtr[0]);
        }

        [Test]
        public void UniformAccessor_GetInner_ReconstructsCorrectly()
        {
            _container.Inner_TagPtr[0] = 7;
            _container.Inner_ValuePtr[0] = 999;

            var inner = _container.GetInner(0);

            Assert.AreEqual(7, inner.Tag);
            Assert.AreEqual(999, inner.Value);
        }

        [Test]
        public void UniformAccessor_GetInner_MultipleIndices()
        {
            for (int i = 0; i < 5; i++)
            {
                _container.Inner_TagPtr[i] = (byte)(i + 1);
                _container.Inner_ValuePtr[i] = (i + 1) * 100;
            }

            for (int i = 0; i < 5; i++)
            {
                var inner = _container.GetInner(i);
                Assert.AreEqual((byte)(i + 1), inner.Tag);
                Assert.AreEqual((i + 1) * 100, inner.Value);
            }
        }

        [Test]
        public void SetCapacity_PreservesFlattenedFieldData()
        {
            _container.Inner_TagPtr[0] = 42;
            _container.Inner_ValuePtr[0] = 999;
            _container.WeightPtr[0] = 1.5f;

            _container.SetCapacity(256);

            Assert.AreEqual(42, _container.Inner_TagPtr[0]);
            Assert.AreEqual(999, _container.Inner_ValuePtr[0]);
            Assert.AreEqual(1.5f, _container.WeightPtr[0]);
        }

        [Test]
        public void AllFlattenedFields_IndependentStorage()
        {
            _container.Inner_TagPtr[0] = 1;
            _container.Inner_ValuePtr[0] = 2;
            _container.WeightPtr[0] = 3f;

            Assert.AreEqual(1, _container.Inner_TagPtr[0]);
            Assert.AreEqual(2, _container.Inner_ValuePtr[0]);
            Assert.AreEqual(3f, _container.WeightPtr[0]);
        }
    }

    public unsafe class UnsafeNoBitPackEntitySoATests
    {
        private UnsafeNoBitPackEntitySoA _container;

        [SetUp]
        public void SetUp()
        {
            _container = new UnsafeNoBitPackEntitySoA(128, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            if (_container.IsCreated)
                _container.Dispose();
        }

        [Test]
        public void Constants_FlagCount_IsZero()
        {
            Assert.AreEqual(0, UnsafeNoBitPackEntitySoA.FlagCount);
        }

        [Test]
        public void Constants_ElementSize_IncludesBoolean()
        {
            Assert.AreEqual(5, UnsafeNoBitPackEntitySoA.ElementSize);
        }

        [Test]
        public void FieldPtr_Flag_ReadWriteRoundtrip()
        {
            _container.FlagPtr[0] = true;
            Assert.IsTrue(_container.FlagPtr[0]);
            _container.FlagPtr[0] = false;
            Assert.IsFalse(_container.FlagPtr[0]);
        }

        [Test]
        public void FieldPtr_Id_ReadWriteRoundtrip()
        {
            _container.IdPtr[0] = 42;
            Assert.AreEqual(42, _container.IdPtr[0]);
        }
    }

    /// <summary>
    /// Covers reinterpretation of externally owned memory as a generated SoA container
    /// via the static ConvertExistingDataToSoA factory.
    /// </summary>
    public unsafe class UnsafeSoAConversionTests
    {
        private const int Capacity = 128;

        private AllocatorManager.AllocatorHandle _allocator;

        [SetUp]
        public void SetUp()
        {
            _allocator = Allocator.Persistent;
        }

        private void* Allocate(long byteSize, int alignment)
        {
            return _allocator.Allocate((int)byteSize, alignment, 1);
        }

        private void Free(void* buffer)
        {
            AllocatorManager.Free(_allocator, buffer);
        }

        [Test]
        public void ConvertExistingData_SetsCapacityAllocatorAndPointer()
        {
            void* buffer = Allocate(UnsafeSimpleEntitySoA.GetRequiredByteSize(Capacity), UnsafeSimpleEntitySoA.Alignment);

            try
            {
                var view = UnsafeSimpleEntitySoA.ConvertExistingDataToSoA(buffer, Capacity, Allocator.None);

                Assert.IsTrue(view.IsCreated);
                Assert.AreEqual(Capacity, view.Capacity);
                Assert.IsTrue(view.DataPtr == buffer);
                Assert.AreEqual(Allocator.None, (Allocator)view.Allocator.Value);
            }
            finally
            {
                Free(buffer);
            }
        }

        [Test]
        public void ConvertExistingData_RoundTripsEveryFieldAcrossFullCapacity()
        {
            // Writing every field at every index proves the per-array strides derived from the
            // supplied capacity are correct and do not overlap.
            void* buffer = Allocate(UnsafeSimpleEntitySoA.GetRequiredByteSize(Capacity), UnsafeSimpleEntitySoA.Alignment);

            try
            {
                var view = UnsafeSimpleEntitySoA.ConvertExistingDataToSoA(buffer, Capacity, Allocator.None);

                for (int i = 0; i < Capacity; i++)
                {
                    view.HealthPtr[i] = i * 1.5f;
                    view.IdPtr[i] = i * 10;
                    view.PositionPtr[i] = new float3(i, i + 1, i + 2);
                }

                for (int i = 0; i < Capacity; i++)
                {
                    Assert.AreEqual(i * 1.5f, view.HealthPtr[i]);
                    Assert.AreEqual(i * 10, view.IdPtr[i]);
                    Assert.AreEqual(new float3(i, i + 1, i + 2), view.PositionPtr[i]);
                }
            }
            finally
            {
                Free(buffer);
            }
        }

        [Test]
        public void ConvertExistingData_FlaggedContainer_RoundTripsFieldsAndFlags()
        {
            // The bit array block sits past an 8-byte aligned gap after the primitive block, so this
            // is what proves the flag offsets survive conversion.
            var byteSize = UnsafeFlaggedEntitySoA.GetRequiredByteSize(Capacity);
            void* buffer = Allocate(byteSize, UnsafeFlaggedEntitySoA.Alignment);

            try
            {
                UnsafeUtility.MemClear(buffer, byteSize);

                var view = UnsafeFlaggedEntitySoA.ConvertExistingDataToSoA(buffer, Capacity, Allocator.None);

                view.SpeedPtr[3] = 5.5f;
                view.ScorePtr[3] = 77;
                view.IsActiveBitArray.Set(3, true);
                view.IsVisibleBitArray.Set(11, true);

                Assert.AreEqual(5.5f, view.SpeedPtr[3]);
                Assert.AreEqual(77, view.ScorePtr[3]);
                Assert.IsTrue(view.IsActiveBitArray.IsSet(3));
                Assert.IsFalse(view.IsActiveBitArray.IsSet(11));
                Assert.IsTrue(view.IsVisibleBitArray.IsSet(11));
                Assert.IsFalse(view.IsVisibleBitArray.IsSet(3));
            }
            finally
            {
                Free(buffer);
            }
        }

        [Test]
        public void ConvertExistingData_AliasesOwningContainer()
        {
            using var owner = new UnsafeSimpleEntitySoA(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            var view = UnsafeSimpleEntitySoA.ConvertExistingDataToSoA(owner.DataPtr, owner.Capacity, Allocator.None);

            owner.HealthPtr[7] = 42.5f;
            owner.PositionPtr[7] = new float3(1f, 2f, 3f);

            Assert.AreEqual(42.5f, view.HealthPtr[7]);
            Assert.AreEqual(new float3(1f, 2f, 3f), view.PositionPtr[7]);

            view.IdPtr[9] = 99;

            Assert.AreEqual(99, owner.IdPtr[9]);
        }

        [Test]
        public void ConvertExistingData_NonOwningDispose_LeavesSourceIntact()
        {
            using var owner = new UnsafeSimpleEntitySoA(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            owner.HealthPtr[0] = 12.5f;

            var view = UnsafeSimpleEntitySoA.ConvertExistingDataToSoA(owner.DataPtr, owner.Capacity, Allocator.None);
            view.Dispose();

            Assert.IsFalse(view.IsCreated);
            Assert.IsTrue(owner.IsCreated);
            Assert.AreEqual(12.5f, owner.HealthPtr[0]);
        }

        [Test]
        public void ConvertExistingData_OwningAllocator_DisposeReleasesBuffer()
        {
            void* buffer = Allocate(UnsafeSimpleEntitySoA.GetRequiredByteSize(Capacity), UnsafeSimpleEntitySoA.Alignment);

            var container = UnsafeSimpleEntitySoA.ConvertExistingDataToSoA(buffer, Capacity, Allocator.Persistent);
            container.HealthPtr[0] = 1f;

            // Ownership was transferred, so Dispose frees the buffer and no manual Free follows.
            // A leak or double free here is reported by the native leak detector.
            container.Dispose();

            Assert.IsFalse(container.IsCreated);
        }

        [Test]
        public void ConvertExistingData_NullBufferWithZeroCapacity_IsAllowed()
        {
            var view = UnsafeSimpleEntitySoA.ConvertExistingDataToSoA(null, 0, Allocator.None);

            Assert.IsFalse(view.IsCreated);
            Assert.AreEqual(0, view.Capacity);
        }

        [Test]
        public void GetRequiredByteSize_MatchesContainerByteSize()
        {
            using var simple = new UnsafeSimpleEntitySoA(Capacity, Allocator.Persistent);
            using var flagged = new UnsafeFlaggedEntitySoA(Capacity, Allocator.Persistent);

            Assert.AreEqual(simple.ByteSize, UnsafeSimpleEntitySoA.GetRequiredByteSize(simple.Capacity));
            Assert.AreEqual(flagged.ByteSize, UnsafeFlaggedEntitySoA.GetRequiredByteSize(flagged.Capacity));
        }

        [Test]
        public void GetRequiredByteSize_GenericMatchesConcrete()
        {
            Assert.AreEqual(
                UnsafeSimpleEntitySoA.GetRequiredByteSize(Capacity),
                SoAUnsafeUtility.GetRequiredByteSize<UnsafeSimpleEntitySoA>(Capacity));

            Assert.AreEqual(
                UnsafeFlaggedEntitySoA.GetRequiredByteSize(Capacity),
                SoAUnsafeUtility.GetRequiredByteSize<UnsafeFlaggedEntitySoA>(Capacity));
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void ConvertExistingData_NegativeCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                UnsafeSimpleEntitySoA.ConvertExistingDataToSoA(null, -1, Allocator.None));
        }

        [Test]
        public void ConvertExistingData_NullBufferWithNonZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                UnsafeSimpleEntitySoA.ConvertExistingDataToSoA(null, Capacity, Allocator.None));
        }

        [Test]
        public void ConvertExistingData_MisalignedBuffer_Throws()
        {
            Assert.Greater(UnsafeSimpleEntitySoA.Alignment, 1, "A one byte alignment cannot be violated.");

            var byteSize = UnsafeSimpleEntitySoA.GetRequiredByteSize(Capacity);
            void* buffer = Allocate(byteSize + UnsafeSimpleEntitySoA.Alignment, UnsafeSimpleEntitySoA.Alignment);

            try
            {
                // Held as IntPtr because a lambda cannot capture a variable of pointer type.
                var misaligned = (IntPtr)((byte*)buffer + 1);

                Assert.Throws<ArgumentException>(() =>
                    UnsafeSimpleEntitySoA.ConvertExistingDataToSoA((void*)misaligned, Capacity, Allocator.None));
            }
            finally
            {
                Free(buffer);
            }
        }

        [Test]
        public void SetCapacity_OnNonOwningView_Throws()
        {
            // A converted view over a non-owning allocator is fixed capacity: resizing would have to
            // reallocate through an allocator that does not own the buffer.
            using var owner = new UnsafeSimpleEntitySoA(Capacity, Allocator.Persistent);

            var view = UnsafeSimpleEntitySoA.ConvertExistingDataToSoA(owner.DataPtr, owner.Capacity, Allocator.None);

            Assert.Throws<ArgumentException>(() => view.SetCapacity(Capacity * 2));
        }
#endif
    }
}
