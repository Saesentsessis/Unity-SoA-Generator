using UnityEngine.Scripting;

namespace Saesentsessis.DOD.SoA
{
	/// <summary>
	/// Partial copy of internal Unity.Collections.Bitwise struct.
	/// Provides high-performance bitwise operations for memory alignment and layout calculations.
	/// </summary>
	[Preserve]
	public struct Bitwise
	{
		/// <summary>
		/// Masks out the lower bits of a value to round it down to the nearest multiple of the specified alignment.
		/// </summary>
		/// <param name="value">The raw memory offset or size to align.</param>
		/// <param name="alignPow2">The target alignment boundary. This must be a power of 2 (e.g., 2, 4, 8, 16).</param>
		/// <returns>The aligned value, which will be less than or equal to the original value.</returns>
		public static int AlignDown(int value, int alignPow2)
		{
			return value & ~(alignPow2 - 1);
		}

		/// <summary>
		/// Advances a value forward to the nearest multiple of the specified alignment.
		/// </summary>
		/// <param name="value">The raw memory offset or size to align.</param>
		/// <param name="alignPow2">The target alignment boundary. This must be a power of 2 (e.g., 2, 4, 8, 16).</param>
		/// <returns>The aligned value, which will be greater than or equal to the original value.</returns>
		public static int AlignUp(int value, int alignPow2)
		{
			return AlignDown(value + alignPow2 - 1, alignPow2);
		}

		/// <summary>
		/// Masks out the lower bits of a 64-bit value to round it down to the nearest multiple of the specified alignment.
		/// </summary>
		/// <param name="value">The raw 64-bit memory offset or size to align.</param>
		/// <param name="alignPow2">The target alignment boundary. This must be a power of 2 (e.g., 2, 4, 8, 16).</param>
		/// <returns>The aligned 64-bit value, which will be less than or equal to the original value.</returns>
		public static long AlignDown(long value, int alignPow2)
		{
			return value & ~(alignPow2 - 1);
		}

		/// <summary>
		/// Advances a 64-bit value forward to the nearest multiple of the specified alignment.
		/// </summary>
		/// <param name="value">The raw 64-bit memory offset or size to align.</param>
		/// <param name="alignPow2">The target alignment boundary. This must be a power of 2 (e.g., 2, 4, 8, 16).</param>
		/// <returns>The aligned 64-bit value, which will be greater than or equal to the original value.</returns>
		public static long AlignUp(long value, int alignPow2)
		{
			return AlignDown(value + alignPow2 - 1, alignPow2);
		}
		
		/// <summary>
		/// Calculates the total memory footprint required for a dual-block Structure of Arrays (SoA) layout.
		/// </summary>
		/// <param name="elementSize">The accumulated byte size of all non-boolean primitive fields in the struct.</param>
		/// <param name="capacity">The maximum number of elements the SoA container can hold.</param>
		/// <param name="flagCount">The number of boolean fields packed into the bit array block.</param>
		/// <returns>
		/// The total allocated size in bytes, accounting for the 8-byte alignment gap between
		/// the primary primitive block and the trailing bit array block.
		/// </returns>
		public static long ComputeByteSize(int elementSize, int capacity, int flagCount)
		{
			return AlignUp((long)elementSize * capacity, 8) + GetBlockCount(capacity) * flagCount;
		}

		/// <summary>
		/// Calculates the exact number of bytes required to store a contiguous array of bits.
		/// </summary>
		/// <param name="capacity">The total number of bits (elements) required.</param>
		/// <returns>The minimum number of 8-bit bytes needed to store the specified capacity.</returns>
		public static int GetBlockCount(int capacity)
		{
			return (AlignUp(capacity, 64) >> 6) << 3;
		}
	}
}