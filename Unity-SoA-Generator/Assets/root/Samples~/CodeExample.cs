using System;
using Saesentsessis.DOD.SoA.CodeGen;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Saesentsessis.DOD.SoA.Examples
{
    [GenerateSoA(GenerateNativeContainer = true)]
    public struct PointMass
    {
        public float3 Position;
        public float3 PreviousPosition;
        public float3 Velocity;
        public float InverseMass;
    }

    public class UsageExample
    {
        private const int Capacity = 256;
        
        public void ExecuteSafe()
        {
            // Allocate container
            NativePointMassSoA points = new NativePointMassSoA(Capacity, Allocator.TempJob);

            var positions = points.Position;
            var previousPositions = points.PreviousPosition;
            var velocities = points.Velocity;
            var inverseMasses = points.InverseMass;

            var random = new Random((uint)DateTime.Now.Ticks);
            
            // Fill your data
            for (int i = Capacity - 1; i >= 0; i--)
            {
                previousPositions[i] = positions[i] = random.NextFloat3(new float3(-1f), new float3(1f));
                velocities[i] = float3.zero;
                inverseMasses[i] = random.NextFloat(1f, 2f);
            }

            // Construct jobs as usual
            var updateVelocityJob = new UpdateVelocityJob
            {
                Positions = positions,
                PreviousPositions = previousPositions,
                Velocities = velocities,
                DeltaTimeReciprocal = 1f / Time.deltaTime
            };

            var applyVelocityJob = new ApplyVelocityJob
            {
                Positions = positions,
                Velocities = velocities,
                DeltaTime = Time.deltaTime
            };

            // Schedule them
            var jobHandle = updateVelocityJob.Schedule(Capacity, default);
            
            jobHandle = applyVelocityJob.Schedule(Capacity, jobHandle);
            
            // Complete manually or store JobHande
            jobHandle.Complete();

            // Don't forget to dispose unmanaged resources
            points.Dispose();
        }

        public unsafe void ExecuteUnsafe()
        {
            // Allocate container
            UnsafePointMassSoA points = new UnsafePointMassSoA(Capacity, Allocator.TempJob);
            
            var positionsPtr = points.PositionPtr;
            var previousPositionsPtr = points.PreviousPositionPtr;
            var velocitiesPtr = points.VelocityPtr;
            var inverseMassesPtr = points.InverseMassPtr;
            
            var random = new Random((uint)DateTime.Now.Ticks);

            // Fill your data
            for (int i = Capacity - 1; i >= 0; i--)
            {
                previousPositionsPtr[i] = positionsPtr[i] = random.NextFloat3(new float3(-1f), new float3(1f));
                velocitiesPtr[i] = float3.zero;
                inverseMassesPtr[i] = random.NextFloat(1f, 2f);
            }

            // Construct jobs as usual
            var updateVelocityJob = new UpdateVelocityUnsafeJob
            {
                PositionsPtr = positionsPtr,
                PreviousPositionsPtr = previousPositionsPtr,
                VelocitiesPtr = velocitiesPtr,
                DeltaTimeReciprocal = 1f / Time.deltaTime
            };

            var applyVelocityJob = new ApplyVelocityUnsafeJob
            {
                PositionsPtr = positionsPtr,
                VelocitiesPtr = velocitiesPtr,
                DeltaTime = Time.deltaTime
            };
            
            // Schedule them
            var jobHandle = updateVelocityJob.Schedule(Capacity, default);
            
            jobHandle = applyVelocityJob.Schedule(Capacity, jobHandle);
            
            // Complete manually or store JobHande
            jobHandle.Complete();

            // Don't forget to dispose unmanaged resources
            points.Dispose();
        }
    }
    
    [BurstCompile(DisableSafetyChecks = true)]
    public struct ApplyVelocityJob : IJobFor
    {
        public NativeSlice<float3> Positions;
        
        public NativeSlice<float3> Velocities;
        
        public float3 DeltaTime;

        public void Execute(int index)
        {
            Positions[index] = math.mad(Velocities[index], DeltaTime, Positions[index]);
        }
    }

    [BurstCompile(DisableSafetyChecks = true)]
    public struct UpdateVelocityJob : IJobFor
    {
        [ReadOnly] public NativeSlice<float3> Positions;
        [ReadOnly] public NativeSlice<float3> PreviousPositions;
        
        [WriteOnly] public NativeSlice<float3> Velocities;

        public float3 DeltaTimeReciprocal;
        
        public void Execute(int index)
        {   
            Velocities[index] = (Positions[index] - PreviousPositions[index]) * DeltaTimeReciprocal;
        }
    }

    [BurstCompile(DisableSafetyChecks = true)]
    public unsafe struct ApplyVelocityUnsafeJob : IJobFor
    {
        [NativeDisableUnsafePtrRestriction]
        [NoAlias] public float3* PositionsPtr;
        
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly, NoAlias] public float3* VelocitiesPtr;
        
        public float3 DeltaTime;

        public void Execute(int index)
        {
            PositionsPtr[index] = math.mad(VelocitiesPtr[index], DeltaTime, PositionsPtr[index]);
        }
    }

    [BurstCompile(DisableSafetyChecks = true)]
    public unsafe struct UpdateVelocityUnsafeJob : IJobFor
    {
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly, NoAlias] public float3* PositionsPtr;
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly, NoAlias] public float3* PreviousPositionsPtr;
        
        [NativeDisableUnsafePtrRestriction]
        [WriteOnly, NoAlias] public float3* VelocitiesPtr;

        public float3 DeltaTimeReciprocal;
        
        public void Execute(int index)
        {   
            VelocitiesPtr[index] = (PositionsPtr[index] - PreviousPositionsPtr[index]) * DeltaTimeReciprocal;
        }
    }
}
