using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Saesentsessis.DOD.SoA
{
	/// <summary>
	/// Copy of internal Unity.Collections.LowLevel.Unsafe.UnsafeDisposeJob struct.
	/// </summary>
	[BurstCompile]
	public unsafe struct UnsafeDisposeJob : IJob
	{
		[NativeDisableUnsafePtrRestriction]
		public void* Ptr;
		public AllocatorManager.AllocatorHandle Allocator;
        
		public void Execute()
		{
			AllocatorManager.Free(Allocator, Ptr);
		}
	}
}