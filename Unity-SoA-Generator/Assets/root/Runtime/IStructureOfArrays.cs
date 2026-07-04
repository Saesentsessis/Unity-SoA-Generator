using Unity.Collections;

namespace Saesentsessis.DOD.SoA
{
	/// <summary>
	/// Defines the common memory footprint and allocation data for generated Structure of Arrays (SoA) containers.
	/// </summary>
	public unsafe interface IStructureOfArrays
	{
		/// <summary>
		/// The size of a single element's primitive data in bytes, excluding vertically packed bit flags.
		/// If boolean bit-packing is disabled, boolean fields are treated as standard 1-byte primitives 
		/// and are included in this total.
		/// </summary>
		int ElementSize { get; }
		
		/// <summary>
		/// The number of boolean bit flags reserved per element.
		/// </summary>
		int FlagCount { get; }
		
		/// <summary>
		/// A pointer to the head of the contiguous raw memory block containing both the primitive 
		/// arrays and/or the trailing bit arrays.
		/// </summary>
		void* DataPtr { get; }

		/// <summary>
		/// The maximum number of elements this container can currently hold before requiring a resize.
		/// </summary>
		int Capacity { get; }

		/// <summary>
		/// The handle to the native memory allocator used to create and manage this container's internal buffer.
		/// </summary>
		AllocatorManager.AllocatorHandle Allocator { get; }

		/// <summary>
		/// Whether this list has been allocated (and not yet deallocated).
		/// </summary>
		bool IsCreated { get; }

		/// <summary>
		/// The total size of the internal memory allocation in bytes, encompassing both the primitive 
		/// data block and the bit array block (including any required alignment padding).
		/// </summary>
		long ByteSize { get; }
	}
}