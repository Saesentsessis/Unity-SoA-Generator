using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.Specialized;

namespace Saesentsessis.DOD.SoA
{
	/// <summary>
	/// Low-level helpers shared by the generated Structure of Arrays (SoA) containers when they
	/// reinterpret memory the caller already owns.
	/// </summary>
	public static unsafe class SoAUnsafeUtility
	{
		/// <summary>
		/// Validates the arguments of a generated container's ConvertExistingDataToSoA.
		/// </summary>
		/// <param name="dataPtr">The buffer that is about to be reinterpreted.</param>
		/// <param name="capacity">The capacity the buffer is claimed to be laid out for.</param>
		/// <param name="elementSize">The container's ElementSize constant.</param>
		/// <param name="flagCount">The container's FlagCount constant.</param>
		/// <param name="alignment">The container's Alignment constant.</param>
		/// <param name="maxCapacity">The container's MaxCapacity constant.</param>
		/// <remarks>
		/// The layout constants are passed in rather than read back from the container so that this
		/// check stays a single implementation shared by every generated type, while remaining a
		/// compile-time constant expression at each call site. The whole call is stripped outside of
		/// ENABLE_UNITY_COLLECTIONS_CHECKS.
		/// </remarks>
		[Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
		public static void CheckConvertArguments(void* dataPtr, int capacity, int elementSize, int flagCount,
			int alignment, int maxCapacity)
		{
			CollectionUtils.CheckCapacityInRange(capacity, maxCapacity);

			if (dataPtr == null && capacity != 0)
				throw new ArgumentException(
					$"A null buffer requires a capacity of 0, but {capacity} was requested.", nameof(dataPtr));

			if (((ulong)dataPtr & (ulong)(alignment - 1)) != 0)
				throw new ArgumentException(
					$"Buffer at 0x{(ulong)dataPtr:X} is not aligned to {alignment} bytes.", nameof(dataPtr));

			CollectionUtils.CheckByteSizeInRange(Bitwise.ComputeByteSize(elementSize, capacity, flagCount), int.MaxValue);
		}

		/// <summary>
		/// The number of bytes a buffer must span to back a container of type
		/// <typeparamref name="T"/> at the given capacity.
		/// </summary>
		/// <typeparam name="T">The generated SoA container type.</typeparam>
		/// <param name="capacity">The capacity the buffer is laid out for.</param>
		/// <returns>The required size of the data block in bytes.</returns>
		/// <remarks>
		/// Equivalent to the container's own static GetRequiredByteSize, for callers that are generic
		/// over container type and therefore cannot reach its static members.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long GetRequiredByteSize<T>(int capacity)
			where T : unmanaged, IStructureOfArrays
		{
			T layout = default;
			return Bitwise.ComputeByteSize(layout.ElementSize, capacity, layout.FlagCount);
		}
	}
}
