#ifndef __HAIRSIMDATA_HLSL__
#define __HAIRSIMDATA_HLSL__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#include "HairSimData.cs.hlsl"

//-------------
// solver data

#if HAIRSIM_WRITEABLE_SOLVERDATA
#define HAIRSIM_SOLVERDATA RWStructuredBuffer
#else
#define HAIRSIM_SOLVERDATA StructuredBuffer
#endif

StructuredBuffer<float> _RootScale;					// x: relative strand length [0..1] (to group maximum)
StructuredBuffer<float4> _RootPosition;				// xyz: strand root position, w: -
StructuredBuffer<float4> _RootDirection;			// xyz: strand root direction, w: -
StructuredBuffer<float4> _RootFrame;				// quat(xyz,w): strand root material frame where (0,1,0) is tangent

HAIRSIM_SOLVERDATA<float4> _InitialRootFrame;			// quat(xyz,w): initial strand root material frame
HAIRSIM_SOLVERDATA<float4> _InitialParticleOffset;		// xyz: initial particle offset from strand root, w: -
HAIRSIM_SOLVERDATA<float4> _InitialParticleFrameDelta;	// quat(xyz,w): initial particle material frame delta

HAIRSIM_SOLVERDATA<float4> _ParticlePosition;		// xyz: position, w: initial pose local accumulated weight (gather)
HAIRSIM_SOLVERDATA<float4> _ParticlePositionPrev;	// xyz: position, w: initial pose local accumulated weight (gather)
HAIRSIM_SOLVERDATA<float4> _ParticlePositionCorr;	// xyz: ftl correction, w: -
HAIRSIM_SOLVERDATA<float4> _ParticleVelocity;		// xyz: velocity, w: weight
HAIRSIM_SOLVERDATA<float4> _ParticleVelocityPrev;	// xyz: velocity, w: weight

//-------------
// volume data

#if HAIRSIM_WRITEABLE_VOLUMEDATA
#define HAIRSIM_VOLUMEDATA RWTexture3D
#else
#define HAIRSIM_VOLUMEDATA Texture3D
#endif

HAIRSIM_VOLUMEDATA<int> _AccuWeight;				// x: fp accumulated weight
HAIRSIM_VOLUMEDATA<int> _AccuWeight0;				// x: fp accumulated target weight
HAIRSIM_VOLUMEDATA<int> _AccuVelocityX;				// x: fp accumulated x-velocity
HAIRSIM_VOLUMEDATA<int> _AccuVelocityY;				// x: ... ... ... .. y-velocity
HAIRSIM_VOLUMEDATA<int> _AccuVelocityZ;				// x: .. ... ... ... z-velocity
//TODO this sure would be nice: https://developer.nvidia.com/unlocking-gpu-intrinsics-hlsl

HAIRSIM_VOLUMEDATA<float> _VolumeDensity;			// x: density
HAIRSIM_VOLUMEDATA<float> _VolumeDensity0;			// x: density target
HAIRSIM_VOLUMEDATA<float4> _VolumeVelocity;			// xyz: velocity, w: accumulated weight
HAIRSIM_VOLUMEDATA<float> _VolumeDivergence;

HAIRSIM_VOLUMEDATA<float> _VolumePressure;
HAIRSIM_VOLUMEDATA<float> _VolumePressureNext;
HAIRSIM_VOLUMEDATA<float3> _VolumePressureGrad;

SamplerState _Volume_point_clamp;
SamplerState _Volume_trilinear_clamp;

struct BoundaryCapsule { float3 centerA; float radius; float3 centerB; float __pad__; };
struct BoundarySphere { float3 center; float radius; };
struct BoundaryTorus { float3 center; float radiusA; float3 axis; float radiusB; };
struct BoundaryPack
{
	//  shape   |   capsule     sphere      torus
	//  ----------------------------------------------
	//  float3  |   centerA     center      center
	//  float   |   radius      radius      radiusA
	//  float3  |   centerB     __pad__     axis
	//  float   |   __pad__     __pad__     radiusB

	float3 pA;
	float tA;
	float3 pB;
	float tB;
};

StructuredBuffer<BoundaryCapsule> _BoundaryCapsule;
StructuredBuffer<BoundarySphere> _BoundarySphere;
StructuredBuffer<BoundaryTorus> _BoundaryTorus;
StructuredBuffer<BoundaryPack> _BoundaryPack;

StructuredBuffer<float4x4> _BoundaryMatrix;
StructuredBuffer<float4x4> _BoundaryMatrixInv;
StructuredBuffer<float4x4> _BoundaryMatrixW2PrevW;

#endif//__HAIRSIMDATA_HLSL__
